namespace AxisMediator.Contracts.CQRS.Queries;

public interface IAxisStreamQuery<out TItem> : IAxisRequest;

public interface IAxisStreamQueryHandler<in TQuery, out TItem>
    where TQuery : IAxisStreamQuery<TItem>
{
    IAsyncEnumerable<TItem> HandleAsync(TQuery query);
}
