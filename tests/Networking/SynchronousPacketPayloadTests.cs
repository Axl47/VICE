using PersonalMods.Networking;

internal static class SynchronousPacketPayloadTests
{
    internal static void Run(Action<bool,string> check)
    {
        var studio=new SynchronousPacketPayload(32);
        var tv=new SynchronousPacketPayload(32);
        void Reject(Action action,string message)
        {
            try { action(); }
            catch (Exception error) when (error is InvalidOperationException or InvalidDataException)
            { check(true,message);return; }
            check(false,message);
        }

        // Reproduce callbacks that do not carry the original packet identity.
        string? encoded=null;
        studio.Send("studio",()=>studio.Write(text=>encoded=text));
        check(encoded=="studio","Native callback must serialize without a packet pointer.");
        studio.Read(()=>encoded);
        check(studio.TryTake(out var decoded)&&decoded=="studio","Receive must not depend on the reader's packet identity.");
        check(!studio.TryTake(out _),"A payload must not be delivered twice.");

        studio.Send("outer",()=>
        {
            tv.Send("tv",()=>tv.Write(text=>check(text=="tv","TV and studio outgoing slots mixed.")));
            Reject(()=>studio.Send("nested",()=>{}),"Overlapping sends must not replace the active packet.");
            studio.Write(text=>check(text=="outer","An overlapping send replaced the active payload."));
        });
        studio.Read(()=>"studio reply");tv.Read(()=>"tv reply");
        check(tv.TryTake(out decoded)&&decoded=="tv reply","TV must receive independently of the studio.");
        check(studio.TryTake(out decoded)&&decoded=="studio reply","Studio must receive independently of TV.");

        Reject(()=>studio.Send("missing",()=>{}),"A skipped native writer must not count as a successful send.");
        Reject(()=>studio.Send("broken",()=>
        {
            // IL2CPP's trampoline logs and swallows a callback exception.
            try { studio.Write(_=>throw new InvalidDataException("native writer failed")); }
            catch (InvalidDataException) { }
        }),"A swallowed callback failure must reach the sender.");
        Reject(()=>studio.Send("throw",()=>throw new InvalidOperationException()),"Broadcast exceptions must propagate.");
        studio.Send("recovered",()=>studio.Write(text=>check(text=="recovered","A failed send leaked its payload.")));
        Reject(()=>studio.Write(_=>{}),"A completed send must release its outgoing slot.");
        studio.Send("once",()=>
        {
            studio.Write(_=>{});
            Reject(()=>studio.Write(_=>{}),"A second write must not serialize the same payload again.");
        });

        Reject(()=>studio.Send(new string('x',33),()=>check(false,"Oversized message reached native broadcast.")),"Outgoing size limit was bypassed.");
        studio.Read(()=>"old");
        Reject(()=>studio.Read(()=>null),"Null incoming payload was accepted.");
        check(!studio.TryTake(out _),"A failed read replayed the previous packet.");
        studio.Read(()=>"old");
        Reject(()=>studio.Read(()=>new string('x',33)),"Incoming size limit was bypassed.");
        check(!studio.TryTake(out _),"An oversized read retained a stale packet.");
        studio.Read(()=>"old");
        Reject(()=>studio.Read(()=>throw new InvalidDataException()),"Decode errors must propagate to the reader callback.");
        check(!studio.TryTake(out _),"A decode error replayed stale data.");
        studio.Read(()=>"");check(studio.TryTake(out decoded)&&decoded=="","Empty payload confused with missing payload.");
        studio.Read(()=>"reset");studio.Reset();
        check(!studio.TryTake(out _),"A session reset retained incoming data.");
        studio.Send(new string('x',32),()=>studio.Write(text=>check(text.Length==32,"Maximum-sized payload failed.")));
    }
}
