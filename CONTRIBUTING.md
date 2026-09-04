# Como contribuir

Este documento é a política de **KISS e DRY** aplicada *durante* o
desenvolvimento — não depois, quando o código já ficou ruim.

Ele vale a partir da primeira linha de C#. O label `kiss-dry` e o template de
refatoração continuam existindo, mas eles são a rede embaixo do trapézio: o
trabalho é não cair.

---

## Os gatilhos

São objetivos de propósito. Sem número, "esse método tá grande" vira discussão de
gosto, e a decisão acaba indo para quem fala mais alto. Com número, vira
constatação.

| Gatilho | Limite |
|---|---|
| Tamanho de arquivo | ~300 linhas |
| Tamanho de método | ~40 linhas |
| Parâmetros por método | 4 |
| Níveis de aninhamento | 3 |
| Repetição do mesmo bloco lógico | 3ª aparição |
| Interface com uma implementação | só com teste que a justifique |

Bateu qualquer um: **para e decide**.

---

## Quem mede

A política acima virou verificação automática na #75, em duas ferramentas com
**políticas de falha diferentes** — e a diferença é o ponto.

| O que | Onde | Política |
|---|---|---|
| Estilo, formatação, nome, `using` que sobrou | `.editorconfig` + `dotnet format` | **reprova o PR** |
| Tamanho, aninhamento, parâmetros, duplicação | `ferramentas/Gatilhos` | **avisa**, não reprova |

O estilo reprova porque a ferramenta conserta sozinha: `dotnet format` resolve, e
não há conversa a ter. Um gatilho de tamanho não tem conserto automático — ele
exige uma decisão sobre o desenho, e **gate que reprova build por um método de 42
linhas vira gate que todo mundo aprende a contornar**. A partir daí ele não mede
mais nada.

Na sua máquina:

```bash
dotnet format --verify-no-changes --exclude src/Copiloto.Api/Persistencia/Migrations
dotnet run --project ferramentas/Gatilhos -- .
```

O medidor lê o código com o **Roslyn**, e não com expressão regular: chave dentro
de string interpolada e de comentário existe neste repositório, e contagem por
texto erraria em silêncio — produzindo um número plausível, que é o pior tipo de
número.

Ele mede o repositório **inteiro**, e não só o seu diff. Gate que só vale para
código novo deixa o passivo existente invisível para sempre.

---

## A regra dos três

**Não abstraia na segunda ocorrência.**

Duas coisas parecidas ainda não mostraram qual é o padrão entre elas. Abstrair ali
normalmente produz a abstração errada — e abstração errada é mais cara de desfazer
que duplicação, porque a duplicação está à vista e a abstração errada está
escondida atrás de um nome que parece certo.

Na terceira aparição o padrão já apareceu. Aí sim.

---

## Duplicação acidental não é duplicação

Dois trechos idênticos que **mudam por razões diferentes** não são duplicação: são
coincidência.

Unificar acopla duas regras que precisam evoluir separadas, e a próxima mudança vai
exigir um `if` para diferenciar — que é a duplicação de volta, só que pior, porque
agora está escondida dentro de uma abstração.

**O teste, antes de unificar:**

> *"Se um desses mudar, o outro tem que mudar junto, sempre?"*

Se a resposta não for um sim claro, deixa duplicado.

---

## Quando KISS e DRY brigam, KISS ganha

Código direto e repetido é melhor que código elegante que ninguém entende.

DRY existe para **reduzir custo de manutenção**. Quando a desduplicação aumenta
esse custo, ela perdeu o próprio propósito e virou enfeite.

---

## Onde este projeto já abre exceção, de propósito

`IConversationSource`, `IModelProvider`, `IDistributedState` e `IQueue` nascem com
**mais de uma implementação real** (#17, #27, #66) — Fake/WAHA/CloudAPI,
Fake/provedor real, InMemory/Redis, Channel/RabbitMQ.

Não são abstração prematura. A regra é *"interface sem segunda implementação e sem
teste que a justifique"*, e essas têm as duas coisas.

Registrar isso aqui evita que a própria política seja usada contra decisões que já
foram tomadas com motivo. Quando cada interface nascer, o motivo dela vai escrito
no XML doc, junto do código — não só nesta lista.

---

## O que fazer ao bater um gatilho

**Abra issue com `kiss-dry` + `refatoracao` e SIGA o trabalho.**

Não refatore no meio de outra tarefa. Dois motivos:

1. Refatoração misturada com feature produz um diff que ninguém consegue revisar —
   e esconde mudança de comportamento dentro de mudança cosmética.
2. Quem decide a ordem das coisas é o dono do projeto, não quem esbarrou no
   arquivo.

**Exceção (escoteiro limitado):** limpeza trivial no arquivo que você já está
tocando — renomear variável obscura, remover código morto, apagar comentário óbvio
— pode ir junto, desde que em **commit separado**.

---

## Comentário e nomeação

- Comentário explica **por quê**, nunca **o quê**.
- Código morto é removido, não comentado. O histórico do git guarda.
- Nome que precisa de comentário para ser entendido é nome errado.

---

## O fluxo

Uma issue por branch, uma branch por PR. Trabalho que não tem issue: abra a issue
antes.

```bash
git switch -c 17-conversation-source     # <número>-<slug>
# ... commits ...
gh pr create --fill                      # com "Closes #17" no corpo
```

**Mensagem de commit:** `tipo: o efeito`, em português sem acento, descrevendo **o
que mudou para quem usa** — não quais arquivos foram editados. O corpo conta o
achado, a causa e por que a correção foi feita ali.

```
feat: Redis e RabbitMQ atras de interface, com in-memory como padrao
```

**Nada entra na `main` sem a esteira verde.** Build e teste são trabalho do CI
(#92), não de conferência manual e não de agente: a esteira não esquece, roda igual
para todo mundo e não interpreta mensagem de compilador.

---

## O que a esteira mede, e como ela falha

| Gate | Política de falha |
|---|---|
| `build` + `test` (#92) | reprova o PR |
| Varredura de segredo (#47) | reprova o PR |
| Estilo e formatação (#75) | reprova o PR |
| Gatilho de tamanho e duplicação (#75) | **avisa e exige issue aberta**, não reprova |

A última linha é deliberada. Um gate que reprova o build por um método de 42 linhas
vira um gate que todo mundo aprende a contornar — e a partir daí ele não mede mais
nada.
