# Erros e tipos · `AxisError`

> Um erro no AxisResult é um **valor**, não uma exceção. Cada `AxisError` carrega só duas coisas: um **`Code`** estável e um **`Type`** (categoria).

```csharp
AxisError err = AxisError.NotFound("USER_NOT_FOUND");
// err.Code  → "USER_NOT_FOUND"            (chave estável: logs, métricas, retry)
// err.Type  → AxisErrorType.NotFound
```

---

## As 12 categorias

Todo `AxisError` tem um `AxisErrorType`. Elas mapeiam naturalmente para status HTTP — o que torna a tradução na borda da API trivial:

| Fábrica | Tipo | HTTP |
|---|---|:--:|
| `AxisError.ValidationRule(code)` | `ValidationRule` | 400 |
| `AxisError.Unauthorized(code)` | `Unauthorized` | 401 |
| `AxisError.Forbidden(code)` | `Forbidden` | 403 |
| `AxisError.NotFound(code)` | `NotFound` | 404 |
| `AxisError.Conflict(code)` | `Conflict` | 409 |
| `AxisError.BusinessRule(code)` | `BusinessRule` | 422 |
| `AxisError.TooManyRequests(code)` | `TooManyRequests` | 429 |
| `AxisError.InternalServerError(code)` | `InternalServerError` | 500 |
| `AxisError.Mapping(code)` | `Mapping` | 500 |
| `AxisError.ServiceUnavailable(code)` | `ServiceUnavailable` | 503 |
| `AxisError.Timeout(code)` | `Timeout` | 504 |
| `AxisError.GatewayTimeout(code)` | `GatewayTimeout` | 504 |

---

## Por que não existe um campo `Message`?

Decisão deliberada: `AxisError` carrega **só** `Code` + `Type`.

- **O `Code` é a chave primária.** Estável entre versões, para que logs, métricas, alertas e políticas de retry pivotem nele sem fazer *parse* de string.
- **Mensagem é outra responsabilidade.** Localização, tom e a decisão de expor (ou não) um detalhe interno ao usuário final não pertencem a um valor que trafega no pipeline.

O padrão recomendado é um **resolver `code → mensagem` na borda de apresentação** (controller, interceptor gRPC), com os textos em arquivos de recurso:

```csharp
return result.Match(
    onSuccess: value  => Ok(value),
    onFailure: errors => Problem(
        title:  errors[0].Type.ToString(),
        detail: messageResolver.Resolve(errors[0], CultureInfo.CurrentUICulture)));
```

Assim: códigos pequenos e canônicos (`USER_NOT_FOUND`); várias UIs (REST, gRPC, CLI) renderizam o mesmo código de formas diferentes; testes verificam **códigos**, não prosa em inglês; nenhum dado pessoal vaza no payload de erro.

> Precisa passar **detalhes** (id, quantidade tentada)? Emita **vários `AxisError`** — a lista de erros já é a coleção natural para isso. Veja [Agregar · `Combine`/`All`](aggregate.md).

---

## Erros transientes (retry)

```csharp
if (error.IsTransient)   // true p/ ServiceUnavailable, Timeout, TooManyRequests, GatewayTimeout
    await RetryAsync();
```

`IsTransient` está embutido no tipo. *Circuit breakers*, políticas de retry e *health checks* inspecionam isso sem fazer *parse* de mensagens nem manter listas de strings.

**Por que compensa:** "isto vale a pena tentar de novo?" vira uma propriedade do sistema de tipos, não uma convenção frágil baseada em texto.

### `IsTransientFailure` — a mesma pergunta, no `AxisResult` inteiro

Um loop de polling ou um worker de background raramente tem um único erro para inspecionar — tem um
`AxisResult` inteiro. Em vez de re-derivar `result.IsFailure && result.Errors.All(e => e.IsTransient)` em
cada ponto de chamada, pergunte direto ao resultado:

```csharp
if (result.IsTransientFailure)
    await BackoffAndRetryAsync();     // dependência ainda não pronta — não é falha terminal
```

`IsTransientFailure` é um **eixo de classificação diferente** de `IsSuccess`/`IsFailure` — ele distingue
*transiente-vs-terminal* (política de retry), não *sucesso-vs-falha* (a trilha ROP). Ramificar nele é a
forma sancionada de conduzir o controle de fluxo de retry/polling; a regra de composição ROP (sem
`if`/`else` em `IsSuccess`/`IsFailure`, [Railway-Oriented Programming](railway-oriented-programming.md)) não
se aplica a ele, porque essa regra é sobre a forma de *pipeline* de duas trilhas, e a decisão "continuar
esperando ou seguir em frente" de um loop de retry não é um pipeline — é um loop com estado sobre um fluxo
de desfechos ao longo do tempo.

---

## Veja também

- [Remapear erros · `MapError`](map-errors.md) — reescrever códigos/tipos ao cruzar camadas
- [Recuperar · `Recover`](recover.md) — voltar da trilha de falha para a de sucesso
- [Sair · `Match`](match.md) — converter o resultado final em resposta HTTP

---

↩ [Voltar à documentação do AxisResult](README.md)
