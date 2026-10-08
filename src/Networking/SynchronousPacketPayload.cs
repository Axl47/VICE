namespace PersonalMods.Networking;

// FishNet's native broadcast methods pass T by value. Injected IL2CPP classes
// cannot use the callback argument's pointer as the identity of the sent packet.
// Keep the payload for the duration of the synchronous serializer/handler call.
internal sealed class SynchronousPacketPayload(int maximumChars)
{
    private readonly object gate = new();
    private string? outgoing, incoming;
    private bool written;

    internal void Send(string payload, Action broadcast)
    {
        Validate(payload);
        lock (gate)
        {
            if (outgoing is not null) throw new InvalidOperationException("Another packet is being serialized.");
            outgoing = payload;
            written = false;
            try
            {
                broadcast();
                // IL2CPP delegate trampolines swallow managed exceptions. Do not
                // report a successful send when the writer callback failed.
                if (!written) throw new InvalidOperationException("The native packet writer did not complete.");
            }
            finally { outgoing = null; written = false; }
        }
    }

    internal void Write(Action<string> write)
    {
        lock (gate)
        {
            if (outgoing is null || written) throw new InvalidOperationException("No packet is awaiting serialization.");
            write(outgoing);
            written = true;
        }
    }

    internal void Read(Func<string?> read)
    {
        lock (gate)
        {
            incoming = null;
            var payload = read();
            Validate(payload);
            incoming = payload;
        }
    }

    internal bool TryTake(out string payload)
    {
        lock (gate)
        {
            payload = incoming ?? "";
            var available = incoming is not null;
            incoming = null;
            return available;
        }
    }

    internal void Reset()
    {
        lock (gate) { outgoing = incoming = null; written = false; }
    }

    private void Validate(string? payload)
    {
        if (payload is null || payload.Length > maximumChars)
            throw new InvalidDataException("Packet payload is missing or exceeds its size limit.");
    }
}
