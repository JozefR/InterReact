using InterReact.Core;
using InterReact.Interfaces;

namespace InterReact.Messages.other;

public sealed class ReplaceFAEnd : IHasRequestId
{
    public int RequestId { get; }
    public string Text { get; }

    internal ReplaceFAEnd(ResponseReader reader)
    {
        RequestId = reader.ReadInt();
        Text = reader.ReadString();
    }
}
