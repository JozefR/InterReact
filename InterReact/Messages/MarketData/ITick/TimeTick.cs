using System.Globalization;
using InterReact.Enums;

namespace InterReact.Messages.MarketData.ITick;

public sealed class TimeTick : Interfaces.ITick // from StringTick
{
    public int RequestId { get; }
    public TickType TickType { get; }
    /// <summary>
    /// Seconds precision.
    /// </summary>
    public Instant Time { get; }
    internal TimeTick(int requestId, TickType tickType, string str) 
    {
        RequestId = requestId;
        TickType = tickType;
        Time = Instant.FromUnixTimeSeconds(long.Parse(str, NumberFormatInfo.InvariantInfo));
    }
}
