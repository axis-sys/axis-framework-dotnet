# Por que AxisTypes? · comparação

> Há outras maneiras de modelar value objects fortemente tipados em C#. Esta página diz por que o AxisTypes é diferente — uma comparação direta, sem mão na cintura.

---

## vs. `string` e `Guid` crus

A linha de base "sem biblioteca". Barata, ubíqua e, em qualquer codebase não-trivial, um ímã para bugs.

```csharp
public Task<Customer> GetAsync(string customerId, string tenantId, string countryCode); // 👀
```

Nada impede o chamador de trocar os argumentos. O compilador fica em silêncio, o runtime fica em silêncio, e o bug aparece em produção como o cliente errado. **AxisTypes** tipa cada parâmetro (`AxisEntityId`) e a própria assinatura recusa o erro.

## vs. value objects escritos à mão

Você escreve seu próprio `record struct OrderNumber(string Value)` e depois adiciona — manualmente — `implicit operator string`, `implicit operator OrderNumber(string?)`, `ToString`, `Equals` case-insensitive, `GetHashCode` e dois overloads de `TryParse`. Multiplique por cada value object que você tiver. **AxisTypes** move todo esse boilerplate para um source generator Roslyn: você escreve o construtor, o gerador escreve o resto, e todo value object se comporta igual.

## vs. `Vogen`

`Vogen` é o vizinho mais próximo — um source generator de value object fortemente tipado com features ricas (interfaces de validação, métodos de instância, conversores EF/JSON, configuração). É **maior** e **mais opinativo** que `AxisTypes`. Se você precisa da amplitude dele, use. Se você quer um **gerador pequeno e focado** que casa com o restante do Axis (`AxisResult`, `AxisEntityId`) e não embarca nada em runtime, use `AxisTypes`.

## vs. `StronglyTypedId`

`StronglyTypedId` cobre exatamente uma forma: structs de id que envelopam um primitivo (`Guid`, `int`, `long`, `string`). Não te dá o **escape do construtor** que o AxisTypes dá — seu construtor é a validação, e o gerador embrulha tudo ao redor dele. Se seus value objects são puros ids, ambos funcionam; se você precisa de lógica de parsing ou normalização, AxisTypes mantém isso onde ela pertence.

## vs. uma `struct` simples com operadores manuais

A opção DIY. Mesmo trade-off de "value objects escritos à mão" acima, mais o ônus de manutenção de manter `Equals`/`GetHashCode` alinhados com o `Value`. **AxisTypes** remove o ônus de manutenção completamente.

---

## A comparação

| Característica | AxisTypes | `string` cru | À mão | Vogen | StronglyTypedId |
|---|:--:|:--:|:--:|:--:|:--:|
| Type safety em compile-time entre ids de domínio | **Sim** | Não | Sim | Sim | Sim |
| `implicit operator string` | **Sim** | n/a | manual | Sim | Sim |
| `implicit operator T(string?)` | **Sim** | n/a | manual | Sim | Não |
| `Equals`/`GetHashCode` case-insensitive | **Sim (padrão)** | Não | manual | Configurável | Não |
| Toggle de invariant-culture | **Sim** | Não | manual | Sim | Não |
| `TryParse(object?, out T)` não-lançante | **Sim** | n/a | manual | Sim | Não |
| Construtor como ponto único de validação | **Sim** | Não | Sim | Parcial | Não |
| `AxisEntityId` pronto (UUID v7) | **Sim** | Não | Não | Não | Não |
| Zero payload em runtime (só source generator) | **Sim** | n/a | n/a | Sim | Sim |
| Zero dependências NuGet | **Sim** | n/a | Sim | Sim | Sim |

---

## Veja também

- [O gerador `[ValueObject]`](value-object-generator.md) — o source generator no coração da package
- [Primeiros passos](getting-started.md) — instale e escreva seu primeiro value object
- [Referência da API](api-reference.md) — cada tipo e membro num só lugar

---

↩ [Voltar à documentação do AxisTypes](README.md)
