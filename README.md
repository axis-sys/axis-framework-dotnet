# Axis Framework

> 🌐 [Português (documentação navegável)](docs/pt-br/README.md)

**Write your domain. Axis owns the architecture.** — an opinionated, ports-and-adapters .NET framework where the core stays pure, every failure is a typed value, and a monolith splits into microservices by moving a folder. Typed building blocks, Railway-Oriented orchestration, swappable infrastructure adapters, observability and the HTTP edge — all composing without ceremony.

```csharp
// A whole feature. This is all you write — the framework does the rest.
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    => customerFactory.GetByIdAsync(cmd.CustomerId)
        .ThenAsync(customer => orderFactory.CreateAsync(new()
        {
            CustomerId = customer.CustomerId,
            ProductId  = cmd.ProductId,
            Quantity   = cmd.Quantity
        }))
        .ThenAsync(order => bus.PublishAsync(new OrderCreatedEvent(order.OrderId))) // enqueue the "OrderCreated" fact in the outbox…
        .ThenAsync(order => unitOfWork.SaveChangesAsync())                          // …then one commit persists the order AND the event, atomically
        .TapAsync(order => logger.LogInformation("Order {OrderId} created", order.OrderId))
        .MapAsync(order => new CreateOrderResponse { OrderId = order.OrderId });

// Validation, DI wiring, error→HTTP mapping, tracing and metrics are the framework's
// job — not yours, and not in this file. The event is staged on the bus (an outbox
// adapter) and the single SaveChangesAsync commits it together with the order in one
// transaction: publish first, commit once — never a commit-then-publish dual write.
```

Use this page as a **map**: read the trunk below (~5 min) to understand *why Axis exists*, then jump to the family or package you need — without reading hundreds of lines.

---

## The trunk (read first)

### Why Axis? Three promises

**1 · You write the domain. Axis owns the architecture.**
A feature is a **vertical slice** through a **hexagonal** boundary — a CQRS use case (`Command`/`Query` → `Response` → `Handler` → `Validator`) exposed by a **Facade**. You write the handler; the framework provides the pipeline, the dependency wiring, the transaction boundary, the validation gate, the error-to-HTTP mapping and the telemetry. The architectural questions are answered once, by the framework, and enforced for everyone.

**2 · Ports and adapters — a pure core that never depends on a vendor.**
Every piece of infrastructure (persistence, messaging, cache, object storage, e-mail) is a small **port** that returns `AxisResult` and never throws. The adapter owns the `try/catch` and the SDK; the application sees only the interface. Migrating Postgres → SQL Server, or RabbitMQ → Kafka, is a **DI change at the composition root**.

**3 · Monolithic-first — split into microservices by moving a folder.**
Start with **one deployable** hosting API, workers and jobs. Inside it, code is vertical-sliced by **bounded context**, and because a namespace mirrors its folder path exactly, promoting a bounded context to its own microservice is a **structural move, not a rewrite** — the BC's folders become projects along the layers, every namespace stays identical, and the database can be isolated on the same terms (already separated by schema, already isolated by nature). Cross-BC traffic already flows through the three sanctioned channels, so nothing breaks when the boundary becomes a network hop.

### The three sanctioned cross-BC channels

A bounded context never reaches into another BC's ports or internals. Everything crosses the boundary through exactly one of:

| Need | Channel |
|---|---|
| Synchronous read/write | the other BC's public **Facade** |
| Fire-and-forget side effect | an event on the **Bus** |
| Multi-BC process with compensation | a **Saga** whose stage handlers live in the owning BC |

Because these are the *same* channels whether the BCs share a process or sit on opposite ends of a network, the monolith → microservices promotion changes deployment, not design.

### What's in the box

This is **one repository** that ships everything needed to build the Axis way:

| | |
|---|---|
| 📦 **Packages** (`src/`) | The NuGet libraries, organised into five families. Targets **.NET 10**. |
| 🛡️ **Analyzers & generators** | Roslyn analyzers and a source generator enforce the invariants at build time — a violated rule fails the build, not the code review. |
| 📐 **Rules** (`rules/`) | **Around 400 canonical rules** (conventions + framework) — the single source of truth every analyzer and doc derives from. |
| 🏗️ **Scaffolds** (`src/scaffolds/`) | A complete ECommerce reference application — domain, facades, pipelines, ports, use cases and tests. |
| 📚 **Docs** (`docs/`) | Navigable documentation in **English and Portuguese**, plus the architecture decision records (ADRs). |
| 🤖 **Claude Code agents** (`.agents/claude/`) | Three subagents plus a slash command that automate the Axis workflow — review, planning and migrating an existing repo toward the pattern. |

### One source of truth

The rule base in `rules/` is canonical: the analyzers make the mechanical rules **non-negotiable at build time**, while the docs carry the judgment ones. A human reading a doc and a build failing on a violated rule are enforcing the very same invariant.

### Installation

```
dotnet add package AxisResult
```

Every package is independent — add only what a slice needs. → Start with **[`AxisResult`](docs/en-us/0-Foundations/AxisResult/README.md)**, the railway everything else returns.

---

## The map — the five families

Inspired by the GoF taxonomy, the packages are organised into **five families** by responsibility, mirrored one-to-one by the numbered top-level folders. Knowing the family of a package tells you roughly *what kind of problem it solves* and *where it sits in your stack*.

### 0 · Foundations — typed building blocks

Zero infrastructure, pure types and abstractions. **Start here.**

- [`AxisResult`](docs/en-us/0-Foundations/AxisResult/README.md) — Railway-Oriented Programming, the typed `Result` monad with async support
- [`AxisTypes`](docs/en-us/0-Foundations/AxisTypes/README.md) — strongly-typed value objects (`AxisEntityId`) + `[ValueObject]` source generator
- [`AxisMediator.Contracts`](docs/en-us/0-Foundations/AxisMediator.Contracts/README.md) — pure CQRS contracts shared by the mediator and its consumers

### 1 · Observability — what your operators stare at

- [`AxisLogger`](docs/en-us/1-Observability/AxisLogger/README.md) — structured logging, correlation, enrichers
- [`AxisTelemetry`](docs/en-us/1-Observability/AxisTelemetry/README.md) — OpenTelemetry traces / metrics / logs, with an Azure Monitor / Application Insights adapter

### 2 · Application & Flow — the verbs of your domain

- [`AxisMediator`](docs/en-us/2-ApplicationFlow/AxisMediator/README.md) — request/response mediator, pipelines, behaviours
- [`AxisSaga`](docs/en-us/2-ApplicationFlow/AxisSaga/README.md) — long-running process orchestration with compensations
- [`AxisValidator`](docs/en-us/2-ApplicationFlow/AxisValidator/README.md) — declarative validation that returns `AxisResult`
- [`AxisBus`](docs/en-us/2-ApplicationFlow/AxisBus/README.md) — event bus abstraction: asynchronous fan-out of events to handlers

### 3 · Infrastructure & Integration — adapters, always behind a port

The application never depends on a vendor.

- [`AxisRepository`](docs/en-us/3-Infra/AxisRepository/README.md) — persistence ports, unit of work, Postgres/MySQL adapters (raw Npgsql/MySqlConnector, no ORM)
- [`AxisCache`](docs/en-us/3-Infra/AxisCache/README.md) — cache abstraction with an in-memory adapter
- [`AxisStorage`](docs/en-us/3-Infra/AxisStorage/README.md) — blob/file storage abstraction (Cloudflare R2 / S3-compatible)
- [`AxisEmail`](docs/en-us/3-Infra/AxisEmail/README.md) — transactional email sender (MimeKit / SMTP)

### 4 · Edge — the door your clients knock on

- [`AxisResult.HttpResponse`](docs/en-us/4-Edge/AxisResult.HttpResponse/README.md) — maps `AxisError` → `IActionResult` / `ProblemDetails` at the HTTP boundary

---

## Packages at a glance

| Package | Family | One-liner | Docs |
|---|---|---|---|
| [`AxisResult`](docs/en-us/0-Foundations/AxisResult/README.md) | Foundations | Railway-Oriented `Result` monad with `async`/`ValueTask` and typed errors | [docs](docs/en-us/0-Foundations/AxisResult/README.md) |
| [`AxisTypes`](docs/en-us/0-Foundations/AxisTypes/README.md) | Foundations | Strongly-typed value objects + `[ValueObject]` source generator | [docs](docs/en-us/0-Foundations/AxisTypes/README.md) |
| [`AxisMediator.Contracts`](docs/en-us/0-Foundations/AxisMediator.Contracts/README.md) | Foundations | Pure CQRS contracts shared by the mediator and its consumers | [docs](docs/en-us/0-Foundations/AxisMediator.Contracts/README.md) |
| [`AxisLogger`](docs/en-us/1-Observability/AxisLogger/README.md) | Observability | Structured logging with enrichment | [docs](docs/en-us/1-Observability/AxisLogger/README.md) |
| [`AxisTelemetry`](docs/en-us/1-Observability/AxisTelemetry/README.md) | Observability | OpenTelemetry integration, Azure Monitor adapter | [docs](docs/en-us/1-Observability/AxisTelemetry/README.md) |
| [`AxisMediator`](docs/en-us/2-ApplicationFlow/AxisMediator/README.md) | Application & Flow | In-process request/response mediator with pipelines | [docs](docs/en-us/2-ApplicationFlow/AxisMediator/README.md) |
| [`AxisSaga`](docs/en-us/2-ApplicationFlow/AxisSaga/README.md) | Application & Flow | Saga orchestration with compensations and state | [docs](docs/en-us/2-ApplicationFlow/AxisSaga/README.md) |
| [`AxisValidator`](docs/en-us/2-ApplicationFlow/AxisValidator/README.md) | Application & Flow | Declarative validation returning `AxisResult` | [docs](docs/en-us/2-ApplicationFlow/AxisValidator/README.md) |
| [`AxisBus`](docs/en-us/2-ApplicationFlow/AxisBus/README.md) | Application & Flow | Event bus abstraction — asynchronous fan-out of events | [docs](docs/en-us/2-ApplicationFlow/AxisBus/README.md) |
| [`AxisRepository`](docs/en-us/3-Infra/AxisRepository/README.md) | Infrastructure | Persistence ports, unit of work, Postgres/MySQL adapters | [docs](docs/en-us/3-Infra/AxisRepository/README.md) |
| [`AxisCache`](docs/en-us/3-Infra/AxisCache/README.md) | Infrastructure | Cache abstraction with an in-memory adapter | [docs](docs/en-us/3-Infra/AxisCache/README.md) |
| [`AxisStorage`](docs/en-us/3-Infra/AxisStorage/README.md) | Infrastructure | Blob/file storage abstraction (Cloudflare R2) | [docs](docs/en-us/3-Infra/AxisStorage/README.md) |
| [`AxisEmail`](docs/en-us/3-Infra/AxisEmail/README.md) | Infrastructure | Transactional email (MimeKit / SMTP) | [docs](docs/en-us/3-Infra/AxisEmail/README.md) |
| [`AxisResult.HttpResponse`](docs/en-us/4-Edge/AxisResult.HttpResponse/README.md) | Edge | Maps `AxisError` → `IActionResult`/`ProblemDetails` at the HTTP edge | [docs](docs/en-us/4-Edge/AxisResult.HttpResponse/README.md) |

Foundational utilities — dependency-injection helpers, the testing kit, and migrations — round out the set.

---

## The rule base

The `rules/` tree is split into **`conventions/`** (architecture, domain, persistence, edge, testing, style, process) and **`framework/`** (per-package invariants, mirroring the five families). The analyzers enforce the mechanical rules at build time; the docs carry the judgment ones.

---

## Claude Code agents

On top of the rule base, the framework ships three [Claude Code](https://claude.com/claude-code) subagents plus one orchestrating slash command that automate the Axis workflow itself — reviewing a diff, planning where a feature belongs, and migrating an existing repository toward the pattern BC by BC. Source of truth, install steps (project + global scope) and editing rules: [`.agents/claude/README.md`](.agents/claude/README.md).

| Agent | Role |
|---|---|
| `axis-reviewer` | Pre-commit/pre-push review gate — runs the deterministic gates (build, analyzers, tests, linters) first, then the topology/semantic judgment no analyzer covers. Read-only. |
| `axis-architect` | Planning gate that runs BEFORE code — decides BC boundary, subdomain classification, aggregate level and cross-BC channel. Read-only. |
| `axis-bc-migrator` | Migrates one bounded context of an existing (non-Axis) repository toward the pattern; fixes what's safe, defers what needs a decision. Invoked per BC by `/axis-audit`, which fans out one worker per bounded context and consolidates the report. |

---

## Suggested learning order

The packages are independent, but reading them in this order builds the model bottom-up, each step using only what came before.

1. [`AxisResult`](docs/en-us/0-Foundations/AxisResult/README.md) — the railway and typed errors. Everything else returns one.
2. [`AxisTypes`](docs/en-us/0-Foundations/AxisTypes/README.md) — typed identities and value objects. The types your domain talks in.
3. [`AxisCache`](docs/en-us/3-Infra/AxisCache/README.md) — small infrastructure, easy first read.
4. [`AxisBus`](docs/en-us/2-ApplicationFlow/AxisBus/README.md) — events and integration messages.
5. [`AxisStorage`](docs/en-us/3-Infra/AxisStorage/README.md) — blobs and files.
6. [`AxisEmail`](docs/en-us/3-Infra/AxisEmail/README.md) — transactional email.
7. [`AxisLogger`](docs/en-us/1-Observability/AxisLogger/README.md) — structured logging.
8. [`AxisRepository`](docs/en-us/3-Infra/AxisRepository/README.md) — persistence and unit of work.
9. [`AxisValidator`](docs/en-us/2-ApplicationFlow/AxisValidator/README.md) — input rules into `AxisResult`.
10. [`AxisTelemetry`](docs/en-us/1-Observability/AxisTelemetry/README.md) — OpenTelemetry traces and metrics.
11. [`AxisMediator`](docs/en-us/2-ApplicationFlow/AxisMediator/README.md) — the verb pipeline; ties the application together.
12. [`AxisSaga`](docs/en-us/2-ApplicationFlow/AxisSaga/README.md) — long processes built on top of the mediator.
13. [`AxisResult.HttpResponse`](docs/en-us/4-Edge/AxisResult.HttpResponse/README.md) — the HTTP edge, mapping results to HTTP responses.

---

## Design principles

1. **You own the domain; the framework owns the architecture.** The recurring structural decisions are made once and enforced for everyone.
2. **Errors are values, not exceptions.** Every operation that can fail says so in its return type.
3. **The type system is the documentation.** Reading the signature is reading the contract.
4. **Depend on ports, never on vendors.** Infrastructure sits behind an `AxisResult` port; swapping a provider is a DI change.
5. **Boundaries, not pyramids.** Exceptions stop at the infrastructure boundary; everything inside speaks in `AxisResult`.
6. **Monolithic-first, microservice-ready.** One deployable today; promote a bounded context to its own service by moving folder, not by rewriting Core.
7. **One package, one responsibility.** Pick what you need. Nothing here pulls in a kitchen sink.

---

## License

Apache 2.0
