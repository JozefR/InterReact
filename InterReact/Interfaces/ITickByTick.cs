using InterReact.Enums;

namespace InterReact.Interfaces;

public interface ITickByTick : IHasRequestId
{
    TickByTickType TickByTickType { get; }
}
