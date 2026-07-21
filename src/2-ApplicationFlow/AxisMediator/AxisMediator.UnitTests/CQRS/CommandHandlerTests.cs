using AxisMediator.Contracts;
using AxisMediator.Contracts.CQRS.Commands;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace AxisMediator.UnitTests.CQRS;

public class CommandHandlerTests : BaseUnitTest
{
    public record TestCommand : IAxisCommand<TestResponse>
    {
        public bool? Ping { get; init; }
    }

    public record TestResponse : IAxisCommandResponse
    {
        public required bool Pong { get; init; }
    }

    public class TestHandler : IAxisCommandHandler<TestCommand, TestResponse>
    {
        public Task<AxisResult<TestResponse>> HandleAsync(TestCommand command)
            => Task.FromResult<AxisResult<TestResponse>>(
                command.Ping!.Value
                    ? new TestResponse { Pong = true }
                    : throw new ValidationTestException("ping is null or false"));
    }

    [Fact]
    public async Task ShouldReturnValidationRuleErrorWhenExternalApiIdIsNullAsync()
    {
        //Arrange
        using var scope = DefaultServiceProvider().CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();

        //Act
        var result = await mediator.Cqrs.ExecuteAsync<TestCommand, TestResponse>(new TestCommand() { Ping = true });

        //Assert
        result.ShouldSucceed();
        Assert.True(result.Match(onSuccess: () => true, onFailure: _ => false));
    }

}
