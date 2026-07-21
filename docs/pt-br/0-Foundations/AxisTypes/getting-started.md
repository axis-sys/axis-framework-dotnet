# Primeiros passos · instalação e uso

> Instale a package, use os identificadores prontos e declare seu primeiro value object customizado — o mínimo para decolar em poucos minutos.

---

## Instalação

```
dotnet add package AxisTypes                     # AxisEntityId pronto para uso
dotnet add package AxisTypes.SourceGenerator      # o atributo [ValueObject] + o gerador, para seus próprios value objects
```

O `AxisTypes` já traz o `AxisEntityId` pré-gerado. Se você só consome o `AxisEntityId`, é só isso que precisa. O próprio atributo `[ValueObject]` não faz parte desse package — quem o emite é o gerador Roslyn. Para declarar um value object que **você** escreve (como o `OrderNumber` abaixo), referencie diretamente o `AxisTypes.SourceGenerator` — é uma `DevelopmentDependency` (apenas analyzer), emitida em tempo de compilação e nunca embarcada no seu binário de saída. Essa referência é o único pré-requisito; você não precisa do package `AxisTypes` para isso.

---

## Usando os tipos prontos

```csharp
using Axis;

// AxisEntityId — o id de toda entidade
AxisEntityId personId = AxisEntityId.New;    // UUID v7

// Roundtrip implícito com string — para colunas do banco, headers, JSON
string raw = personId;             // "01927a8b-3c5e-7..."
AxisEntityId back = raw;           // parse implícito
```

> A conversão implícita a partir de `string` é **estrita** por design. `AxisEntityId _ = "invalid";` lança — veja [Por que AxisTypes?](why-axistypes.md).

---

## Declarando um value object customizado

```csharp
using AxisTypes.SourceGenerator;

[ValueObject]
public readonly partial record struct OrderNumber
{
    private string Value { get; }

    private OrderNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("OrderNumber cannot be empty");

        Value = value.ToUpperInvariant();
    }
}
```

O gerador emite:

```csharp
public readonly partial record struct OrderNumber
{
    public static implicit operator string(OrderNumber value) => value.Value;
    public override string ToString() => Value;
    public static implicit operator OrderNumber(string? value) => new(value);
    public bool Equals(OrderNumber other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public static bool TryParse(object? s) => TryParse(s, out _);
    public static bool TryParse(object? s, out OrderNumber result) { /* ... */ }
}
```

> Quer ajustar o nome da propriedade, desligar o parse implícito ou usar igualdade invariant-culture? Veja **[O gerador `[ValueObject]`](value-object-generator.md)**.

---

## Inspecionando e parseando

```csharp
// Roundtrip
var id = AxisEntityId.New;
string asString = id;                  // implícito → string
AxisEntityId again = asString;          // implícito ← string

// Parse não-lançante (útil para model binding HTTP, query strings)
if (AxisEntityId.TryParse(rawInput, out var parsed))
    Console.WriteLine($"Got {parsed}");
else
    Console.WriteLine("Identity inválido");

// Igualdade case-insensitive
OrderNumber a = "ORD-1";                // parse implícito
OrderNumber b = "ord-1";                // parse implícito
a == b;   // true
```

**Por que compensa:** o construtor roda a validação **uma vez**, quando o valor entra no domínio. Depois disso, o próprio sistema de tipos prova o invariante — você nunca mais checa `if (string.IsNullOrWhiteSpace(...))` lá adiante.

---

## Veja também

- [O gerador `[ValueObject]`](value-object-generator.md) — cada opção do atributo e o código que ele emite
- [`AxisEntityId`](axis-entity-id.md) — identificador tipado UUID v7
- [Por que AxisTypes?](why-axistypes.md) — primitive obsession em detalhe
- [Referência da API](api-reference.md) — cada tipo e membro num só lugar

---

↩ [Voltar à documentação do AxisTypes](README.md)
