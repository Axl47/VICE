using PersonalMods.Networking;

internal static class ReliableOutboxTests
{
    internal static void Run(Action<bool,string> check)
    {
        var queue = new ReliableOutbox<string>();
        var wire = new SynchronousPacketPayload(100);
        var delivered = new List<string>();
        var failed = new List<string>();
        queue.Enqueue("chunk-zero");queue.Enqueue("chunk-one");
        check(!queue.SendNext(0,p=>wire.Send(p,()=>{}),(p,e)=>failed.Add(p)) && queue.Count==2,
            "A swallowed native writer failure discarded a video chunk.");
        check(!queue.SendNext(.1,p=>delivered.Add(p),(p,e)=>failed.Add(p)) && delivered.Count==0,
            "Packet retry ignored backoff.");
        check(queue.SendNext(.25,p=>wire.Send(p,()=>wire.Write(delivered.Add)),(p,e)=>failed.Add(p)),
            "The retained packet could not be retried after a writer failure.");
        queue.SendNext(.25,p=>wire.Send(p,()=>wire.Write(delivered.Add)),(p,e)=>failed.Add(p));
        check(delivered.SequenceEqual(new[]{"chunk-zero","chunk-one"}) && queue.Count==0 && failed.Count==0,
            "Packet retry changed video ordering or duplicated queued bytes.");
        queue.Enqueue("broken");queue.Enqueue("control");
        Action<string> broken=p=>throw new IOException("Native send failed");
        queue.SendNext(1,broken,(p,e)=>failed.Add(p));queue.SendNext(1.25,broken,(p,e)=>failed.Add(p));
        check(queue.SendNext(1.75,broken,(p,e)=>failed.Add(p)) && failed.SequenceEqual(new[]{"broken"}),
            "Permanent send failure did not finish after three attempts.");
        queue.SendNext(1.75,delivered.Add,(p,e)=>failed.Add(p));
        check(delivered[^1]=="control", "Failed video traffic permanently blocked TV controls.");
        queue.Enqueue("video");queue.Enqueue("pause");queue.RemoveWhere(p=>p=="video");
        queue.SendNext(2,delivered.Add,(p,e)=>failed.Add(p));
        check(delivered[^1]=="pause" && queue.Count==0, "Cancelling a video removed an unrelated TV control.");
    }
}
