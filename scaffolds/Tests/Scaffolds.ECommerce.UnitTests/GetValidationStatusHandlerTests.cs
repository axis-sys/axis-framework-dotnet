using Axis.SharedKernel;
using Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class GetValidationStatusHandlerTests : AuthTestHost
{
    private static readonly string SagaId = Guid.CreateVersion7().ToString();

    [Fact]
    public async Task ReportsTheRunWhenItIsAValidateEmailRunAsync()
    {
        SagaMediator.Setup(saga => saga.GetByIdAsync(SagaId)).ReturnsAsync(AxisResult.Ok(Run(ValidateEmailSaga.Name, AxisSagaStatus.Completed)));
        var facade = Build<IEmailValidationFacade>();

        var result = await facade.GetValidationStatusAsync(new GetValidationStatusQuery { SagaId = SagaId });

        var run = result.ShouldSucceed();
        Assert.Equal(nameof(AxisSagaStatus.Completed), run.Status);
        Assert.True(run.EmailValidated);
    }

    [Fact]
    public async Task FailsWithTheRunNotFoundWhenTheSagaIsOfAnotherTypeAsync()
    {
        SagaMediator.Setup(saga => saga.GetByIdAsync(SagaId)).ReturnsAsync(AxisResult.Ok(Run("AnotherSaga", AxisSagaStatus.Running)));
        var facade = Build<IEmailValidationFacade>();

        var result = await facade.GetValidationStatusAsync(new GetValidationStatusQuery { SagaId = SagaId });

        result.ShouldFailWithCode(SharedErrors.SagaRunNotFound.Code);
    }

    private static AxisSagaInstance Run(string sagaName, AxisSagaStatus status) => new()
    {
        SagaId = SagaId,
        SagaName = sagaName,
        Status = status,
        PayloadJson = "{}",
        Version = 1,
        CreatedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
    };
}
