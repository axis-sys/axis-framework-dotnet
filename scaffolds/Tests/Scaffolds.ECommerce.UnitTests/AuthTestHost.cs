using Axis.Ports;
using Microsoft.Extensions.Options;
using Scaffolds.ECommerce.Adapters.Driving.Facade;
using Scaffolds.ECommerce.Application;
using Scaffolds.ECommerce.Application.Auth;
using Scaffolds.ECommerce.Application.Authorization.Services.GrantsCache;
using Scaffolds.ECommerce.Contracts.Driven.Authorization.Members;
using Scaffolds.ECommerce.Domain.Authorization.Members;
using Scaffolds.ECommerce.SharedKernel.Auth;
using Scaffolds.ECommerce.SharedKernel.Authorization.Events.v1;

namespace Scaffolds.ECommerce.UnitTests;

public abstract class AuthTestHost
{
    protected static readonly ExternalUser DefaultIdentity = new("0192aa00-0000-7000-8000-0000000000c1", "ada@example.com", "Ada", AuthSchemes.MsEntra);

    protected Mock<IExternalIdentityContext> ExternalIdentity { get; } = new(MockBehavior.Loose);
    protected Mock<IAccessTokenSignerPort> AccessTokenSigner { get; } = new(MockBehavior.Loose);
    protected Mock<ICustomersReaderPort> CustomersReader { get; } = new(MockBehavior.Loose);
    protected Mock<ICustomersWritePort> CustomersWrite { get; } = new(MockBehavior.Loose);
    protected Mock<IValidationCodesReaderPort> ValidationCodesReader { get; } = new(MockBehavior.Loose);
    protected Mock<IValidationCodesWritePort> ValidationCodesWrite { get; } = new(MockBehavior.Loose);
    protected Mock<IMembersReaderPort> MembersReader { get; } = new(MockBehavior.Loose);
    protected Mock<IMembersWritePort> MembersWrite { get; } = new(MockBehavior.Loose);
    protected Mock<IAxisCache> Cache { get; } = new(MockBehavior.Loose);
    protected Mock<IAxisBus> Bus { get; } = new(MockBehavior.Loose);
    protected Mock<IAxisEmailService> Email { get; } = new(MockBehavior.Loose);
    protected Mock<IUnitOfWork> UnitOfWork { get; } = new(MockBehavior.Loose);
    protected Mock<IAxisSagaMediator> SagaMediator { get; } = new(MockBehavior.Loose);
    protected AxisEntityId? Identity { get; set; } = AxisEntityId.New;

    protected AuthTestHost()
    {
        ExternalIdentity.Setup(context => context.Get()).Returns(AxisResult.Ok(DefaultIdentity));
        AccessTokenSigner.Setup(signer => signer.Sign(It.IsAny<AccessTokenDescriptor>())).Returns(AxisResult.Ok("unit-test-access-token"));
        CustomersReader.Setup(port => port.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(CustomerErrors.CustomerNotFound);
        CustomersReader.Setup(port => port.GetByIdAsync(It.IsAny<CustomerId>()))
            .ReturnsAsync((CustomerId id) => AxisResult.Ok<ICustomerEntityProperties>(
                new FakeCustomer(id, DefaultIdentity.Email!, DefaultIdentity.Name!, IsAdmin: false, DefaultIdentity.Subject, DefaultIdentity.Provider, EmailValidated: false)));
        CustomersWrite.Setup(port => port.CreateAsync(It.IsAny<ICustomerEntityProperties>())).ReturnsAsync(AxisResult.Ok());
        CustomersWrite.Setup(port => port.SetEmailValidatedAsync(It.IsAny<CustomerId>(), It.IsAny<bool>())).ReturnsAsync(AxisResult.Ok());
        ValidationCodesReader.Setup(port => port.GetByCustomerIdAsync(It.IsAny<CustomerId>()))
            .ReturnsAsync(ValidationCodeErrors.ValidationCodeNotFound);
        ValidationCodesWrite.Setup(port => port.CreateAsync(It.IsAny<IValidationCodeEntityProperties>()))
            .ReturnsAsync((IValidationCodeEntityProperties validationCode) => AxisResult.Ok(validationCode));
        ValidationCodesWrite.Setup(port => port.UpdateAsync(It.IsAny<IValidationCodeEntityProperties>()))
            .ReturnsAsync((IValidationCodeEntityProperties validationCode) => AxisResult.Ok(validationCode));
        ValidationCodesWrite.Setup(port => port.RemoveAsync(It.IsAny<CustomerId>())).ReturnsAsync(AxisResult.Ok());
        MembersReader.Setup(port => port.GetByCustomerIdAsync(It.IsAny<CustomerId>()))
            .ReturnsAsync(MemberErrors.MemberNotFound);
        MembersWrite.Setup(port => port.CreateAsync(It.IsAny<IMemberEntityProperties>()))
            .ReturnsAsync((IMemberEntityProperties member) => AxisResult.Ok(member));
        MembersWrite.Setup(port => port.UpdateAsync(It.IsAny<IMemberEntityProperties>()))
            .ReturnsAsync((IMemberEntityProperties member) => AxisResult.Ok(member));
        Cache.Setup(cache => cache.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<Func<Task<AxisResult<Grants>>>>(), It.IsAny<TimeSpan?>()))
            .Returns((string _, Func<Task<AxisResult<Grants>>> compose, TimeSpan? _) => compose());
        Cache.Setup(cache => cache.RemoveAsync(It.IsAny<string>())).ReturnsAsync(AxisResult.Ok());
        Bus.Setup(bus => bus.PublishAsync(It.IsAny<GrantsUpdatedEvent>(), It.IsAny<string[]>())).ReturnsAsync(AxisResult.Ok());
        Email.Setup(service => service.SendAsync(It.IsAny<AxisEmailData>())).ReturnsAsync(AxisResult.Ok());
        UnitOfWork.Setup(unit => unit.SaveChangesAsync()).ReturnsAsync(AxisResult.Ok());
        UnitOfWork.Setup(unit => unit.RenewAsync()).ReturnsAsync(AxisResult.Ok());
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
            .AddSingleton(AccessTokenSigner.Object)
            .AddSingleton(CustomersReader.Object)
            .AddSingleton(CustomersWrite.Object)
            .AddSingleton(ValidationCodesReader.Object)
            .AddSingleton(ValidationCodesWrite.Object)
            .AddSingleton(MembersReader.Object)
            .AddSingleton(MembersWrite.Object)
            .AddSingleton(Cache.Object)
            .AddSingleton(Bus.Object)
            .AddSingleton(Email.Object)
            .AddSingleton(UnitOfWork.Object)
            .AddSingleton(SagaMediator.Object)
            .BuildServiceProvider();

        provider.GetRequiredService<IAxisMediatorContextAccessor>().AxisEntityId = Identity;
        return provider.CreateScope().ServiceProvider.GetRequiredService<TFacade>();
    }
}
