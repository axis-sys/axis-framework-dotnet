# Referência da API

> O catálogo completo, agrupado por responsabilidade. Use para consulta — cada grupo linka de volta à sua página de detalhe.

---

## O contrato — `IAxisBus`

| Método | Assinatura | Descrição |
|---|---|---|
| `PublishAsync<TEvent>` | `Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics) where TEvent : IAxisEvent` | distribuir para cada handler registrado, agregar as falhas |

→ [O contrato `IAxisBus`](iaxisbus.md) · [Publicar · `PublishAsync`](publish.md)

---

## Contratos de evento (de `AxisMediator.Contracts.CQRS.Events`)

| Tipo | Formato | Descrição |
|---|---|---|
| `IAxisEvent` | `string? OrderingKey => null` (implementado por padrão) | identifica o payload como evento de bus; `OrderingKey` permite optar pela chave de partição FIFO do outbox durável |
| `IAxisEventHandler<TEvent>` | `Task<AxisResult> HandleAsync(TEvent @event)` | um handler por `TEvent` (zero ou mais — todos rodam) |

→ [Definindo eventos e handlers](events-and-handlers.md)

---

## Adapter in-process — `AxisMemoryBus`

| Membro | Descrição |
|---|---|
| `MemoryBusAdapter(IServiceProvider)` | construtor; resolve handlers a cada publish |
| `services.AddAxisMemoryBus()` | extensão DI; scaneia o assembly chamador por handlers, registra `IAxisBus → MemoryBusAdapter` (scoped) |

→ [Adapter `AxisMemoryBus`](memory-adapter.md)

---

## Contrato de comportamento (para adapters)

| Cenário | `AxisResult` retornado |
|---|---|
| zero handlers registrados | `Ok()` |
| todo handler retorna `Ok()` | `Ok()` |
| K de N handlers retornam erros | `Combine`d — K grupos de erros, sem contribuição de Ok |
| um handler lança | (adapter da caixa) exceção escapa; um adapter custom pode capturar |
| cancelamento | `Error(...)` se o adapter honrar; o adapter da caixa atualmente não propaga |

→ [Adapter custom](custom-adapter.md)

---

## Adapter de outbox durável — `AxisBus.Repository` (`AxisBus.Postgres` / `AxisBus.MySql`)

Um adapter `IAxisBus` pronto para produção já vem na caixa: publicar enfileira numa fila com escopo de requisição e o unit of work a drena para sua própria transação no commit, então o evento e a mudança de estado do negócio pousam atomicamente; um dispatcher em background separado entrega depois do commit. Não há coluna de status — a presença de uma linha é seu estado pendente, e a entrega é sua deleção (claim-by-lease, at-least-once).

| Serviço | Lifetime | Descrição |
|---|---|---|
| `AxisBusRepositorySettings` | singleton | connection string, intervalo de poll, duração do lease, tamanho de lote, opt-outs de dispatcher/migration |
| `IAxisBus` | scoped | `RepositoryBusAdapter` — enfileira na fila com escopo de requisição, nunca toca no banco diretamente |
| `IOutboxScopedQueue` | scoped | a fila com escopo de requisição drenada no commit |
| `IAxisRepositoryOutbox` | scoped | `RepositoryOutboxDrain` — a ponte que o unit of work invoca no commit |
| `IBusDispatcher` | scoped | `BusDispatcher` — reivindica heads de partição devidos, distribui para handlers, deleta/libera |
| `IBusEventDispatchStore` | scoped | `PostgresBusDispatchStore` / `MySqlBusDispatchStore` — claim/delete/release contra a tabela do outbox |
| `IAxisBusStorageInitializer` | singleton | bootstrap do schema para `AXIS_OUTBOX.OUTBOX_EVENTS` (específico do dialeto) |
| `AxisBusStorageInitializerWorker` | hosted service | roda a migration do schema no startup (opt out via `RunStartupMigration`) |
| `AxisBusDispatcherWorker` | hosted service | o loop de poll que dirige o `BusDispatcher` (opt out via `DispatcherEnabled`) |

Registre com `services.AddAxisBusPostgres(settings)` ou `services.AddAxisBusMySql(settings)` — um storage adapter por processo (uma segunda chamada lança exceção).

→ [Adapter custom](custom-adapter.md)

---

## Veja também

- [Primeiros passos](getting-started.md) — instale, registre, publique
- [Por que AxisBus?](why-axisbus.md) — o argumento pela porta de um método só
- [Documentação completa](README.md) — o mapa de toda a documentação

---

↩ [Voltar à documentação do AxisBus](README.md)
