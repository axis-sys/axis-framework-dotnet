# Recuperar · `Recover`

> O oposto do [`Then`](then.md): opera na **trilha de falha**. `Recover` e seus parentes trazem o pipeline de volta ao sucesso — sempre de forma **deliberada e explícita**.

---

## Quando usar

Fornecer um padrão quando algo não foi encontrado, cair para uma fonte alternativa quando um serviço está fora, tentar um segundo caminho.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| só **observar** o erro (log) sem recuperar | [`TapError`](tap.md) |
| **reescrever** o erro, não recuperar | [`MapError`](map-errors.md) |

---

## Operadores

| Método | Recupera quando… |
|---|---|
| `Recover(value)` / `Recover(func)` | **qualquer** falha → valor padrão |
| `RecoverWhen(AxisErrorType, func)` | os erros são de um **tipo** |
| `RecoverWhen(code, func)` | os erros têm um **código** |
| `RecoverWhen(predicate, func)` | um **predicado** sobre os erros |
| `RecoverNotFound(func)` | **todos** os erros são `NotFound` |
| `RecoverConflict(func)` | **todos** os erros são `Conflict` |
| `OrElse(fallback)` | tenta uma **operação** alternativa |
| `OrElse(fallback, combineErrors: true)` | alternativa, **acumulando** os erros dos dois lados |

Todos têm variantes `Async` (`Task`/`ValueTask`).

---

## Exemplo 1 — padrão quando não existe

```csharp
var settings = await GetUserSettingsAsync(userId)
    .RecoverNotFoundAsync(() => DefaultSettings.Create());
```

**Por que compensa:** "não tem configuração salva? use o padrão" deixa de ser um `catch (NotFoundException)` e vira uma intenção explícita — e só recupera de `NotFound`, não de erros de verdade (ex.: falha de banco).

## Exemplo 2 — ensure read-mostly: `RecoverNotFound` por fora, `RecoverConflict` por dentro

```csharp
public Task<AxisResult<Customer>> EnsureAsync(string externalId)
    => customers.GetByExternalIdAsync(externalId)      // o caso comum: já existe — uma leitura
        .RecoverNotFoundAsync(() => CreateAsync(externalId));

private Task<AxisResult<Customer>> CreateAsync(string externalId)
    => BuildCustomer(externalId).Rop()
        .ThenAsync(customers.CreateAsync)
        .ThenAsync(_ => unitOfWork.SaveChangesAsync())
        .RecoverConflictAsync(() => customers.GetByExternalIdAsync(externalId)); // perdeu a corrida -> busca o vencedor
```

**Por que compensa:** o caminho quente de um get-or-create é o *get*, então ele fica em uma única
leitura, sem insert falho. A criação é o ramo raro — e só ali existe a janela de check-then-act, que o
`RecoverConflict` fecha: o perdedor de uma corrida concorrente de primeira-vez relê a linha que o
vencedor gravou. As duas recuperações são falíveis (`Func<Task<AxisResult<T>>>`), e qualquer falha que
não seja NotFound / Conflict ainda aflora.

> **Get-or-create vs. guarda — dois duais de "criar quando ausente".** `RecoverConflict` / `RecoverNotFound`
> acima são **idempotentes**: um duplicado é absorvido e o valor existente volta como **sucesso**. O dual
> é a **guarda** — ler primeiro e deixar um duplicado virar **falha de conflito** — veja
> [`RequireNotFound`](ensure.md). Escolha pela intenção. O scaffold mostra os dois na mesma operação —
> `RegisterProduct` (recover) ao lado de `CreateProduct` (guarda).

## Exemplo 3 — fallback condicional por tipo

```csharp
var data = await FetchFromPrimaryAsync()
    .RecoverWhenAsync(AxisErrorType.ServiceUnavailable, () => FetchFromFallbackAsync());
```

**Por que compensa:** só cai para o secundário quando o primário está **indisponível** (transiente); um erro de validação, por exemplo, continua falhando como deveria.

## Exemplo 4 — caminho alternativo com acúmulo de erros

```csharp
var user = await FindByEmailAsync(email)
    .OrElseAsync(_ => FindByPhoneAsync(phone), combineErrors: true);
// se os DOIS falharem, você recebe os erros das duas tentativas
```

---

## Além do Recover · `ElseNotFound`

`ElseNotFound<TNew>` parece um parente do `RecoverNotFound`, mas **não faz parte dessa família** —
todo operador acima devolve a fonte intocada quando ela já é sucesso. `ElseNotFound` não consegue
prometer isso: ele existe especificamente para convergir um valor *encontrado* e um padrão de
*NotFound* num **tipo novo**, então precisa rodar nas duas trilhas.

```csharp
var response = await factory.GetByIdAsync(externalApiId)          // AxisResult<ExternalApi>
    .ElseNotFoundAsync(
        api => BuildResponse(api),                                // achou → mapeia para o tipo de resposta
        () => ExternalApiResponse.Empty());                        // NotFound → um padrão desse MESMO tipo novo
// qualquer outro erro (ex.: falha de banco) continua propagando como AxisResult<ExternalApiResponse>
```

**Por que compensa:** sem ele, o mesmo resultado exige duas chamadas —
`.MapAsync(BuildResponse).RecoverNotFoundAsync(() => ExternalApiResponse.Empty())` — que continua
sendo a escolha certa sempre que algum dos dois lados puder falhar (troque por `ThenAsync<TNew>` no
lado que achou, ou por `RecoverNotFound(Func<AxisResult<TNew>>)` no lado da recuperação). Use
`ElseNotFound` só para a forma simples, sem falha em nenhum dos dois lados; use `RecoverNotFound(func)`
(sem parâmetro de tipo) quando o valor recuperado continua do **mesmo** tipo da fonte.

---

## Veja também

- [Erros e tipos](errors-and-types.md) — `IsTransient`, tipos e códigos para condicionar a recuperação
- [Remapear erros · `MapError`](map-errors.md) — transformar o erro em vez de recuperar
- [Garantir · `Ensure`](ensure.md) — o oposto: levar do sucesso à falha

---

↩ [Voltar à documentação do AxisResult](README.md)
