# Adapter Azure Monitor · `AxisTelemetry.AzureMonitor`

> O pacote de pareamento para Azure Monitor / Application Insights: registra o [adapter OpenTelemetry](opentelemetry-adapter.md) **e** liga o distro oficial `Azure.Monitor.OpenTelemetry.AspNetCore` (`UseAzureMonitor()`) em uma chamada — spans e métricas do Axis, instrumentação de ASP.NET Core / HttpClient e export de `ILogger`, tudo fluindo para o Application Insights.

```csharp
builder.Services.AddAzureMonitorAxis(builder.Configuration);
```

---

## Quando usar

Seu serviço roda no (ou reporta para o) Azure e você quer traces, métricas e logs no Application Insights sem montar o pipeline OpenTelemetry na mão. O `AxisTelemetry` deliberadamente não traz exporter — este pacote é exatamente esse pareamento que faltava.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| exportar para qualquer outro sink (collector OTLP, Jaeger, Prometheus) | [`AddOpenTelemetryAxis`](opentelemetry-adapter.md) + o exporter de sua escolha |
| rodar testes sem custo de instrumentação | [`NullAxisTelemetry`](null-adapter.md) |

---

## Instalação

```
dotnet add package AxisTelemetry.AzureMonitor
```

Depende de `AxisTelemetry` e `Azure.Monitor.OpenTelemetry.AspNetCore` (o distro oficial) — nada mais.

---

## O que `AddAzureMonitorAxis(configuration)` faz

1. Resolve a connection string, nesta ordem: `AzureMonitorAxisOptions.ConnectionString` → a chave `APPLICATIONINSIGHTS_CONNECTION_STRING` (a env var padrão do App Insights) → a entrada `ConnectionStrings:ApplicationInsights` (o idioma padrão .NET de `GetConnectionString` — sem criar seção extra no config dos clientes).
2. **Com** connection string: chama `AddOpenTelemetryAxis()` (o singleton `OpenTelemetryAdapter` atrás de `IAxisTelemetry` + `IAxisMetrics`) e depois `AddOpenTelemetry().UseAzureMonitor(...)`, assinando o `ActivitySource` e o `Meter` `"Axis.AxisMediator"` além da instrumentação de ASP.NET Core / HttpClient e do export de `ILogger` do distro.
3. **Sem** connection string: registra o [`NullAxisTelemetry`](null-adapter.md), loga um aviso no startup, não exporta nada — e **nunca lança**. Máquinas de dev, CI e testes E2E sobem o `Program` real sem App Insights por perto.

> **Host obrigatório para traces e logs.** O distro anexa os exporters de trace/log quando o host inicia os `IHostedService`s (ASP.NET Core e o Generic Host fazem isso por você). Um `BuildServiceProvider()` manual que nunca inicia hosted services exporta apenas métricas.

```csharp
// Program.cs
builder.Services
    .AddAxisMediator()
    .AddAzureMonitorAxis(builder.Configuration, o =>
    {
        o.ServiceName = "orders-api";
        o.ServiceVersion = "2.0.1";
        o.SamplingRatio = 0.25f;                                   // exporta 25% dos traces
        o.CategoryLogLevels["Microsoft.AspNetCore"] = LogLevel.Warning;
    });
```

---

## Custo — o Azure Monitor cobra por GB ingerido

O Application Insights cobra por volume de dados (**~US$ 2,30/GB** no tier pay-as-you-go padrão, com **5 GB/mês grátis por workspace do Log Analytics**). Dois botões controlam a conta:

- **`SamplingRatio`** — fração dos *traces* exportados (0.0–1.0). Uma API de alto tráfego em `0.1` mantém os traces distribuídos estatisticamente úteis a um décimo do custo. **Sampling não se aplica a logs.**
- **`TracesPerSecond`** — alternativa à fração: limita os traces exportados a uma taxa fixa (conta mais previsível sob tráfego irregular). Quando setado, `SamplingRatio` é ignorado. O distro cru usa rate-limited a 5 traces/s por default desde a 1.5; este pacote usa **fração 1.0 por default** (determinístico — nada é dropado silenciosamente) e deixa o rate limiter como opt-in explícito.
- **Opções de export de log** — logs costumam ser o maior driver de custo; veja abaixo.

### Export de logs — custo vs verbosidade, o cliente decide

Os filtros se aplicam **somente ao pipeline de export** (`OpenTelemetryLoggerProvider`): console e outros providers locais mantêm a verbosidade própria — só o que é ingerido (pago) é aparado.

| Opção | Default | Efeito |
|---|---|---|
| `EnableLogExport` | `true` | `false` → nenhuma entrada de `ILogger` chega ao Azure Monitor (traces/métricas continuam) |
| `MinimumLogLevel` | `Information` | piso global do que é exportado — `Warning` corta o custo drasticamente |
| `CategoryLogLevels` | vazio | overrides por categoria — silencie o ruído de `Microsoft.AspNetCore`, mantenha sua aplicação verbosa |
| `IncludeScopes` | `false` | scopes estruturados por entrada — mais contexto, mais bytes |
| `IncludeFormattedMessage` | `false` | mensagem renderizada além do template — mais legível, mais bytes |

```csharp
// Perfil custo mínimo
o.SamplingRatio = 0.1f;
o.MinimumLogLevel = LogLevel.Warning;
o.EnableLiveMetrics = false;

// Perfil diagnóstico verboso
o.SamplingRatio = 1.0f;
o.MinimumLogLevel = LogLevel.Debug;
o.IncludeScopes = true;
o.IncludeFormattedMessage = true;
```

---

## `AzureMonitorAxisOptions` — a superfície completa

| Opção | Default | Notas |
|---|---|---|
| `ConnectionString` | `null` | override programático das chaves de configuração |
| `SamplingRatio` | `1.0f` | fração dos traces exportados (ignorado quando `TracesPerSecond` está setado) |
| `TracesPerSecond` | `null` | teto de traces por taxa fixa, em vez de fração |
| `EnableLiveMetrics` | `true` | o stream de Live Metrics (grátis, mantém um canal aberto) |
| `ServiceName` / `ServiceVersion` | `null` | resource attributes `service.name` / `service.version` (cloud role name no portal) |
| `ResourceAttributes` | vazio | atributos extras carimbados em cada span, métrica e log |
| `EnableLogExport`, `MinimumLogLevel`, `CategoryLogLevels`, `IncludeScopes`, `IncludeFormattedMessage` | veja acima | os botões de custo/verbosidade de log |

---

## Relação com `AddOpenTelemetryAxis` e `NullAxisTelemetry`

`AddAzureMonitorAxis` **é** o `AddOpenTelemetryAxis` mais o exporter do Azure Monitor — o código de aplicação continua vendo apenas `IAxisTelemetry`/`IAxisMetrics`, então trocar o Azure Monitor por um collector OTLP (ou desligar a telemetria) é uma mudança de uma linha no DI, nunca no call site. O fallback sem connection string registra o mesmo [`NullAxisTelemetry`](null-adapter.md) que você registraria na mão em testes.

---

## Veja também

- [Adapter OpenTelemetry](opentelemetry-adapter.md) — a metade vendor-neutral que este pacote pareia com um exporter
- [Adapter null](null-adapter.md) — o fallback da degradação elegante
- [`TelemetryBehavior`](telemetry-behavior.md) — auto-instrumente cada request do mediator
- [Primeiros passos](getting-started.md) — o passo a passo da família

---

↩ [Voltar à documentação do AxisTelemetry](README.md)
