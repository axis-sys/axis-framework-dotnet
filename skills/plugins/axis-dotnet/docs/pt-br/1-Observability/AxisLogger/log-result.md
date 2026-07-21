# `LogResult` — desfechos estruturados

> Loga um desfecho de `AxisResult` em uma linha. O nível é escolhido por você — `Information` no sucesso, `Error` na falha — e a entrada carrega `Tag`, `RequestName` e (na falha) a `AxisErrorList` inteira como propriedades estruturadas.

```csharp
logger.LogResult("CreatePerson", result);
// Information "CreatePerson Handled CreatePersonHandler" + Tag/RequestName       (no sucesso)
// Error       "CreatePerson Handled CreatePersonHandler" + Tag/RequestName/...   (na falha)
```

---

## Quando usar

No **fim de um pipeline**, para registrar o que aconteceu. Pareie com `TapAsync` para que a ferrovia continue fluindo:

```csharp
return factory.CreateAsync(cmd)
    .ThenAsync(person => uow.SaveChangesAsync())
    .TapAsync(r => logger.LogResult("CreatePerson", r))
    .MapAsync(_ => new CreatePersonResponse { … });
```

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| logar o **início** de uma requisição automaticamente | [`LoggingBehavior`](logging-behavior.md) |
| logar um valor que não é `AxisResult` | `LogInformation` / `LogError` |
| logar o valor que a ferrovia carregou | `Tap` com `LogInformation` manual |

---

## O que vai parar na entrada

Lendo `AxisLogger<T>.LogResult` direto:

| Campo | Sucesso | Falha |
|---|---|---|
| `LogLevel` | `Information` | `Error` |
| Message | `$"{tag} Handled {typeof(T).Name}"` | igual |
| Propriedade `Tag` | a `tag` que você passou | igual |
| Propriedade `RequestName` | `typeof(T).FullName` | igual |
| Propriedade `AxisErrorList` | — | `result.Errors` (a lista completa como array estruturado) |

Mais o enriquecimento sempre-ligado de `BuildScope`: `UtcTime`, `OriginId`, `TraceId`, `JourneyId`.

> O `T` em `IAxisLogger<T>` é o que determina `RequestName` — é por isso que você injeta `IAxisLogger<CreatePersonHandler>` (ou o tipo do seu handler / behaviour / adapter) no lugar de um logger não-genérico.

---

## Exemplos reais

### 1. Fim de um pipeline de comando

```csharp
public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
    => factory.CreateAsync(cmd)
        .ThenAsync(person => uow.SaveChangesAsync())
        .TapAsync(r => logger.LogResult("CreatePerson", r))
        .MapAsync(_ => new CreatePersonResponse { PersonId = cmd.PersonId });
```

**Por que compensa:** a entrada de sucesso inclui `Tag="CreatePerson"` e `RequestName="CreatePersonHandler"`; se o pipeline falha, a mesma entrada sai em `Error` com a `AxisErrorList` inteira anexada. A query "quantos comandos CreatePerson falharam na última hora" vira uma agregação no sink.

### 2. Desfecho por passo dentro de uma saga

```csharp
return await reserveStock.ExecuteAsync(cmd)
    .TapAsync(r => logger.LogResult("ReserveStock", r))
    .ThenAsync(_ => chargeCard.ExecuteAsync(cmd))
    .TapAsync(r => logger.LogResult("ChargeCard",  r))
    .ThenAsync(_ => createShipment.ExecuteAsync(cmd))
    .TapAsync(r => logger.LogResult("CreateShipment", r));
```

**Por que compensa:** cada passo produz uma única linha estruturada; a linha do tempo do trace lê como o storyboard da saga, e uma falha em qualquer passo acende em `Error` com sua `AxisErrorList`.

### 3. Logando uma query que não falha de fato

```csharp
return await reader.GetByIdAsync(id)
    .TapAsync(r => logger.LogResult("GetPerson", r))
    .MapAsync(person => new GetPersonResponse { … });
```

**Por que compensa:** mesmo um `NotFound` **não** é uma exceção — é um erro na trilha. `LogResult` o registra em `Error` com a lista de erros tipada, que é a severidade certa para "pedimos um id que não existe".

---

## Veja também

- [O contrato `IAxisLogger<T>`](iaxislogger.md) — cada overload
- [`LoggingBehavior`](logging-behavior.md) — request logging automático
- [Categorias](categories.md) — por que o `T` importa

---

↩ [Voltar à documentação do AxisLogger](README.md)
