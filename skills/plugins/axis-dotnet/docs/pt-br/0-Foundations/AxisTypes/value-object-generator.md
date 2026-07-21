# O gerador `[ValueObject]` · `ValueObjectAttribute`

> Anote um `partial record struct`, `partial struct`, `partial record` ou `partial class` com `[ValueObject]` e o source generator Roslyn escreve todo o boilerplate de conversão, igualdade e parsing. Você escreve o construtor; o gerador escreve tudo o mais.

```csharp
using AxisTypes.SourceGenerator;

[ValueObject]
public readonly partial record struct InvoiceNumber
{
    private string Value { get; }

    private InvoiceNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("InvoiceNumber cannot be empty");

        Value = value;
    }
}
```

---

## Quando usar

Sempre que uma `string` (ou qualquer outro primitivo) **não** for o tipo certo para um conceito de domínio. Ids de cliente, números de pedido, códigos de país, números de documento, códigos de moeda, endereços de e-mail, números de telefone — qualquer coisa que você queira que o compilador rastreie.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| representar um conceito de domínio multi-campo (preço = valor + moeda) | um `readonly record struct` escrito à mão |
| construir um conjunto fixo estilo enum | um `enum` de verdade ou uma biblioteca de Smart Enum |
| validar sem lançar | chame `TryParse`; não lance dentro do construtor para entradas não-domínio |

---

## O que é gerado

Lendo o `ValueObjectGenerator.cs` diretamente, isto é o que o gerador emite para cada tipo:

| Membro | Sempre | Controlado por |
|---|:--:|---|
| `static implicit operator string(MyType value)` | **Sim** | — |
| `override string ToString()` | **Sim** | — |
| `static implicit operator MyType(string? value)` | opcional | `ImplicitFromString` (padrão `true`) |
| `bool Equals(MyType other)` (case-insensitive) | opcional | `CaseInsensitiveEquals` (padrão `true`) |
| `override int GetHashCode()` (case-insensitive) | opcional | `CaseInsensitiveEquals` (padrão `true`) |
| `static bool TryParse(object?)` e `TryParse(object?, out MyType)` | opcional | `TryParse` (padrão `true`) |

A variante case-insensitive usa `OrdinalIgnoreCase` por padrão e muda para `InvariantCultureIgnoreCase` quando `UseInvariantCulture = true`.

---

## As cinco opções

| Opção | Tipo | Padrão | Significado |
|---|---|---|---|
| `PropertyName` | `string` | `"Value"` | o nome da propriedade envelopada que o gerador lê quando emite `ToString`, `Equals` etc. Mude quando seu value object exponha o dado envelopado sob outro nome (ex.: `Code`). |
| `ImplicitFromString` | `bool` | `true` | quando `true`, emite `implicit operator MyType(string?)` para `MyType x = "abc"` funcionar. Desligue quando o tipo só aceitar construção via fábrica. |
| `TryParse` | `bool` | `true` | emite os dois overloads de `TryParse`. Desligue se você implementar o parsing manualmente. |
| `CaseInsensitiveEquals` | `bool` | `true` | emite `Equals` + `GetHashCode` case-insensitive. Desligue para identificadores case-sensitive (ex.: hashes criptográficos). |
| `UseInvariantCulture` | `bool` | `false` | quando a igualdade case-insensitive está ligada, muda `OrdinalIgnoreCase` → `InvariantCultureIgnoreCase`. Use para tokens em linguagem humana onde o folding consciente de locale importa. |

```csharp
// Estrito, case-sensitive, sem parse implícito — para um token criptográfico
[ValueObject(
    ImplicitFromString    = false,
    CaseInsensitiveEquals = false,
    TryParse              = false)]
public readonly partial record struct ApiKey
{
    private string Value { get; }
    private ApiKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("empty");
        Value = value;
    }

    public static ApiKey FromBase64(string b64) => new(b64); // fábrica explícita
}
```

---

## Exemplo real — número de pedido tipado com validação

```csharp
[ValueObject]
public readonly partial record struct OrderNumber
{
    private string Value { get; }

    private OrderNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("OrderNumber cannot be empty");

        if (value.Length is < 6 or > 32)
            throw new ArgumentException("OrderNumber length must be 6..32");

        Value = value.ToUpperInvariant();
    }
}

// Uso
OrderNumber n = "ord-12345";              // parse implícito → uppercased internamente
string column   = n;                       // implícito para string → "ORD-12345"
OrderNumber other = "ORD-12345";           // parse implícito
bool sameOrder  = n == other;              // true (case-insensitive)

// Model binding HTTP — não-lançante
if (OrderNumber.TryParse(rawQueryString, out var orderNumber))
    return await GetByOrderAsync(orderNumber);
```

**Por que compensa:** cada ponto de entrada (construtor, cast implícito, `TryParse`) passa pela **mesma** validação. O compilador recusa `string` onde se espera `OrderNumber`, e o código a jusante nunca revalida.

---

## Veja também

- [`AxisEntityId`](axis-entity-id.md) — o exemplo canônico de identificador tipado
- [Por que AxisTypes?](why-axistypes.md) — o que o gerador compra em relação ao código escrito à mão

---

↩ [Voltar à documentação do AxisTypes](README.md)
