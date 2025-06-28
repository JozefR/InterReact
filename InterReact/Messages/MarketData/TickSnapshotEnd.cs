using InterReact.Core;
using InterReact.Interfaces;

namespace InterReact.Messages.MarketData;

public sealed class SnapshotEndTick : IHasRequestId
{
    public int RequestId { get; }
    internal SnapshotEndTick(ResponseReader r)
    {
        r.IgnoreMessageVersion();
        RequestId = r.ReadInt();
    }
};
