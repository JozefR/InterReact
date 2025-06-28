using InterReact.Core;
using InterReact.Interfaces;

namespace InterReact.Messages.other;

public sealed class HeadTimestamp : IHasRequestId
{
    public int RequestId { get; }
    public string HeadTimeStamp { get; }

    internal HeadTimestamp(ResponseReader r)
    {
        RequestId = r.ReadInt();
        HeadTimeStamp = r.ReadString();
    }
}
