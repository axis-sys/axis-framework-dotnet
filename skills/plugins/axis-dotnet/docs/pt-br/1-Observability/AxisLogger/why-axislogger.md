# Por que AxisLogger? · comparação

> Há outras maneiras de logar entradas estruturadas em .NET. Esta página diz por que o AxisLogger é diferente — uma comparação direta, sem mão na cintura.

---

## vs. `ILogger<T>` (direto)

`ILogger<T>` é o substrato sobre o qual o AxisLogger se senta. Chamá-lo direto tem três arestas ásperas:

1. **Enriquecimento manual.** Cada handler tem que lembrar de `BeginScope(new Dictionary { "TraceId", … })` — ou pior, nenhum scope.
2. **Sem `LogResult`.** Você escreve `if (result.IsSuccess) logger.LogInformation(...) else logger.LogError(...)` à mão em cada saída de pipeline.
3. **Sem integração com mediator.** Você não consegue derivar `OriginId`/`TraceId`/`JourneyId` sem injetar `IAxisMediator` em toda parte.

**AxisLogger** embrulha `ILogger<T>` com um scope de enriquecimento automático e o `LogResult` ciente da ferrovia. Os sinks ficam — Serilog, OpenTelemetry, Datadog, console — mas a **fiação** fica centralizada.

## vs. `Serilog` (direto)

Serilog tem sua própria interface `ILogger` com destructuring (`{@Order}`). API linda, mas você perde:

- A abstração de categoria `Microsoft.Extensions.Logging.ILogger<T>` (então filtros dirigidos por config ficam mais difíceis).
- Adapters entre stacks no ecossistema .NET (a maioria das packages NuGet logam em `ILogger<T>`).

**AxisLogger** mantém a abstração e te deixa plugar Serilog por baixo via `Microsoft.Extensions.Logging.Serilog`. Você ainda pode destrucutrar — passe um objeto como `("Order", order)` e o Serilog vai renderizar conforme sua config.

## vs. `LogContext.PushProperty` / enrichers do Serilog

`PushProperty` por chamada convida erros (esquecer de dispose, push duplo, race conditions). Enrichers são globais, o que é OK para campos estáticos como nome de máquina, mas errado para campos por requisição como `TraceId`. **AxisLogger** usa `BeginScope` uma vez por entrada — a ferramenta certa para enriquecimento por chamada.

## vs. um `IMyLogger` caseiro

DIY. Mesma forma, mas você também escreve o auto-enriquecimento, a escolha de nível em `LogResult`, os behaviours open-generic do pipeline e os testes. `IAxisLogger<T>` poupa o custo — e integra com o resto do Axis de graça.

---

## A comparação

| Característica | AxisLogger | `ILogger<T>` direto | Serilog direto | `IMyLogger` caseiro |
|---|:--:|:--:|:--:|:--:|
| Propriedades estruturadas via `params (Key, Value)[]` | **Sim** | Manual | Sim (destructuring) | Talvez |
| Auto-enriquecimento `TraceId`/`OriginId`/`JourneyId` | **Sim** | Não | Por contexto (manual) | Talvez |
| `LogResult` para desfechos de `AxisResult` | **Sim** | Não | Não | Talvez |
| Pipeline behaviour para request logging automático | **Sim** | Não | Não | Talvez |
| Funciona bem com qualquer sink `ILogger` | **Sim** | Sim | Parcial | Talvez |
| Pequeno — embrulha `ILogger<T>` | **Sim** | Sim | Não (pipeline próprio) | Talvez |
| Zero deps NuGet além de `Microsoft.Extensions.Logging.*` | **Sim** | Sim | Não | Talvez |

---

## Veja também

- [O contrato `IAxisLogger<T>`](iaxislogger.md) — a superfície
- [`LogResult`](log-result.md) — o operador que justifica a abstração
- [`LoggingBehavior`](logging-behavior.md) — pipeline behaviour opt-in

---

↩ [Voltar à documentação do AxisLogger](README.md)
