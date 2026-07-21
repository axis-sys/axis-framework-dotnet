# Identidade · `AxisEntityId`

> Um identificador tipado para toda entidade de domínio, codificado como um **UUID v7**. UUID v7 significa que o id é **ordenável por tempo**, o que é amigável a índices, paginação e logs.

```csharp
var personId = AxisEntityId.New;   // "01927a8b-3c5e-7..."

string column   = personId;       // implícito → "01927a8b-3c5e-7..."
AxisEntityId back = column;       // implícito ← roundtrip
```

---

## Quando usar

A chave primária de toda entidade persistida. Em qualquer lugar onde você escreveria `Guid Id { get; set; }`.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| construir um id de string tipado com **formato próprio** | o [gerador `[ValueObject]`](value-object-generator.md) |

---

## Anatomia do valor

```
01927a8b-3c5e-7c63-8ff0-9da76e5db0a3
└─ UUID v7 (ordenável por tempo; os primeiros bytes codificam um timestamp em ms)
```

O construtor força cada parte desse layout. Entradas inválidas lançam na construção, e `TryParse` retorna `false`.

| Validação | Comportamento |
|---|---|
| `null` ou whitespace | `ArgumentNullException` |
| não é um GUID válido | `ArgumentException` |
| GUID não é UUID v7 | `ArgumentException` |

---

## Membros

| Membro | Assinatura | Descrição |
|---|---|---|
| `New` | `static AxisEntityId New { get; }` | cunha um UUID v7 novo |
| `ToString()` | `string` (gerado) | a string UUID |
| `implicit operator string` | (gerado) | para colunas do banco, headers, serialização JSON |
| `implicit operator AxisEntityId(string?)` | (gerado) | parse estrito — lança em entrada inválida |
| `TryParse(object?)` | (gerado) | parse não-lançante — retorna `true`/`false` sem produzir o valor |
| `TryParse(object?, out AxisEntityId)` | (gerado) | parse não-lançante — retorna `false` em entrada inválida |
| `Equals` / `GetHashCode` | (gerado) | case-insensitive (`OrdinalIgnoreCase`) |

---

## Exemplos reais

### 1. Persistindo uma entidade

```csharp
public class Person
{
    public AxisEntityId PersonId { get; private set; }     // chave primária tipada

    public Person()                                        // ctor do EF Core
    { }

    public Person(AxisEntityId personId)
    {
        PersonId = personId;
    }
}

// Mapeamento EF Core (uma coluna string, graças ao operador implícito)
modelBuilder.Entity<Person>()
    .Property(p => p.PersonId)
    .HasConversion(id => (string)id, value => value);     // round-trip via string
```

**Por que compensa:** a coluna é `string`, a propriedade é `AxisEntityId`, e a conversão implícita cola os dois. Seu código lê tipado; o storage lê estável. UUID v7 mantém os inserts no fim da B-tree.

### 2. Parâmetro de rota HTTP (parse não-lançante)

```csharp
app.MapGet("/people/{id}", (string id, IPersonService svc) =>
{
    if (!AxisEntityId.TryParse(id, out var personId))
        return Results.BadRequest("INVALID_PERSON_ID");

    return svc.GetByIdAsync(personId);
});
```

**Por que compensa:** o parser recusa ids malformados **antes** de qualquer chamada ao banco. Dentro do handler, o id já é tipado — `IPersonService.GetByIdAsync(AxisEntityId)` não pode ser chamado com a coisa errada.

---

## Veja também

- [O gerador `[ValueObject]`](value-object-generator.md) — como os helpers de conversão e parsing são gerados
- [Por que AxisTypes?](why-axistypes.md) — por que identificadores tipados ganham de `Guid` e `string`

---

↩ [Voltar à documentação do AxisTypes](README.md)
