using InterReact.Core;
using InterReact.Enums;

namespace InterReact.Messages.MarketData.ITick;

public sealed class GenericTick : Interfaces.ITick
{
    public int RequestId { get; }
    public TickType TickType { get; }
    public double Value { get; }

    internal GenericTick(ResponseReader r)
    {
        r.IgnoreMessageVersion();
        RequestId = r.ReadInt();
        TickType = r.ReadEnum<TickType>();
        Value = r.ReadDouble();
    }
};
