# Application guide

How an application is built the Axis way: a hexagon split into projects, vertical-sliced by bounded context inside
each project, where every use case is a command or query exposed by a facade, every piece of infrastructure is a
port returning `AxisResult`, and the host is the only place that chooses an adapter.

Every convention here is followed by the **reference application** in
[`scaffolds/`](https://github.com/axis-sys/axis-framework-dotnet/tree/main/scaffolds), a small e-commerce with five
bounded contexts over Postgres or MySQL, and each section links the files that show it. When a convention and the
reference application disagree, the application is the one that compiles and passes its tests: trust the code and
report the page.

## Reading order

| Page | Read it when you… |
|---|---|
| [Architecture](architecture.md) | start an application, add a bounded context, or decide where a type lives and how two contexts talk |
| [Domain](domain.md) | model an aggregate, its state, its factory, its codes and its value objects |
| [Persistence](persistence.md) | write a port, a repository, a table or a migration, or reason about the unit of work |
| [HTTP edge](edge.md) | expose a use case or a saga over HTTP, version a route, secure it, document it |
| [Testing](testing.md) | decide which test project a test belongs to and write it |
| [Code style](style.md) | name a project, a type or an error, or pick a visibility |
| [Process](process.md) | write comments and docs, layer configuration, run migrations, review a change |

## The packages behind it

The guide shows how the packages are combined; each package's own articles — the operators of `AxisResult`, the
mediator pipeline, the saga DSL, the repository — are under [Packages](../articles/index.md), and the contract of
every public member is in the [API reference](../api/index.md).

## For coding assistants

The same conventions, condensed for a coding assistant, are in
[`agents/axis-framework-dotnet.md`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/agents/axis-framework-dotnet.md):
copy it into whatever instructions format your assistant reads.
