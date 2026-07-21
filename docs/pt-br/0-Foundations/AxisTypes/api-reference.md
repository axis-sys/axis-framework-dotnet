# Referência da API

> O catálogo completo, agrupado por responsabilidade. Use para consulta — cada grupo linka de volta à sua página de detalhe.

---

## O atributo do gerador

| Membro | Padrão | Descrição |
|---|---|---|
| `[ValueObject]` | — | anote um `partial record struct`, `partial struct`, `partial record` ou `partial class` para emitir os helpers de conversão / igualdade / parse |
| `PropertyName` | `"Value"` | o nome da propriedade envelopada que o gerador lê quando emite `ToString`/`Equals` |
| `ImplicitFromString` | `true` | emite `implicit operator MyType(string?)` |
| `TryParse` | `true` | emite `TryParse(object?)` e `TryParse(object?, out MyType)` |
| `CaseInsensitiveEquals` | `true` | emite `Equals` e `GetHashCode` case-insensitive |
| `UseInvariantCulture` | `false` | troca a igualdade case-insensitive para `InvariantCultureIgnoreCase` |

→ [O gerador `[ValueObject]`](value-object-generator.md)

---

## Membros emitidos pelo gerador

| Membro | Assinatura | Quando |
|---|---|---|
| Conversão implícita para string | `static implicit operator string(MyType)` | sempre |
| `ToString` | `override string ToString()` | sempre |
| Parse implícito | `static implicit operator MyType(string?)` | `ImplicitFromString = true` |
| `Equals` case-insensitive | `bool Equals(MyType)` (struct) ou `virtual bool Equals(MyType?)` (class) | `CaseInsensitiveEquals = true` |
| `GetHashCode` | `override int GetHashCode()` | `CaseInsensitiveEquals = true` |
| `TryParse` | `static bool TryParse(object?)` | `TryParse = true` |
| `TryParse` (out) | `static bool TryParse(object?, out MyType)` | `TryParse = true` |

→ [O gerador `[ValueObject]`](value-object-generator.md)

---

## `AxisEntityId`

| Membro | Assinatura | Descrição |
|---|---|---|
| `New` | `static AxisEntityId New { get; }` | cunha um UUID v7 novo |
| `ToString()` | `string` | a string UUID |
| `implicit operator string` | (gerado) | para storage e serialização |
| `implicit operator AxisEntityId(string?)` | (gerado) | parse estrito |
| `TryParse` | (gerado) | parse não-lançante |
| `Equals` / `GetHashCode` | (gerado, case-insensitive) | igualdade estrutural |

→ [`AxisEntityId`](axis-entity-id.md)

---

## Veja também

- [Primeiros passos](getting-started.md) — instale e use
- [Por que AxisTypes?](why-axistypes.md) — o argumento por value objects tipados
- [Documentação completa](README.md) — o mapa de toda a documentação

---

↩ [Voltar à documentação do AxisTypes](README.md)
