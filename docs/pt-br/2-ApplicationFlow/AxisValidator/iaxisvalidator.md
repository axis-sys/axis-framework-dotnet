# Contrato · `IAxisValidator<T>`

> Dois métodos. Ambos retornam `AxisResult` — `Ok()` quando a instância é válida, `Error(errors)` onde cada erro é um `AxisError.ValidationRule(code)` tipado.

```csharp
public interface IAxisValidator<in T>
{
    AxisResult       Validate(T instance);
    Task<AxisResult> ValidateAsync(T instance);
}
```

---

## Quando usar

Em qualquer lugar onde você escreveria `IValidator<T>` do FluentValidation, escreva `IAxisValidator<T>`. Na verdade, você quase nunca escreve à mão — você herda `AxisValidatorBase<T>` e o framework pluga o adapter para você.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| validar **invariantes de negócio** que dependem de dados fora do request | a entidade de domínio (retorne `AxisError.BusinessRule`) |
| validar **no parse** (model binding HTTP) | um `TryParse` num tipo `[ValueObject]` de [`AxisTypes`](../../0-Foundations/AxisTypes/README.md) |
| validar **antes da persistência** com queries no banco | a camada de aplicação com `Then(...)` + um repositório |

---

## Como o adapter embarcado funciona

Lendo `FluentValidatorAdapter<T>` direto:

1. Resolve `IValidator<T>` do `IServiceProvider`. Se nenhum estiver registrado, retorna `AxisResult.Ok()` — *sem validator = nada para validar*.
2. Roda `validator.Validate(instance)` (ou `ValidateAsync` com `mediator.CancellationToken`).
3. Em `IsValid`, retorna `Ok()`.
4. Em falha, projeta cada `ValidationFailure.ErrorCode` em `AxisError.ValidationRule(code)` e retorna `AxisResult.Error(errors)`.

O adapter joga fora o `ValidationFailure.ErrorMessage`. **O contrato é o `Code`.** Renderize mensagens na borda de apresentação via um resolver `code → message`.

---

## Exemplos reais

### 1. Chame um validator manualmente

```csharp
var result = await validator.ValidateAsync(cmd);
if (result.IsFailure)
    return result.Errors.ToArray();    // conversão implícita → AxisResult<TResponse>.Error
```

**Por que compensa:** num fluxo orquestrado à mão (um console app, um caminho de código não-mediator), você ainda recebe erros tipados na trilha — sem `ValidationException` para capturar.

### 2. Validar dentro da ferrovia

```csharp
return await ParseInputAsync(raw)
    .ThenAsync(cmd => validator.ValidateAsync(cmd).Map(_ => cmd))
    .ThenAsync(cmd => factory.CreateAsync(cmd))
    .MapAsync(_ => new CreatePersonResponse { ... });
```

**Por que compensa:** a cadeia parse-depois-valida-depois-cria lê como uma frase. Se a validação falha, o resto é pulado, sem exceção.

### 3. Sem validator? `Ok()` automaticamente

```csharp
// nenhum CreatePersonValidator registrado
var result = await validator.ValidateAsync(cmd);   // Ok() — nada para checar
```

O comportamento padrão é deliberado: registrar um validator é **opt-in**. O handler continua rodando.

---

## Veja também

- [Base e regras do validador](validator-base.md) — o que você realmente escreve
- [`ValidationBehavior`](validation-behavior.md) — chame este método automaticamente antes de cada handler
- [Referência da API](api-reference.md) — cada membro, num só lugar

---

↩ [Voltar à documentação do AxisValidator](README.md)
