# Validadores americanos · `AxisValidator.Usa`

> Uma package de localização: um validador de SSN, um formatador de telefone dos EUA (NANP), a fachada estática `UsaValidator` para `FormatPhone` e `ValidateSsn`, mais um `RandomUsaDataHelper` para gerar dados de teste.

```csharp
using AxisValidator.Usa;

SsnValidator.Validate("512-45-6789");                 // true / false
PhoneValidator.Format("2125551234");                  // "(212) 555-1234"
UsaValidator.ValidateSsn("...");                       // AxisResult<string>
UsaValidator.FormatPhone("2125551234");                // AxisResult<string>
```

---

## Quando usar

Em qualquer lugar onde seu código aceita inputs americanos e você quer **validação tipada** + **formatação canônica**. Os métodos estáticos de `UsaValidator` retornam um `AxisResult<string>` e plugam direto num pipeline; os validators por baixo são funções puras que você chama de um `RequiredTryParse` em `AxisValidatorBase`.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| validar documentos não-americanos (CPF brasileiro, IDs da UE) | [Validadores brasileiros](brazil.md) ou uma package de localização diferente |
| extrair detalhes do documento (estado emissor, tipo de entidade) | não é objetivo desta package — escreva seu próprio helper |

---

## `SsnValidator`

| Membro | Assinatura | Descrição |
|---|---|---|
| `Validate(ssn)` | `static bool Validate(string? ssn)` | true se `ssn` for um SSN de 9 dígitos sintaticamente válido; rejeita faixas de área/grupo/serial estruturalmente inválidas e as sequências sabidamente inválidas (ex.: `"000000000"`, `"666123456"`, `"078051120"`) |

Diferente do CPF, o SSN **não tem algoritmo público de dígito verificador** — a SSA (Social Security Administration) nunca publicou um. `Validate` é uma checagem estrutural (área `!= 000/666` e `< 900`, grupo `!= 00`, serial `!= 0000`) mais uma lista de bloqueio de sequências sabidamente inválidas, não um checksum. Ela não garante que o SSN foi realmente emitido.

## `PhoneValidator`

| Membro | Assinatura | Descrição |
|---|---|---|
| `Format(phone)` | `static string? Format(string? phone)` | retorna `"(NPA) NXX-XXXX"` ou `null` se o input não puder ser normalizado para um número NANP válido |
| `OnlyNumbers(phone)` | `static string? OnlyNumbers(string? phone)` | normaliza uma string de telefone num formato local de 10 dígitos ou `null` |
| `TryFormat(phone, out formatted)` | `static bool TryFormat(string?, out string?)` | companheiro não-lançante de `Format` |

Segue o North American Numbering Plan: 10 dígitos, código de área e código de troca (exchange) começando ambos em `2`-`9`; um código de país `1` à esquerda (11 dígitos no total) é removido automaticamente.

## `UsaValidator`

A fachada estática que embrulha os validators puros na ferrovia `AxisResult<string>`.

| Membro | Assinatura | Comportamento |
|---|---|---|
| `FormatPhone(phone)` | `static AxisResult<string> FormatPhone(string? phone)` | retorna `AxisResult.Ok(formatted)` com o canônico `"(NPA) NXX-XXXX"`; `AxisError.ValidationRule("PHONE_NUMBER_NULL_OR_NOT_VALID")` para input não parseável |
| `ValidateSsn(document)` | `static AxisResult<string> ValidateSsn(string? document)` | retorna `AxisResult.Ok(document)` quando o SSN é estruturalmente válido; `AxisError.ValidationRule("DOCUMENT_INVALID")` se for ruim |

## `RandomUsaDataHelper`

> Vive no namespace `AxisValidator.Usa.Helpers` — um nível abaixo de `SsnValidator`/`PhoneValidator`/`UsaValidator` (todos em `AxisValidator.Usa`). Adicione `using AxisValidator.Usa.Helpers;` para usá-lo.

| Membro | Assinatura | Descrição |
|---|---|---|
| `GenerateSsn(format = false)` | `static string GenerateSsn(bool format = false)` | um SSN aleatório estruturalmente válido (cru ou `"512-45-6789"`) para testes |

---

## Exemplos reais

### 1. SSN dentro de um validator

```csharp
public class CreatePersonValidator : AxisValidatorBase<CreatePersonCommand>
{
    public CreatePersonValidator()
    {
        RequiredTryParse(x => x.Ssn, "PERSON_SSN_INVALID", ssn => SsnValidator.Validate(ssn as string));
    }
}
```

**Por que compensa:** o check de SSN anda no mesmo behaviour do pipeline que todo o resto. Um SSN ruim vira `AxisError.ValidationRule("PERSON_SSN_INVALID")`, exatamente como qualquer outro campo.

### 2. Formatação de telefone num pipeline

```csharp
return await uow.InTransactionAsync(() =>
    UsaValidator.FormatPhone(input.Phone)                 // AxisResult<string>
        .ThenAsync(formatted => contactWriter.UpsertAsync(personId, formatted)));
```

**Por que compensa:** a regra de formato e o passo de persistência compartilham a ferrovia. Se o telefone for inválido, o writer nunca é chamado.

### 3. SSN realista em testes unitários

```csharp
using AxisValidator.Usa.Helpers;   // RandomUsaDataHelper vive um namespace abaixo

var fake = RandomUsaDataHelper.GenerateSsn();    // 9 dígitos crus
Assert.True(SsnValidator.Validate(fake));
```

**Por que compensa:** testes não hard-codeiam um único SSN que pode eventualmente colidir com seed data; toda execução usa um fresco e estruturalmente válido.

---

## Veja também

- [Base e regras do validador](validator-base.md) — `RequiredTryParse` é a ponte para esses validators
- [O contrato `IAxisValidator<T>`](iaxisvalidator.md) — o que seu validator retorna
- [Validadores brasileiros](brazil.md) — o outro pack de localização, mesma forma

---

↩ [Voltar à documentação do AxisValidator](README.md)
