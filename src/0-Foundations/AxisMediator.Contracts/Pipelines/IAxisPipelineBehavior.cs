using Axis;
using AxisMediator.Contracts.CQRS;

namespace AxisMediator.Contracts.Pipelines;

public interface IAxisPipelineBehavior<in TRequest> where TRequest : IAxisRequest
{
    Task<AxisResult> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next);
}

public interface IAxisPipelineBehavior<in TRequest, TResponse>
    where TRequest : IAxisRequest
    where TResponse : IAxisResponse
{
    Task<AxisResult<TResponse>> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next);
}
