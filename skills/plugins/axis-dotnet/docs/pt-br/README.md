# Axis Framework

> 🌐 [English (README principal)](../../README.md)

**Foque no domínio. A gente cuida do resto.** — um framework .NET opinativo, de ports e adapters, onde o core permanece puro, toda falha é um valor tipado, e um monólito vira microserviços movendo uma pasta. Blocos de construção tipados, orquestração Railway-Oriented, adaptadores de infraestrutura intercambiáveis, observabilidade e a borda HTTP — tudo se compondo sem cerimônia.

```csharp
// Uma feature inteira. É só isto que você escreve — o framework faz o resto.
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    => customerFactory.GetByIdAsync(cmd.CustomerId)
        .ThenAsync(customer => orderFactory.CreateAsync(new()
        {
            CustomerId = customer.CustomerId,
            ProductId  = cmd.ProductId,
            Quantity   = cmd.Quantity
        }))
        .ThenAsync(order => bus.PublishAsync(new OrderCreatedEvent(order.OrderId))) // enfileira o fato "OrderCreated" no outbox…
        .ThenAsync(order => unitOfWork.SaveChangesAsync())                          // …e então um único commit persiste o pedido E o evento, atomicamente
        .TapAsync(order => logger.LogInformation("Order {OrderId} created", order.OrderId))
        .MapAsync(order => new CreateOrderResponse { OrderId = order.OrderId });

// Validação, wiring de DI, mapeamento erro→HTTP, tracing e métricas são trabalho do
// framework — não seu, e não neste arquivo. O evento é enfileirado no bus (um adapter
// de outbox) e o único SaveChangesAsync o commita junto com o pedido numa só transação:
// publica primeiro, commita uma vez — nunca um dual-write commit-depois-publish.
```

Use esta página como um **mapa**: leia o tronco abaixo (~5 min) para entender *por que o Axis existe*, e então pule para a família ou package que você precisa — sem ler centenas de linhas.

---

## O tronco (leia primeiro)

### Por que Axis? Três promessas

**1 · Você escreve o domínio. O Axis cuida da arquitetura.**
Uma feature é um **vertical slice** através de uma fronteira **hexagonal** — um caso de uso CQRS (`Command`/`Query` → `Response` → `Handler` → `Validator`) exposto por uma **Facade**. Você escreve o handler; o framework fornece o pipeline, o wiring de dependências, a fronteira transacional, o portão de validação, o mapeamento de erro para HTTP e a telemetria. As questões arquiteturais são respondidas uma vez, pelo framework, e impostas para todos.

**2 · Ports e adapters — um core puro que nunca depende de um fornecedor.**
Cada peça de infraestrutura (persistência, mensageria, cache, object storage, email) é um pequeno **port** que retorna `AxisResult` e nunca lança exceção. O adapter é dono do `try/catch` e do SDK; a aplicação enxerga apenas a interface. Migrar de Postgres → SQL Server, ou de RabbitMQ → Kafka, é uma **mudança de DI no composition root**.

**3 · Monolithic-first — vire microserviços movendo uma pasta.**
Comece com **um único deployable** hospedando API, workers e jobs. Dentro dele, o código é fatiado verticalmente por **bounded context**, e como o namespace espelha exatamente o caminho da pasta, promover um bounded context ao seu próprio microserviço é um **movimento estrutural, não uma reescrita** — as pastas do BC viram projetos conforme as camadas, todos os namespaces permanecem idênticos, e o banco de dados pode ser isolado nos mesmos termos (Já é separado via schema e já são isolados por natureza). O tráfego entre BCs já flui pelos três canais autorizados, então nada quebra quando a fronteira vira um salto de rede.

### Os três canais autorizados entre BCs

Um bounded context nunca alcança os ports ou internos de outro BC. Tudo cruza a fronteira por exatamente um destes canais:

| Necessidade | Canal |
|---|---|
| Leitura/escrita síncrona | a **Facade** pública do outro BC |
| Efeito colateral fire-and-forget | um evento no **Bus** |
| Processo multi-BC com compensação | uma **Saga** cujos stage handlers vivem no BC dono |

Como esses são os *mesmos* canais quer os BCs compartilhem um processo ou fiquem em pontas opostas de uma rede, a promoção de monólito → microserviços muda o deployment, não o design.

### O que vem na caixa

Este é **um único repositório** que entrega tudo o que é preciso para construir do jeito Axis:

| | |
|---|---|
| 📦 **Packages** (`src/`) | As bibliotecas NuGet, organizadas em cinco famílias. Tem como alvo o **.NET 10**. |
| 🛡️ **Analyzers & generators** | Analyzers Roslyn e um source generator impõem as invariantes em tempo de build — uma regra violada quebra o build, não a revisão de código. |
| 📐 **Rules** (`rules/`) | **Cerca de 400 regras canônicas** (conventions + framework) — a fonte única da verdade da qual todo analyzer e doc deriva. |
| 🏗️ **Scaffolds** (`src/scaffolds/`) | Uma aplicação de referência de ECommerce completa — domínio, facades, pipelines, ports, casos de uso e testes. |
| 📚 **Docs** (`docs/`) | Documentação navegável em **inglês e português**, além dos registros de decisão de arquitetura (ADRs). |
| 🤖 **Agentes do Claude Code** (`.agents/claude/`) | Três subagentes mais um slash command que automatizam o fluxo Axis — revisão, planejamento e migração de um repositório existente rumo ao padrão. |

### Uma fonte da verdade

A base de regras em `rules/` é canônica: os analyzers tornam as regras mecânicas **inegociáveis em tempo de build**, enquanto as docs carregam as de julgamento. Um humano lendo uma doc e um build quebrando por uma regra violada estão impondo exatamente a mesma invariante.

### Instalação

```
dotnet add package AxisResult
```

Toda package é independente — adicione apenas o que um slice precisa. → Comece com **[`AxisResult`](0-Foundations/AxisResult/README.md)**, a ferrovia que todo o resto retorna.

---

## O mapa — as cinco famílias

Inspiradas na taxonomia GoF, as packages estão organizadas em **cinco famílias** por responsabilidade, espelhadas um-para-um pelas pastas numeradas de nível superior. Conhecer a família de uma package já diz aproximadamente *que tipo de problema ela resolve* e *onde ela se encaixa na sua stack*.

### 0 · Foundations — blocos de construção tipados

Zero infraestrutura, puros tipos e abstrações. **Comece por aqui.**

- [`AxisResult`](0-Foundations/AxisResult/README.md) — Railway-Oriented Programming, a monad `Result` tipada com suporte async
- [`AxisTypes`](0-Foundations/AxisTypes/README.md) — value objects fortemente tipados (`AxisEntityId`) + source generator `[ValueObject]`
- [`AxisMediator.Contracts`](0-Foundations/AxisMediator.Contracts/README.md) — contratos CQRS puros compartilhados pelo mediator e seus consumidores

### 1 · Observability — o que seus operadores observam

- [`AxisLogger`](1-Observability/AxisLogger/README.md) — logging estruturado, correlação, enrichers
- [`AxisTelemetry`](1-Observability/AxisTelemetry/README.md) — OpenTelemetry: traces / metrics / logs, com adapter para Azure Monitor / Application Insights

### 2 · Application & Flow — os verbos do seu domínio

- [`AxisMediator`](2-ApplicationFlow/AxisMediator/README.md) — mediator request/response, pipelines, behaviours
- [`AxisSaga`](2-ApplicationFlow/AxisSaga/README.md) — orquestração de processos longos com compensações
- [`AxisValidator`](2-ApplicationFlow/AxisValidator/README.md) — validação declarativa que retorna `AxisResult`
- [`AxisBus`](2-ApplicationFlow/AxisBus/README.md) — abstração de event bus: fan-out assíncrono de eventos para handlers

### 3 · Infrastructure & Integration — adapters, sempre por trás de um port

A aplicação nunca depende de um fornecedor.

- [`AxisRepository`](3-Infra/AxisRepository/README.md) — portas de persistência, unit of work, adapters Postgres/MySQL (Npgsql/MySqlConnector puros, sem ORM)
- [`AxisCache`](3-Infra/AxisCache/README.md) — abstração de cache com adapter in-memory
- [`AxisStorage`](3-Infra/AxisStorage/README.md) — abstração de blob/arquivos (Cloudflare R2 / compatível com S3)
- [`AxisEmail`](3-Infra/AxisEmail/README.md) — envio de email transacional (MimeKit / SMTP)

### 4 · Edge — a porta onde seus clientes batem

- [`AxisResult.HttpResponse`](4-Edge/AxisResult.HttpResponse/README.md) — mapeia `AxisError` → `IActionResult` / `ProblemDetails` na fronteira HTTP

---

## Packages num piscar de olhos

| Package | Família | Resumo em uma linha | Documentação |
|---|---|---|---|
| [`AxisResult`](0-Foundations/AxisResult/README.md) | Foundations | Monad `Result` Railway-Oriented com `async`/`ValueTask` e erros tipados | [docs](0-Foundations/AxisResult/README.md) |
| [`AxisTypes`](0-Foundations/AxisTypes/README.md) | Foundations | Value objects fortemente tipados + source generator `[ValueObject]` | [docs](0-Foundations/AxisTypes/README.md) |
| [`AxisMediator.Contracts`](0-Foundations/AxisMediator.Contracts/README.md) | Foundations | Contratos CQRS puros compartilhados pelo mediator e seus consumidores | [docs](0-Foundations/AxisMediator.Contracts/README.md) |
| [`AxisLogger`](1-Observability/AxisLogger/README.md) | Observability | Logging estruturado com enriquecimento | [docs](1-Observability/AxisLogger/README.md) |
| [`AxisTelemetry`](1-Observability/AxisTelemetry/README.md) | Observability | Integração OpenTelemetry, adapter Azure Monitor | [docs](1-Observability/AxisTelemetry/README.md) |
| [`AxisMediator`](2-ApplicationFlow/AxisMediator/README.md) | Application & Flow | Mediator in-process request/response com pipelines | [docs](2-ApplicationFlow/AxisMediator/README.md) |
| [`AxisSaga`](2-ApplicationFlow/AxisSaga/README.md) | Application & Flow | Orquestração de sagas com compensações e estado | [docs](2-ApplicationFlow/AxisSaga/README.md) |
| [`AxisValidator`](2-ApplicationFlow/AxisValidator/README.md) | Application & Flow | Validação declarativa retornando `AxisResult` | [docs](2-ApplicationFlow/AxisValidator/README.md) |
| [`AxisBus`](2-ApplicationFlow/AxisBus/README.md) | Application & Flow | Abstração de event bus — fan-out assíncrono de eventos | [docs](2-ApplicationFlow/AxisBus/README.md) |
| [`AxisRepository`](3-Infra/AxisRepository/README.md) | Infrastructure | Portas de persistência, unit of work, adapters Postgres/MySQL | [docs](3-Infra/AxisRepository/README.md) |
| [`AxisCache`](3-Infra/AxisCache/README.md) | Infrastructure | Abstração de cache com adapter in-memory | [docs](3-Infra/AxisCache/README.md) |
| [`AxisStorage`](3-Infra/AxisStorage/README.md) | Infrastructure | Abstração de blob/arquivos (Cloudflare R2) | [docs](3-Infra/AxisStorage/README.md) |
| [`AxisEmail`](3-Infra/AxisEmail/README.md) | Infrastructure | Email transacional (MimeKit / SMTP) | [docs](3-Infra/AxisEmail/README.md) |
| [`AxisResult.HttpResponse`](4-Edge/AxisResult.HttpResponse/README.md) | Edge | Mapeia `AxisError` → `IActionResult`/`ProblemDetails` na borda HTTP | [docs](4-Edge/AxisResult.HttpResponse/README.md) |

Utilitários fundacionais — helpers de injeção de dependência, o kit de testes e as migrations — completam o conjunto.

---

## A base de regras

A árvore `rules/` está dividida em **`conventions/`** (arquitetura, domínio, persistência, edge, testes, estilo, processo) e **`framework/`** (invariantes por package, espelhando as cinco famílias). Os analyzers impõem as regras mecânicas em tempo de build; as docs carregam as de julgamento.

---

## Agentes do Claude Code

Além da base de regras, o framework entrega três subagentes do [Claude Code](https://claude.com/claude-code) mais um slash command orquestrador que automatizam o próprio fluxo de trabalho Axis — revisar um diff, planejar onde uma feature deve viver, e migrar um repositório existente rumo ao padrão, BC por BC. Fonte da verdade, passos de instalação (escopo de projeto + global) e regras de edição: [`.agents/claude/README.md`](../../.agents/claude/README.md).

| Agente | Papel |
|---|---|
| `axis-reviewer` | Gate de revisão pré-commit/pré-push — roda os gates determinísticos (build, analyzers, testes, linters) primeiro, e só depois o julgamento de topologia/semântica que nenhum analyzer cobre. Somente leitura. |
| `axis-architect` | Gate de planejamento que roda ANTES do código — decide fronteira de BC, classificação de subdomínio, nível do agregado e canal cross-BC. Somente leitura. |
| `axis-bc-migrator` | Migra um bounded context de um repositório existente (fora do padrão) rumo ao Axis; corrige o que é seguro, adia o que exige decisão. Acionado por BC pelo `/axis-audit`, que dispara um worker por bounded context e consolida o relatório. |

---

## Ordem de aprendizado sugerida

As packages são independentes, mas lê-las nesta ordem constrói o modelo de baixo para cima, com cada passo usando apenas o que veio antes.

1. [`AxisResult`](0-Foundations/AxisResult/README.md) — a ferrovia e os erros tipados. Tudo o mais retorna um.
2. [`AxisTypes`](0-Foundations/AxisTypes/README.md) — identidades e value objects tipados. Os tipos que seu domínio fala.
3. [`AxisCache`](3-Infra/AxisCache/README.md) — infraestrutura pequena, fácil primeira leitura.
4. [`AxisBus`](2-ApplicationFlow/AxisBus/README.md) — eventos e mensagens de integração.
5. [`AxisStorage`](3-Infra/AxisStorage/README.md) — blobs e arquivos.
6. [`AxisEmail`](3-Infra/AxisEmail/README.md) — email transacional.
7. [`AxisLogger`](1-Observability/AxisLogger/README.md) — logging estruturado.
8. [`AxisRepository`](3-Infra/AxisRepository/README.md) — persistência e unit of work.
9. [`AxisValidator`](2-ApplicationFlow/AxisValidator/README.md) — regras de entrada para `AxisResult`.
10. [`AxisTelemetry`](1-Observability/AxisTelemetry/README.md) — traces e metrics via OpenTelemetry.
11. [`AxisMediator`](2-ApplicationFlow/AxisMediator/README.md) — o pipeline de verbos; amarra a aplicação.
12. [`AxisSaga`](2-ApplicationFlow/AxisSaga/README.md) — processos longos construídos em cima do mediator.
13. [`AxisResult.HttpResponse`](4-Edge/AxisResult.HttpResponse/README.md) — a borda HTTP, que mapeia resultados para respostas HTTP.

---

## Princípios de design

1. **Você é dono do domínio; o framework é dono da arquitetura.** As decisões estruturais recorrentes são tomadas uma vez e impostas para todos.
2. **Erros são valores, não exceções.** Toda operação que pode falhar diz isso no tipo de retorno.
3. **O sistema de tipos é a documentação.** Ler a assinatura é ler o contrato.
4. **Dependa de ports, nunca de fornecedores.** A infraestrutura fica atrás de um port `AxisResult`; trocar de provedor é uma mudança de DI.
5. **Fronteiras, não pirâmides.** Exceções param na fronteira da infraestrutura; tudo lá dentro fala em `AxisResult`.
6. **Monolithic-first, pronto para microserviços.** Um único deployable hoje; promova um bounded context ao seu próprio serviço movendo pasta, não reescrevendo Core.
7. **Um package, uma responsabilidade.** Pegue o que precisa. Nada aqui arrasta um caminhão de recursos juntos.

---

## Licença

Apache 2.0
