namespace Scaffolds.ECommerce.SharedKernel.Shared;

public static class SharedErrors
{
    public static readonly AxisError CustomerIdentityRequired = AxisError.Unauthorized("CUSTOMER_IDENTITY_REQUIRED");

    public const string NameRequired = "NAME_REQUIRED";

    public const string CustomerIdInvalid = "CUSTOMER_ID_INVALID";

    public const string CartIdInvalid = "CART_ID_INVALID";
    public const string QuantityMustBePositive = "QUANTITY_MUST_BE_POSITIVE";

    public static readonly AxisError SagaRunNotFound = AxisError.NotFound("SAGA_RUN_NOT_FOUND");
    public static readonly AxisError SagaRunNotCancellable = AxisError.Conflict("SAGA_RUN_NOT_CANCELLABLE");
    public static readonly AxisError SagaRunStoreFailed = AxisError.InternalServerError("SAGA_RUN_STORE_FAILED");
    public static readonly AxisError SagaRunFailed = AxisError.InternalServerError("SAGA_RUN_FAILED");
}
