# AxisTypes — Documentação

> 🌐 [English (README principal)](../../../en-us/0-Foundations/AxisTypes/README.md)

**Value objects fortemente tipados para C#** — uma package sem dependências e um source generator Roslyn `[ValueObject]` que entrega conversões implícitas, `ToString`, `Equals` case-insensitive e `TryParse` de graça, além do tipo pronto para uso `AxisEntityId` (UUID v7).

```csharp
// 1) Use uma identidade pronta no seu domínio
var personId = AxisEntityId.New;                         // UUID v7, ordenável por tempo
string raw   = personId;                                 // implícito → "01927a8b-..."
AxisEntityId back = raw;                                 // implícito ← roundtrip

// 2) Ou declare seu próprio value object — o gerador escreve o boilerplate
[ValueObject]
public readonly partial record struct OrderNumber
{
    private string Value { get; }
    private OrderNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("empty");
        Value = value.ToUpperInvariant();
    }
}
```

Use esta página como **mapa**: leia o tronco abaixo (~5 min) e salte direto para o detalhe do grupo que você precisa — sem ler centenas de linhas.

---

## O tronco (leia primeiro)

### Primitive obsession em 60 segundos

`string customerId` e `string orderId` são o mesmo tipo. O compilador não vai te impedir de passar um onde se espera o outro, de armazenar um como se fosse o outro, de logar o errado. **Value objects fortemente tipados** transformam o compilador em aliado: `CustomerId` não pode ser atribuído a `OrderId`, mesmo que ambos envelopem uma `string`. → **[Por que AxisTypes?](why-axistypes.md)**

### `[ValueObject]` em 60 segundos

Coloque o atributo num `partial record struct` (ou `partial class`) e o source generator escreve:

- `implicit operator string` — para logs, serialização, colunas do banco.
- `implicit operator MyType(string?)` — para parsing vindo de configuração, JSON, HTTP.
- `override ToString()` — devolve o valor envelopado.
- `bool Equals(other)` e `GetHashCode()` — **case-insensitive** por padrão (`OrdinalIgnoreCase`).
- `static bool TryParse(object?, out MyType)` — parse não-lançante para model binding HTTP.

Você escreve o **construtor** (a validação). Todo o resto é gerado. → **[O gerador `[ValueObject]`](value-object-generator.md)**

### O tipo pronto

- **`AxisEntityId`** — o id de toda entidade. UUID v7 (ordenável por tempo), criado com `AxisEntityId.New`. → **[`AxisEntityId`](axis-entity-id.md)**

### Instalação

```
dotnet add package AxisTypes                     # AxisEntityId pronto para uso
dotnet add package AxisTypes.SourceGenerator      # o atributo [ValueObject] + o gerador, para seus próprios value objects
```

O `AxisTypes` sozinho já traz o `AxisEntityId` pronto (já gerado) — mas **não** expõe o atributo `[ValueObject]`. Para declarar um value object que **você** escreve, referencie o gerador Roslyn em si, `AxisTypes.SourceGenerator` (uma `DevelopmentDependency`, apenas analyzer — nunca vai parar no seu binário de saída): ele emite o atributo `[ValueObject]` como um tipo `internal sealed` a cada compilação que o referencia, então o package `AxisTypes` nem é pré-requisito para isso.

→ Guia completo: **[Primeiros passos](getting-started.md)**

---

## O mapa (salte para o que precisa)

| Grupo | Você quer… | Detalhe |
|---|---|---|
| **Gerador · `[ValueObject]`** ⭐ | declarar um novo value object fortemente tipado | [value-object-generator.md](value-object-generator.md) |
| **Identidade · `AxisEntityId`** | um id tipado para toda entidade de domínio | [axis-entity-id.md](axis-entity-id.md) |
| **Por quê?** | o argumento contra a primitive obsession | [why-axistypes.md](why-axistypes.md) |
| **Referência** | cada tipo e membro num só lugar | [api-reference.md](api-reference.md) |

**Comece aqui:** [Primeiros passos](getting-started.md) · [O gerador `[ValueObject]`](value-object-generator.md) · [Por que AxisTypes?](why-axistypes.md)

**Fundamentos:** [`AxisEntityId`](axis-entity-id.md)

**Referência e extras:** [Referência da API](api-reference.md)

---

## Princípios de design

1. **Identificadores são tipos, não strings.** O compilador recusa misturar `CustomerId` com `OrderId`. Primitive obsession é uma classe de bug, não preferência de estilo.
2. **O gerador paga o imposto do boilerplate.** Escrever cada value object à mão significa escrever cada operador à mão. `[ValueObject]` escreve uma vez, corretamente, sempre.
3. **Case-insensitive por padrão.** Identificadores e códigos viajam por configs, headers e querystrings; `OrdinalIgnoreCase` casa com a realidade.
4. **Identificadores ordenáveis por tempo.** UUID v7 mantém a ordem lexicográfica alinhada à ordem de criação — amigável a índices, paginação e logs.
5. **Sem alocações desnecessárias.** `readonly partial record struct` quer dizer que o invólucro tipado é um value type com igualdade estrutural, `GetHashCode` grátis e zero pressão no heap.

---

## Licença

Apache 2.0
