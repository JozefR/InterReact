using InterReact.Core;
using InterReact.Interfaces;

namespace InterReact.Messages.MarketData;

public sealed class TickRequestParams : IHasRequestId
{
    public int RequestId { get; }
    public double MinTick { get; }
    public string BboExchange { get; }
    public int SnapshotPermissions { get; }
    internal TickRequestParams(ResponseReader r)
    {
        RequestId = r.ReadInt();
        MinTick = r.ReadDouble();
        BboExchange = r.ReadString();
        SnapshotPermissions = r.ReadInt();
    }
};
