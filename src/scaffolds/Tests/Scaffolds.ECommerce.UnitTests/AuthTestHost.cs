using Axis.Ports;
using Microsoft.Extensions.Options;
using Scaffolds.ECommerce.Adapters.Driving.Facade;
using Scaffolds.ECommerce.Application;
using Scaffolds.ECommerce.Application.Auth;
using Scaffolds.ECommerce.Contracts.Driven.Auth;
using Scaffolds.ECommerce.Contracts.Driven.Customers;
using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.UnitTests;

// Shared base for the auth/customers slices (testing-unit-serviceprovider-mocks): the real application
// wiring plus the facade adapter, with every driven port mocked to a permissive default.
public abstract class AuthTestHost
{
    protected static readonly ExternalUser DefaultIdentity = new("0192aa00-0000-7000-8000-0000000000c1", "ada@example.com", "Ada", AuthSchemes.MsEntra);

    protected Mock<IExternalIdentityContext> ExternalIdentity { get; } = new(MockBehavior.Loose);
    protected Mock<ICustomersPort> Customers { get; } = new(MockBehavior.Loose);
    protected Mock<IValidationCodesPort> ValidationCodes { get; } = new(MockBehavior.Loose);
    protected Mock<IAxisEmailService> Email { get; } = new(MockBehavior.Loose);
    protected Mock<IUnitOfWork> UnitOfWork { get; } = new(MockBehavior.Loose);
    protected Mock<IAxisSagaMediator> SagaMediator { get; } = new(MockBehavior.Loose);
    protected AxisEntityId? Identity { get; set; } = AxisEntityId.New;

    protected AuthTestHost()
    {
        ExternalIdentity.Setup(context => context.Get()).Returns(AxisResult.Ok(DefaultIdentity));
        Customers.Setup(port => port.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(AxisError.NotFound("CUSTOMER_NOT_FOUND"));
        Customers.Setup(port => port.GetByIdAsync(It.IsAny<CustomerId>()))
            .ReturnsAsync((CustomerId id) => AxisResult.Ok<ICustomerEntityProperties>(
                new FakeCustomer(id, DefaultIdentity.Email!, DefaultIdentity.Name!, IsAdmin: false, DefaultIdentity.Subject, DefaultIdentity.Provider, EmailValidated: false)));
        Customers.Setup(port => port.CreateAsync(It.IsAny<ICustomerEntityProperties>())).ReturnsAsync(AxisResult.Ok());
        Customers.Setup(port => port.SetEmailValidatedAsync(It.IsAny<CustomerId>(), It.IsAny<bool>())).ReturnsAsync(AxisResult.Ok());
        ValidationCodes.Setup(port => port.SaveAsync(It.IsAny<CustomerId>(), It.IsAny<string>())).ReturnsAsync(AxisResult.Ok());
        ValidationCodes.Setup(port => port.GetAsync(It.IsAny<CustomerId>()))
            .ReturnsAsync(AxisError.NotFound("EMAIL_VALIDATION_CODE_NOT_FOUND"));
        ValidationCodes.Setup(port => port.RemoveAsync(It.IsAny<CustomerId>())).ReturnsAsync(AxisResult.Ok());
        Email.Setup(service => service.SendAsync(It.IsAny<AxisEmailData>())).ReturnsAsync(AxisResult.Ok());
        UnitOfWork.Setup(unit => unit.SaveChangesAsync()).ReturnsAsync(AxisResult.Ok());
        SagaMediator.Setup(saga => saga.StartAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(AxisResult.Ok("saga-0192aa00"));
    }

    protected TFacade Build<TFacade>() where TFacade : notnull
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddECommerceApplication()
            .AddECommerceFacade()
            .Configure<AuthTokenOptions>(_ => { })
            .PostConfigure<AuthTokenOptions>(_ => { })
            .AddSingleton(Options.Create(new AuthTokenOptions
            {
                SigningKey = "unit-test-signing-key-0123456789-abcdef",
                Issuer = "https://unit.test",
                Audience = "unit-tests",
                AccessTokenLifetimeSeconds = 300,
            }))
            .AddSingleton(ExternalIdentity.Object)
            .AddSingleton(Customers.Object)
            .AddSingleton(ValidationCodes.Object)
            .AddSingleton(Email.Object)
            .AddSingleton(UnitOfWork.Object)
            .AddSingleton(SagaMediator.Object)
            .BuildServiceProvider();

        provider.GetRequiredService<IAxisMediatorContextAccessor>().AxisEntityId = Identity;
        return provider.CreateScope().ServiceProvider.GetRequiredService<TFacade>();
    }
}
