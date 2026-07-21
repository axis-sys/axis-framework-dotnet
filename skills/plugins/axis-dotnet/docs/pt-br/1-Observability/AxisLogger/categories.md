# Categorias e propriedades estruturadas

> O `T` em `IAxisLogger<T>` é a **categoria** para o `Microsoft.Extensions.Logging` — o mesmo papel que em `ILogger<T>`. Tag-eia cada entrada com o tipo da fonte, para que filtros e sinks possam rotear por classe. E é o que `LogResult` lê para preencher `RequestName`.

```csharp
public class CreatePersonHandler(IAxisLogger<CreatePersonHandler> logger)
{
    // logger escreve entradas sob a categoria "MyApp.People.CreatePersonHandler"
    // logger.LogResult preenche RequestName="MyApp.People.CreatePersonHandler"
}
```

---

## Quando usar

Sempre injete `IAxisLogger<MyClass>`. Escolha a classe **mais específica** como `T` — o handler, o behaviour, o adapter. Evite um `IAxisLogger<object>` genérico ou um base type compartilhado — você perde o roteamento por componente.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| compartilhar um logger entre dois tipos não relacionados | injete cada um separadamente com seu próprio `T` |
| logar de um método static | aceite `IAxisLogger<TAlgumaCoisa>` como parâmetro |
| logar de infra **fora** do escopo do mediator | `ILogger<T>` direto (sem enriquecimento de `TraceId`) |

---

## Os dois papéis do `T`

| Papel | Usado por | O que produz |
|---|---|---|
| Categoria do `Microsoft.Extensions.Logging` | toda chamada (`LogInformation`, `LogResult`, …) | o campo `Category` na entrada do log — ex.: `"MyApp.People.CreatePersonHandler"` |
| `RequestName` do `LogResult` | `LogResult(tag, result)` | propriedade estruturada `"RequestName" = typeof(T).FullName` |

A categoria dirige **filtros e sinks**: configure `appsettings.json` para suprimir `Debug` de um namespace, ou rotear `Error` de outro para o PagerDuty. O `RequestName` dirige **query e agregação**: conte falhas por handler, alerte num request específico etc.

---

## Propriedades sempre-ligadas

Além de seus pares `(Key, Value)`, cada entrada recebe:

| Propriedade | Fonte | Descrição |
|---|---|---|
| `UtcTime` | `TimeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss.fff zzz")` | timestamp formatado para busca em log |
| `OriginId` | `IAxisMediator.OriginId` | o sistema / canal upstream que começou a jornada |
| `TraceId` | `IAxisMediator.TraceId` | o id de correlação por requisição |
| `JourneyId` | `IAxisMediator.JourneyId` | o id da saga ou jornada longa (se houver) |

Suas propriedades **sobrescrevem** essas se compartilham uma chave — cuidado com nomes para não pisar nos padrões.

---

## Exemplo real — roteamento por handler

```csharp
public class CreatePersonHandler(IAxisLogger<CreatePersonHandler> logger) { /* … */ }
public class CreateOrderHandler (IAxisLogger<CreateOrderHandler>  logger) { /* … */ }
```

```jsonc
// appsettings.json — roteie via a categoria
{
  "Logging": {
    "LogLevel": {
      "MyApp.People.CreatePersonHandler": "Information",
      "MyApp.Orders.CreateOrderHandler":  "Debug"
    }
  }
}
```

**Por que compensa:** entradas debug barulhentas de um handler não inundam os outros. Regras de promoção, alertas e sampling podem ser por handler.

---

## Veja também

- [O contrato `IAxisLogger<T>`](iaxislogger.md) — para que a categoria é usada
- [`LogResult`](log-result.md) — por que o `T` aparece como `RequestName`
- [`LoggingBehavior`](logging-behavior.md) — usa `IAxisLogger<TRequest>`, então a categoria é o tipo do request

---

↩ [Voltar à documentação do AxisLogger](README.md)
