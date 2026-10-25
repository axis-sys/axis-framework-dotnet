using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scaffolds.ECommerce.Host.Auth;

namespace Scaffolds.ECommerce.E2ETests;

public sealed class AuthenticationCompositionTests
{
    private const string SigningKey = "ECommerce:Auth:Token:SigningKey";
    private const string MsEntraClientId = "ECommerce:Auth:MsEntra:ClientId";
    private const string MsEntraTenantId = "ECommerce:Auth:MsEntra:TenantId";
    private const string GoogleClientId = "ECommerce:Auth:Google:ClientId";

    [Fact]
    public void StartsWhenEverySchemeIsConfigured()
    {
        var validator = StartupValidator(ConfiguredSettings());

        var exception = Record.Exception(validator.Validate);

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(SigningKey)]
    [InlineData(MsEntraClientId)]
    [InlineData(MsEntraTenantId)]
    [InlineData(GoogleClientId)]
    public void RefusesToStartWhenARequiredSettingIsMissing(string requiredSetting)
    {
        var settings = ConfiguredSettings();
        settings.Remove(requiredSetting);
        var validator = StartupValidator(settings);

        var start = validator.Validate;

        Assert.Throws<OptionsValidationException>(start);
    }

    [Fact]
    public void RefusesToStartWhenTheSigningKeyIsShorterThan32Characters()
    {
        var settings = ConfiguredSettings();
        settings[SigningKey] = "too-short-for-hmac-sha256";
        var validator = StartupValidator(settings);

        var start = validator.Validate;

        Assert.Throws<OptionsValidationException>(start);
    }

    private static Dictionary<string, string?> ConfiguredSettings() => new()
    {
        [SigningKey] = "composition-test-signing-key-0123456789",
        ["ECommerce:Auth:Token:Issuer"] = "https://scaffolds-ecommerce.test",
        ["ECommerce:Auth:Token:Audience"] = "scaffolds-ecommerce-composition",
        ["ECommerce:Auth:Token:AccessTokenLifetimeSeconds"] = "300",
        [MsEntraTenantId] = "00000000-0000-0000-0000-000000000000",
        [MsEntraClientId] = "00000000-0000-0000-0000-000000000000",
        [GoogleClientId] = "000000000000-composition.apps.googleusercontent.com",
    };

    private static IStartupValidator StartupValidator(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new ServiceCollection()
            .AddECommerceAuthentication(configuration)
            .BuildServiceProvider()
            .GetRequiredService<IStartupValidator>();
    }
}
