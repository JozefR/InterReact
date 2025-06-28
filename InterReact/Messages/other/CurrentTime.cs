using InterReact.Core;

namespace InterReact.Messages.other;

public sealed class CurrentTime
{
    public long Seconds { get; }
    internal CurrentTime(ResponseReader r)
    {
        r.IgnoreMessageVersion();
        Seconds =r.ReadLong();
    }
}
