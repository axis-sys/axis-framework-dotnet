# Validadores brasileiros · `AxisValidator.Brazil`

> Uma package de localização: um validador de CPF, um formatador de celular brasileiro, a fachada estática `BrazilValidator` para `FormatCellphone` e `ValidateCpf`, mais um `RandomBrazilianDataHelper` para gerar dados de teste.

```csharp
using AxisValidator.Brazil;

CpfValidator.Validate("123.456.789-00");              // true / false
CellphoneValidator.Format("11987654321");             // "(11) 98765-4321"
BrazilValidator.ValidateCpf("...");                   // AxisResult<string>
BrazilValidator.FormatCellphone("11987654321");       // AxisResult<string>
```

---

## Quando usar

Em qualquer lugar onde seu código aceita inputs brasileiros e você quer **validação tipada** + **formatação canônica**. Os métodos estáticos de `BrazilValidator` retornam um `AxisResult<string>` e plugam direto num pipeline; os validators por baixo são funções puras que você chama de um `RequiredTryParse` em `AxisValidatorBase`.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| validar documentos não-brasileiros (SSN dos EUA, IDs da UE) | [Validadores americanos](usa.md) ou uma package de localização diferente |
| extrair detalhes do documento (gênero, região) | não é objetivo desta package — escreva seu próprio helper |

---

## `CpfValidator`

| Membro | Assinatura | Descrição |
|---|---|---|
| `Validate(cpf)` | `static bool Validate(string? cpf)` | true se `cpf` for sintaticamente + check-digit-válido; rejeita as sequências sabidamente inválidas (ex.: `"00000000000"`, `"12345678909"`) |

Função pura — sem alocação além de uma única string normalizada de dígitos.

## `CellphoneValidator`

| Membro | Assinatura | Descrição |
|---|---|---|
| `Format(cellphone)` | `static string? Format(string? cellphone)` | retorna `"(DD) 9NNNN-NNNN"` ou `null` se o input não puder ser normalizado para um celular brasileiro válido |
| `OnlyNumbers(cellphone)` | `static string? OnlyNumbers(string? cellphone)` | normaliza uma string de telefone num formato local de 11 dígitos ou `null` |
| `TryFormat(cellphone, out formatted)` | `static bool TryFormat(string?, out string?)` | companheiro não-lançante de `Format` |

## `BrazilValidator`

A fachada estática que embrulha os validators puros na ferrovia `AxisResult<string>`.

| Membro | Assinatura | Comportamento |
|---|---|---|
| `FormatCellphone(phone)` | `static AxisResult<string> FormatCellphone(string? phone)` | retorna `AxisResult.Ok(formatted)` com o canônico `"(DD) 9NNNN-NNNN"`; `AxisError.ValidationRule("CELLPHONE_NUMBER_NULL_OR_NOT_VALID")` para input não parseável |
| `ValidateCpf(document)` | `static AxisResult<string> ValidateCpf(string? document)` | retorna `AxisResult.Ok(document)` quando o CPF é válido; `AxisError.ValidationRule("DOCUMENT_INVALID")` se o CPF for ruim |

## `RandomBrazilianDataHelper`

> Vive no namespace `AxisValidator.Brazil.Helpers` — um nível abaixo de `CpfValidator`/`CellphoneValidator`/`BrazilValidator` (todos em `AxisValidator.Brazil`). Adicione `using AxisValidator.Brazil.Helpers;` para usá-lo.

| Membro | Assinatura | Descrição |
|---|---|---|
| `GenerateCpf(format = false)` | `static string GenerateCpf(bool format = false)` | um CPF aleatório válido (cru ou `"123.456.789-09"`) para testes |

---

## Exemplos reais

### 1. CPF dentro de um validator

```csharp
public class CreatePersonValidator : AxisValidatorBase<CreatePersonCommand>
{
    public CreatePersonValidator()
    {
        RequiredTryParse(x => x.Cpf, "PERSON_CPF_INVALID", cpf => CpfValidator.Validate(cpf as string));
    }
}
```

**Por que compensa:** o check de CPF anda no mesmo behaviour do pipeline que todo o resto. Um CPF ruim vira `AxisError.ValidationRule("PERSON_CPF_INVALID")`, exatamente como qualquer outro campo.

### 2. Formatação de celular num pipeline

```csharp
return await uow.InTransactionAsync(() =>
    BrazilValidator.FormatCellphone(input.Phone)        // AxisResult<string>
        .ThenAsync(formatted => contactWriter.UpsertAsync(personId, formatted)));
```

**Por que compensa:** a regra de formato e o passo de persistência compartilham a ferrovia. Se o telefone for inválido, o writer nunca é chamado.

### 3. CPF realista em testes unitários

```csharp
using AxisValidator.Brazil.Helpers;   // RandomBrazilianDataHelper vive um namespace abaixo

var fake = RandomBrazilianDataHelper.GenerateCpf();    // 11 dígitos crus
Assert.True(CpfValidator.Validate(fake));
```

**Por que compensa:** testes não hard-codeiam um único CPF que pode eventualmente colidir com seed data; toda execução usa um fresco e válido.

---

## Veja também

- [Base e regras do validador](validator-base.md) — `RequiredTryParse` é a ponte para esses validators
- [O contrato `IAxisValidator<T>`](iaxisvalidator.md) — o que seu validator retorna

---

↩ [Voltar à documentação do AxisValidator](README.md)
