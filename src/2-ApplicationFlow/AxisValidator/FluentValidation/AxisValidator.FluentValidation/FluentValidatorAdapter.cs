using Axis;
using AxisMediator.Contracts;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AxisValidator;

internal class FluentValidatorAdapter<T>(IAxisMediator mediator, IServiceProvider serviceProvider) : IAxisValidator<T>
{
    public AxisResult Validate(T instance)
    {
        var validator = serviceProvider.GetService<IValidator<T>>();
        if (validator is null) return AxisResult.Ok();

        var result = validator.Validate(instance);

        if (result.IsValid)
            return AxisResult.Ok();

        var errors = result.Errors
            .Select(e => AxisError.ValidationRule(e.ErrorCode))
            .ToList();

        return AxisResult.Error(errors);
    }

    public async Task<AxisResult> ValidateAsync(T instance)
    {
        var validator = serviceProvider.GetService<IValidator<T>>();
        if (validator is null) return AxisResult.Ok();

        var result = await validator.ValidateAsync(instance, mediator.CancellationToken);

        if (result.IsValid)
            return AxisResult.Ok();

        var errors = result.Errors
            .Select(e => AxisError.ValidationRule(e.ErrorCode))
            .ToList();

        return AxisResult.Error(errors);
    }
}
