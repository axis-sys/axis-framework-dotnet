# Railway-Oriented Programming · o porquê

> Antes de aprender os operadores, entenda o problema que eles resolvem. Esta página mostra o "C# corporativo" típico, por que ele dói, e como o modelo de dois trilhos o desmonta.

---

## O problema

Isto é como se parece o "C# corporativo" na maioria das bases de código:

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
                // Engolir? Relançar? Quem decide?
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

**40 linhas. 5 blocos catch. 3 verificações de null. 2 try/catch aninhados.** E a intenção de negócio real fica soterrada sob cerimônia defensiva.

Exceções são **`goto` disfarçados**. Elas quebram a pilha de chamadas, são invisíveis nas assinaturas de método, forçam você a adivinhar o que pode falhar, e tornam cada chamador responsável por capturar coisas que nem são excepcionais. "Customer not found" não é uma exceção — é um desfecho perfeitamente normal.

Agora a mesma lógica com AxisResult:

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

**5 linhas. Zero try/catch. Zero verificações de null. Zero exceções.** Toda falha possível está codificada no tipo de retorno. O pipeline se lê como uma frase descrevendo o que a operação faz.

Isso é Railway-Oriented Programming.

---

## O que é Railway-Oriented Programming?

Imagine seu código como uma ferrovia de dois trilhos:

```
Sucesso ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━▶  Resultado
              ┃              ┃              ┃              ┃
           Validar        GetUser      CreateOrder      Salvar
              ┃              ┃              ┃              ┃
Falha   ━━━━━━╋━━━━━━━━━━━━━━╋━━━━━━━━━━━━━━╋━━━━━━━━━━━━━━╋━▶  Erros
```

No **trilho de sucesso**, os dados fluem de uma operação para a próxima. Cada operação transforma ou valida o dado e o passa adiante.

No **trilho de falha**, os erros propagam automaticamente. No momento em que qualquer operação falha, todas as operações seguintes são **puladas** — sem o boilerplate `if (result.IsFailure) return result;`.

A mágica está nas **chaves de desvio** (os pontos `┃`). Operações como `ThenAsync`, `MapAsync` e `EnsureAsync` são chaves ferroviárias: só executam quando se está no trilho de sucesso. Se você já está no trilho de falha, elas deixam você passar intocado.

Não é uma ideia nova — vem da programação funcional (o `Either` de Haskell, o `Result` de F#, o `Result<T, E>` de Rust). Mas o AxisResult é a primeira library C# que a implementa **por completo**, com suporte async total, zero dependências, e APIs desenhadas para como devs C# realmente escrevem código.

---

## Veja também

- [Primeiros passos](getting-started.md) — instalar e escrever o primeiro pipeline
- [Encadear · `Then`](then.md) — a chave de desvio mais importante
- [Por que AxisResult?](why-axisresult.md) — comparação com outras libraries de Result

---

↩ [Voltar à documentação do AxisResult](README.md)
