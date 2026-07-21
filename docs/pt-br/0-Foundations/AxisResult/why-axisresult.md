# Por que AxisResult? · comparação

> Existem outras libraries de Result para C#. Esta página diz por que o AxisResult é diferente — uma comparação direta, sem rodeios.

---

## vs. FluentResults

FluentResults é popular, mas limitada. Falta composição monádica — você não consegue encadear operações async sem desembrulhar manualmente. Não há suporte a `ValueTask`, nem composição de tuplas, nem padrões de recuperação, nem categorias de erro tipadas. É um contêiner, não uma ferrovia.

## vs. ErrorOr

ErrorOr oferece encadeamento básico, mas perde a profundidade necessária para sistemas em produção. Sem variantes `ValueTask`, sem `Zip` para combinar operações independentes, sem `Recover`/`RecoverWhen` para fallbacks condicionais, sem `RequireNotFound` para padrões de criação idempotente.

## vs. LanguageExt

LanguageExt é um framework de programação funcional de 7,5 MB. Se você só precisa de tipos Result, está carregando coleções imutáveis, monads State, monads Reader e uma API inteiramente não-idiomática. O AxisResult dá o poder de composição sem o peso nem a curva de aprendizado.

## vs. CSharpFunctionalExtensions

Library sólida, mas sem suporte a `ValueTask`, sem `Zip`, sem categorias de erro tipadas, sem padrões de recuperação, sem agregação paralela. É boa para padrões básicos de Result, mas fica aquém em cenários de domínio complexos.

## vs. Ardalis.Result

Desenhada para controllers ASP.NET, não para lógica de domínio. Suporte básico a `Map`/`Bind`, sem profundidade de composição, sem variantes async, sem recuperação. Ótima para mapear respostas HTTP, limitada em todo o resto.

---

## A comparação

| Feature | AxisResult | FluentResults | ErrorOr | LanguageExt | CSharpFunctExt |
|---------|:---:|:---:|:---:|:---:|:---:|
| Composição monádica (Then/Map) | **Sim** | Parcial | Sim | Sim | Sim |
| Async Task + ValueTask | **Sim** | Não | Não | Não | Não |
| Composição de tuplas (Zip) | **Até 4** | Não | Não | Sim | Não |
| Recuperação condicional (Recover/RecoverWhen) | **Sim** | Não | Não | Não | Não |
| Categorias de erro tipadas | **12 tipos** | Não | Parcial | Não | Não |
| Detecção de erro transiente | **Sim** | Não | Não | Não | Não |
| Padrão RequireNotFound | **Sim** | Não | Não | Não | Não |
| Sintaxe de query LINQ | **Sim** | Não | Parcial | Sim | Sim |
| Agregação paralela (Combine/All) | **Sim** | Não | Não | Sim | Não |
| Acúmulo de erros (OrElse) | **Sim** | Parcial | Não | Não | Não |
| Zero dependências externas | **Sim** | Parcial | Parcial | Não | Sim |
| Leve (~240 KB) | **Sim** | Sim | Sim | Não (7,5 MB) | Sim |
| Sobrecargas cientes de CancellationToken | **Sim** | Não | Não | Parcial | Não |
| Zip paralelo (ops independentes) | **Sim** | Não | Não | Parcial | Não |

---

## Veja também

- [Railway-Oriented Programming](railway-oriented-programming.md) — o modelo que essas features implementam
- [Primeiros passos](getting-started.md) — instalar e começar
- [Referência da API](api-reference.md) — o catálogo completo de operadores

---

↩ [Voltar à documentação do AxisResult](README.md)
