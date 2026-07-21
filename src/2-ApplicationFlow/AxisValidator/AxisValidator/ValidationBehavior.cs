using Axis;
using AxisMediator.Contracts.CQRS;
using AxisMediator.Contracts.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace AxisValidator;

public class ValidationBehavior<TRequest>(IServiceProvider serviceProvider)
    : IAxisPipelineBehavior<TRequest>
    where TRequest : IAxisRequest
{
    public async Task<AxisResult> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)
    {
        var validator = serviceProvider.GetService<IAxisValidator<TRequest>>();
        if (validator is null) return await next();

        var result = await validator.ValidateAsync(request);
        return result.IsFailure ? result : await next();
    }
}

public class ValidationBehavior<TRequest, TResponse>(IServiceProvider serviceProvider)
    : IAxisPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxisRequest
    where TResponse : IAxisResponse
{
    public async Task<AxisResult<TResponse>> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)
    {
        var validator = serviceProvider.GetService<IAxisValidator<TRequest>>();
        if (validator is null) return await next();

        var result = await validator.ValidateAsync(request);
        return result.IsFailure ? result.Errors.ToArray() : await next();
    }
}
