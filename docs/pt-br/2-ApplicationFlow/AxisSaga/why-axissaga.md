# Por que AxisSaga? · comparação

> Há outras maneiras de orquestrar workflows longos em .NET. Esta página diz por que o AxisSaga é diferente — uma comparação direta, sem mão na cintura.

---

## vs. MassTransit saga / Automatonymous

`MassTransit.Saga` é a opção pesada — máquinas de estado construídas em Automatonymous, mais um substrato de messaging distribuído completo. É excelente para orquestração cross-service complexa. AxisSaga é **declarativo primeiro**, **in-process por padrão**, e **integra com o resto do Axis** (`AxisResult`, `AxisBus`, `AxisLogger`, o schema `AXIS_SAGA`). Se você não precisa da história de transporte do MassTransit, AxisSaga embarca menos cerimônia.

## vs. sagas do NServiceBus

Mesmo trade-off. NServiceBus é um produto de messaging completo; AxisSaga é um pequeno saga engine. Se seu bus já é MassTransit / NServiceBus, talvez você não precise de AxisSaga. Se seu bus é `AxisBus`, AxisSaga encaixa naturalmente.

## vs. Temporal / DTFx / serviços de orquestração

Temporal e Azure DTFx são engines de execução durável: registram cada passo no próprio runtime e replay a history no resume. AxisSaga é uma **máquina de estados sobre um banco relacional** (atualmente Postgres e MySQL, entre outros) — mais simples, mais leve, mais fácil de inspecionar à mão. Use Temporal/DTFx quando genuinamente precisa de versionamento de workflow, signals, fan-out de child-workflow etc. AxisSaga basta para "uma sequência de passos com compensação".

## vs. uma máquina de estados caseira

DIY. Mesma forma — forward stages, error stages, roteamento — mas você redescobre o engine, a persistência, o resumer, o catálogo, o schema, a tabela de stage-log. AxisSaga poupa o custo.

---

## A comparação

| Característica | AxisSaga | MassTransit Saga | NServiceBus Saga | Temporal | Caseiro |
|---|:--:|:--:|:--:|:--:|:--:|
| API declarativa de stage + roteamento | **Sim** | Sim (máquina de estados) | Sim (máquina de estados) | Não (código) | Talvez |
| Retorna `AxisResult` de cada stage | **Sim** | Não | Não | Não | Talvez |
| Compensação como roteamento first-class | **Sim** | Sim | Sim | Sim (código) | Talvez |
| Tabela forense por stage | **Sim** (`saga_stage_logs`) | Auditoria via SDK | Auditoria via SDK | Built-in | Manual |
| Resumer por polling para instâncias travadas | **Sim** | Built-in | Built-in | Built-in | Manual |
| Linhas com concorrência otimista | **Sim** | Sim | Sim | n/a | Manual |
| Storage relacional embarcado (Postgres, MySQL, …) | **Sim** | Configurável | Configurável | Self-hosted | Manual |
| Só in-process (sem broker) | **Sim** | Não | Não | Não | Sim |
| Abstração minúscula (um configurator + uma interface de handler) | **Sim** | Não | Não | Não | Sim |

---

## Veja também

- [Primeiros passos](getting-started.md) — instale e rode
- [Conceitos](concepts.md) — as engrenagens
- [Configurator](configuration.md) — o builder declarativo

---

↩ [Voltar à documentação do AxisSaga](README.md)
