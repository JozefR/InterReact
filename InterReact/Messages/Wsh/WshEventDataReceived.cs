using InterReact.Core;
using InterReact.Interfaces;

namespace InterReact.Messages.Wsh;

public sealed class WshEventDataReceived : IHasRequestId // output
{
    public int RequestId { get; }
    public string Data { get; }

    internal WshEventDataReceived(ResponseReader r)
    {
        RequestId = r.ReadInt();
        Data = r.ReadString();
    }
}
