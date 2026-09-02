# Roteiro da #75 — gate de KISS e DRY no CI

Este arquivo é o ponto de partida do trabalho na branch `75-gate-kiss-dry-ci`.
Ele existe porque a issue diz *o que* precisa existir, e não *em que ordem
descobrir*. Quando o gate estiver de pé, **este arquivo sai junto no PR** — ele é
andaime, não documentação do projeto.

Leia antes de começar: [`CONTRIBUTING.md`](../CONTRIBUTING.md). Os limites
numéricos e a política de falha moram lá e **não são repetidos aqui** de
propósito — número copiado em dois lugares fica desatualizado num deles.

---

## O que já está de pé, e o que não está

| Peça | Situação |
|---|---|
| `.github/workflows/ci.yml` | existe — compila e testa |
| `.github/workflows/segredo.yml` | existe — varredura de segredo, **é o molde a copiar** |
| `Directory.Build.props` | existe, e tem um comentário reservando o lugar da política de analisador |
| `.editorconfig` | **não existe** — é a primeira entrega |
| Workflow do gate KISS/DRY | **não existe** |

O `Directory.Build.props` foi escrito prevendo esta issue. Abra e leia o
comentário do topo antes de mexer: ele diz por que a política de analisador ficou
de fora até agora.

---

## A armadilha desta issue

O critério de aceite nº 1 é *"ferramentas validadas no terminal e versões
registradas na issue"*.

Isso não é burocracia. É a regra mais cara de aprender: **não se adota pacote de
memória.** Nome de analisador que "todo mundo usa" costuma estar sem manutenção há
dois anos, ou não suportar .NET 9, ou ter mudado de nome. Descobrir isso depois de
o CSPROJ já estar commitado custa muito mais caro do que descobrir agora.

Então a ordem é: **descobrir → medir → decidir → só então fixar no arquivo.**

Este roteiro deliberadamente **não te diz qual ferramenta usar.** Escolher é a
parte da issue que é sua.

---

## Passo 1 — Ver o terreno com os próprios olhos

Antes de qualquer ferramenta, meça o código que já existe. Sem isso você calibra
limiar no escuro.

```bash
# maiores arquivos .cs do projeto
find src testes -name '*.cs' | xargs wc -l | sort -rn | head -20

# quantos arquivos existem ao todo
find src testes -name '*.cs' | wc -l
```

Anote os números. Se o maior arquivo do projeto tem 120 linhas, um limiar de 300
não vai acusar nada e o gate nasce decorativo — vale registrar isso na issue.

---

## Passo 2 — Descobrir o que existe para .NET 9

O SDK já traz analisadores embutidos. Comece por eles antes de sair instalando
pacote:

```bash
dotnet --version
dotnet format --help
dotnet build -warnaslogger 2>/dev/null || dotnet build --nologo
```

Perguntas a responder **no terminal**, não no chute:

- O que o `dotnet format` já verifica sozinho, sem nenhum pacote?
- Quais regras o SDK liga por padrão, e qual severidade elas têm hoje?
- Para os gatilhos numéricos (tamanho, aninhamento, complexidade): existe regra
  nativa, ou isso precisa de ferramenta externa?
- Para duplicação: o que existe que roda em CI de repositório público sem custo?

Toda ferramenta candidata: verifique a **data do último release** e se ela declara
suporte a .NET 9. Registre versão exata na issue. Nada de `:latest` — o
`segredo.yml` explica por quê num comentário, vale a pena ler.

---

## Passo 3 — `.editorconfig` na raiz

Primeira entrega concreta. Estilo e formatação **reprovam o PR**, então esta parte
tem que estar sólida antes de ligar o gate.

Cuidado com o efeito manada: ligar 400 regras de uma vez faz o primeiro `dotnet
format` acusar milhares de pontos, e aí ninguém lê o relatório. Comece pelo que o
projeto já pratica na prática, e cresça depois.

Feito quando: `dotnet format --verify-no-changes` passa limpo no código atual.

---

## Passo 4 — O workflow

Copie a **forma** do `.github/workflows/segredo.yml`: workflow próprio, `permissions`
mínimo, `concurrency`, `timeout-minutes`, versão fixada, e comentário dizendo *por
que* cada decisão foi tomada.

Preste atenção na política de falha da tabela do `CONTRIBUTING.md` — ela tem duas
linhas diferentes, e confundir as duas é o erro que mata o gate:

- estilo e formatação → **reprova**
- tamanho e duplicação → **avisa**, não reprova

O motivo dessa assimetria está escrito na issue e no `CONTRIBUTING.md`. Se você não
concordar com ele, ótimo — argumente no PR. Só não inverta em silêncio.

---

## Passo 5 — Rodar no passivo existente

O último critério de aceite é o que separa gate de enfeite: rode no código que já
está lá e **abra uma issue para cada coisa que aparecer** (label `kiss-dry` +
`refatoracao`).

Não conserte nada agora. A #75 entrega o *medidor*; consertar o que ele mede é
trabalho de outra issue — e quem decide a ordem é o dono do projeto. Isso está no
`CONTRIBUTING.md`, em "O que fazer ao bater um gatilho".

---

## Como entregar

```bash
git switch 75-gate-kiss-dry-ci
# ... commits ...
gh pr create --fill        # com "Closes #75" no corpo
```

Convenção de commit do projeto: `tipo: o efeito`, em português **sem acento**,
descrevendo o que mudou para quem usa — não quais arquivos você editou. Exemplos
reais estão em `git log --oneline`.

Apague este arquivo no último commit do PR.

---

## Quando travar

Trave rápido e pergunte — não passe três dias num gate de CI. Especificamente,
**pare e pergunte** se:

- nenhuma ferramenta gratuita cobrir os gatilhos numéricos em repositório público
- o `.editorconfig` mínimo já acusar dezenas de pontos no código atual
- a política de "avisa mas não reprova" não tiver como ser expressa na ferramenta
  escolhida

Os três são decisão de dono de projeto, não de quem está implementando.
