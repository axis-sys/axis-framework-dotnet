namespace Scaffolds.ECommerce.Application.Pipelines;

internal sealed class ActorStampBehavior<TRequest, TResponse>(
    IAxisMediator mediator
) : IAxisPipelineBehavior<TRequest, TResponse> where TRequest : IAxisRequest  where TResponse : IAxisResponse
{
    public Task<AxisResult<TResponse>> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)
    {
        context.Set(PipelineKeys.Actor, mediator.AxisEntityId);
        return next();
    }
}
