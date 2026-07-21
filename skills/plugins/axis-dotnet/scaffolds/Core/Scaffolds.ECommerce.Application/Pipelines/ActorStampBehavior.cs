namespace Scaffolds.ECommerce.Application.Pipelines;

internal static class PipelineKeys
{
    public const string Actor = "scaffold.pipeline.actor";
}

internal sealed class ActorStampBehavior<TRequest, TResponse>(
    IAxisMediator mediator
) : IAxisPipelineBehavior<TRequest, TResponse> where TRequest : IAxisRequest  where TResponse : IAxisResponse
{
    #region scaffold:pipeline-behavior
    public Task<AxisResult<TResponse>> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)
    {
        context.Set(PipelineKeys.Actor, mediator.AxisEntityId);
        return next();
    }
    #endregion
}
