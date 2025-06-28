using InterReact.Core;
using InterReact.Interfaces;

namespace InterReact.Messages.News;

public sealed class NewsArticle : IHasRequestId
{
    public int RequestId { get; }
    public int ArticleType { get; }
    public string ArticleText { get; }

    internal NewsArticle(ResponseReader r)
    {
        RequestId = r.ReadInt();
        ArticleType = r.ReadInt();
        ArticleText = r.ReadString();
    }
}
