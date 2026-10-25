using Axis.SharedKernel;
using Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.CancelValidation;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class CancelValidationHandlerTests : AuthTestHost
{
    private static readonly string SagaId = Guid.CreateVersion7().ToString();

    [Fact]
    public async Task RequestsTheCancelWhenTheRunIsAValidateEmailRunAsync()
    {
        SagaMediator.Setup(saga => saga.GetByIdAsync(SagaId)).ReturnsAsync(AxisResult.Ok(Run(ValidateEmailSaga.Name, AxisSagaStatus.Running)));
        SagaMediator.Setup(saga => saga.CancelAsync(SagaId)).ReturnsAsync(AxisResult.Ok(Run(ValidateEmailSaga.Name, AxisSagaStatus.Cancelling)));
        var facade = Build<IEmailValidationFacade>();

        var result = await facade.CancelValidationAsync(new CancelValidationCommand { SagaId = SagaId });

        var cancelled = result.ShouldSucceed();
        Assert.Equal(SagaId, cancelled.SagaId);
        Assert.Equal(nameof(AxisSagaStatus.Cancelling), cancelled.Status);
    }

    [Fact]
    public async Task FailsWithTheRunNotFoundWithoutCancellingWhenTheSagaIsOfAnotherTypeAsync()
    {
        SagaMediator.Setup(saga => saga.GetByIdAsync(SagaId)).ReturnsAsync(AxisResult.Ok(Run("AnotherSaga", AxisSagaStatus.Running)));
        var facade = Build<IEmailValidationFacade>();

        var result = await facade.CancelValidationAsync(new CancelValidationCommand { SagaId = SagaId });

        result.ShouldFailWithCode(SharedErrors.SagaRunNotFound.Code);
        SagaMediator.Verify(saga => saga.CancelAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FailsWithoutCancellingWhenTheSagaIdIsNotAGuid7Async()
    {
        var facade = Build<IEmailValidationFacade>();

        var result = await facade.CancelValidationAsync(new CancelValidationCommand { SagaId = "not-a-saga-id" });

        result.ShouldFailWithCode(ValidationCodeErrors.SagaIdInvalid);
        SagaMediator.Verify(saga => saga.CancelAsync(It.IsAny<string>()), Times.Never);
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
