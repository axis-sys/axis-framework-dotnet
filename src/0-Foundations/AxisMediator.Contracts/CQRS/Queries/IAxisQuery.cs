namespace AxisMediator.Contracts.CQRS.Queries;

public interface IAxisQuery : IAxisRequest;

public interface IAxisQuery<TResponse> : IAxisQuery where TResponse : IAxisQueryResponse;
