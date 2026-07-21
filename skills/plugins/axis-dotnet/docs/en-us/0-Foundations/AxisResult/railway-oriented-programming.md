# Railway-Oriented Programming · the why

> Before learning the operators, understand the problem they solve. This page shows the typical "enterprise C#", why it hurts, and how the two-rail model dismantles it.

---

## The problem

This is what "enterprise C#" looks like in most codebases:

```csharp
public async Task<ApiResponse> Handle(CreateOrderCommand cmd)
{
    try
    {
        var customer = await _customerRepo.GetByIdAsync(cmd.CustomerId);
        if (customer == null)
            throw new NotFoundException("Customer not found");

        if (!customer.IsActive)
            throw new BusinessRuleException("Customer is not active");

        var existingOrder = await _orderRepo.GetByReferenceAsync(cmd.Reference);
        if (existingOrder != null)
            throw new ConflictException("Order already exists");

        var product = await _productRepo.GetByIdAsync(cmd.ProductId);
        if (product == null)
            throw new NotFoundException("Product not found");

        if (product.Stock < cmd.Quantity)
            throw new BusinessRuleException("Insufficient stock");

        try
        {
            var order = new Order(customer.Id, product.Id, cmd.Quantity);
            await _orderRepo.CreateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            try
            {
                await _eventBus.PublishAsync(new OrderCreatedEvent(order.Id));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish event");
                // Swallow? Rethrow? Who decides?
            }

            return new ApiResponse { OrderId = order.Id };
        }
        catch (DbUpdateException ex)
        {
            throw new ConflictException("Duplicate order", ex);
        }
    }
    catch (NotFoundException ex) { return NotFound(ex.Message); }
    catch (BusinessRuleException ex) { return BadRequest(ex.Message); }
    catch (ConflictException ex) { return Conflict(ex.Message); }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error");
        return InternalServerError();
    }
}
```

**40 lines. 5 catch blocks. 3 null checks. 2 nested try/catch.** And the actual business intent is buried under defensive ceremony.

Exceptions are **goto statements in disguise**. They break your call stack, they're invisible in method signatures, they force you to guess what might fail, and they make every caller responsible for catching things that aren't exceptional at all. "Customer not found" isn't an exception — it's a perfectly normal outcome.

Now the same logic with AxisResult:

```csharp
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    => customerFactory.GetByIdAsync(cmd.CustomerId)
        .ThenAsync(customer => orderFactory.CreateAsync(new()
        {
            CustomerId = customer.CustomerId,
            ProductId = cmd.ProductId,
            Quantity = cmd.Quantity
        }))
        .ThenAsync(_ => unitOfWork.SaveChangesAsync())
        .TapAsync(order => logger.LogInformation("Order {OrderId} created", order.OrderId))
        .MapAsync(order => new CreateOrderResponse { OrderId = order.OrderId });
```

**5 lines. Zero try/catch. Zero null checks. Zero exceptions.** Every possible failure is encoded in the return type. The pipeline reads like a sentence describing what the operation does.

That's Railway-Oriented Programming.

---

## What is Railway-Oriented Programming?

Imagine your code as a railway track with two rails:

```
Success ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━▶  Result
              ┃              ┃              ┃              ┃
           Validate       GetUser      CreateOrder      Save
              ┃              ┃              ┃              ┃
Failure ━━━━━━╋━━━━━━━━━━━━━━╋━━━━━━━━━━━━━━╋━━━━━━━━━━━━━━╋━▶  Errors
```

On the **success rail**, data flows from one operation to the next. Each operation transforms or validates the data, then passes it forward.

On the **failure rail**, errors propagate automatically. The moment any operation fails, all subsequent operations are **skipped** — no `if (result.IsFailure) return result;` boilerplate.

The magic is in the **switches** (the `┃` points). Operations like `ThenAsync`, `MapAsync`, and `EnsureAsync` are railway switches: they only execute when on the success rail. If you're already on the failure rail, they let you pass through untouched.

This isn't a new idea — it comes from functional programming (Haskell's `Either`, F#'s `Result`, Rust's `Result<T, E>`). But AxisResult is the first C# library that implements it **completely**, with full async support, zero dependencies, and APIs designed for how C# developers actually write code.

---

## See also

- [Getting started](getting-started.md) — install and write your first pipeline
- [Chain · `Then`](then.md) — the most important switch
- [Why AxisResult?](why-axisresult.md) — comparison with other Result libraries

---

↩ [Back to AxisResult docs](README.md)
