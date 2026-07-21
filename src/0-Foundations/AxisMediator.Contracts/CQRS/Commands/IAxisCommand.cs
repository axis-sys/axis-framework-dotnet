namespace AxisMediator.Contracts.CQRS.Commands;

public interface IAxisCommand : IAxisRequest;

public interface IAxisCommand<TResponse> : IAxisRequest where TResponse : IAxisCommandResponse;
