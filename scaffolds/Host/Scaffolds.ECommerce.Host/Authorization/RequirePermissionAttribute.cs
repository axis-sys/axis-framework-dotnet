namespace Scaffolds.ECommerce.Host.Authorization;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class RequirePermissionAttribute(string code) : Attribute
{
    public string Code { get; } = code;

    public bool ConcealsSagaRun { get; set; }
}
