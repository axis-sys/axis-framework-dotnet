using Axis;

namespace AxisMediator.Contracts.CQRS.Queries;

public interface IAxisQueryHandler<in TQuery, TResponse>
    where TQuery : IAxisQuery<TResponse>
    where TResponse : IAxisQueryResponse
{
    Task<AxisResult<TResponse>> HandleAsync(TQuery query);
}
