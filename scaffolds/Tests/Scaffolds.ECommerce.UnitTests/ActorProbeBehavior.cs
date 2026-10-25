using AxisMediator.Contracts.CQRS;
using Scaffolds.ECommerce.Application.Pipelines;

namespace Scaffolds.ECommerce.UnitTests;

internal sealed class ActorProbeBehavior<TRequest, TResponse>(ActorProbe probe) : IAxisPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxisRequest
    where TResponse : IAxisResponse
{
    public Task<AxisResult<TResponse>> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)
    {
        var stamped = context.Items.TryGetValue(PipelineKeys.Actor, out var actor);
        probe.Observations.Add(new ActorProbe.Observation(stamped, actor as AxisEntityId?));
        return next();
    }
}
