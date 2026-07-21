# Por que AxisStorage? · comparação

> Há outras maneiras de falar com object storage a partir de .NET. Esta página diz por que o AxisStorage é diferente — uma comparação direta, sem mão na cintura.

---

## vs. `IAmazonS3` (direto)

O SDK da AWS é o carro-chefe, e o `AxisStorage.CloudflareR2` o usa internamente. Chamá-lo direto a partir de handlers tem três problemas:

1. O SDK lança — cada site precisa de `try/catch` ou aceita crashes.
2. `BucketName`, `RegionEndpoint` e a fiação de credenciais vazam para cada chamador.
3. Testes têm que mockar `IAmazonS3` com sua superfície enorme, ou rodar contra um bucket de verdade.

**AxisStorage** retorna `AxisResult`, esconde os conceitos do fornecedor atrás do adapter e deixa seus testes usarem uma implementação de disco local.

## vs. `BlobContainerClient` (Azure)

Mesmos trade-offs do SDK da AWS: API que lança, conceitos do fornecedor em toda parte, doloroso de mockar. A superfície de cinco métodos do AxisStorage encaixa nos dois backends — o código da sua aplicação não muda quando você migra.

## vs. `FileExtensions.WriteAllBytesAsync`

A abordagem "sem abstração" para disco local. OK até você ter que ir para a nuvem — aí cada site muda. `AxisStorage` mantém a opção de disco local aberta (escreva um adapter custom) sem vazá-la para os handlers.

## vs. um `IFileService` caseiro

DIY. Mesma forma do `IAxisStorage`, mas você escreve o contrato, o adapter, a semântica de streaming e o tratamento de falha por conta própria. `IAxisStorage` poupa o custo — e herda a história de ferrovia do `AxisResult`.

---

## A comparação

| Característica | AxisStorage | `IAmazonS3` direto | `BlobContainerClient` direto | `IFileService` caseiro |
|---|:--:|:--:|:--:|:--:|
| Retorna `AxisResult` | **Sim** | Não | Não | Talvez |
| Conceitos do fornecedor escondidos no adapter | **Sim** | Não | Não | Sim |
| Superfície de cinco métodos, fácil de mockar | **Sim** | Não | Não | Sim |
| Troca R2 ↔ Azure Blob sem mudar a aplicação | **Sim** | Não | Não | Sim |
| Streaming up e down | **Sim** | Sim | Sim | Talvez |
| URLs pré-assinadas como operação first-class | **Sim** | Sim (verboso) | Sim (verboso) | Talvez |
| Adapter S3-compatível embarcado | **Sim** | n/a | n/a | Não |
| Cancelamento implícito via `AxisMediator` | **Sim** | Não | Não | Talvez |

---

## Veja também

- [O contrato `IAxisStorage`](iaxisstorage.md) — a superfície
- [Adapter Cloudflare R2](cloudflare-r2.md) — a implementação na caixa
- [Adapter custom](custom-adapter.md) — escreva um para seu backend

---

↩ [Voltar à documentação do AxisStorage](README.md)
