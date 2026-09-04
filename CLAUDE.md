# CLAUDE.md

Orientacao para o Claude Code (claude.ai/code) trabalhando neste repositorio.

O projeto se chama **Copiloto** (a pasta e `copiloto-crm/`). Codigo, documentacao,
commits e issues sao em **portugues** — mantenha. `README.md` e `docs/` usam
acento; **corpo de issue e mensagem de commit sao ASCII sem acento**, que e o
padrao ja estabelecido no historico.

---

## A tese, que manda em tudo

**O robo nao fala com o cliente.** Ele le a conversa e entrega ao vendedor um
dossie de contexto: estagio, objecao (mesmo velada) e — o mais util — o que
ainda **nao** sabemos sobre aquele cliente. Quem escreve a mensagem e o vendedor.

Isso nao e limitacao tecnica, e o produto. Sugestao de fala pronta falha **na
frente do cliente** e queima a confianca do vendedor para sempre; leitura de
conversa falha **na tela**, antes de qualquer dano — o vendedor discorda, ajusta
e segue usando.

Consequencia de engenharia: o sistema e otimizado para **precisao de leitura**,
nao para fluencia de escrita. Qualquer ideia que termine em "e ai o sistema manda
a mensagem" esta fora de escopo por decisao, nao por falta de tempo.

---

## Estado real do repositorio

> Esta secao envelhece rapido. **Quem fecha uma issue atualiza ela no mesmo PR.**

- **A solution existe**: `Copiloto.sln` com `Copiloto.Dominio` (POCO, sem pacote),
  `Copiloto.Api` e a suite `Copiloto.Testes`. Front ainda nao existe.
- Dominio: conversas e agrupamento de falas, dossie e sinais, ficha do cliente,
  planos e ancoragem, deal e lead, roteamento de modelo.
- Api: webhook do WhatsApp com fila, resolvedor de lead, EF Core + Postgres com
  migrations, PII Shield e guarda de saida, catalogo fake de ancoragem.
- `seed/conversas/` tem as tres conversas do cenario de cafe. `prompts/tecnicas/`
  continua **vazia**.
- Esteira: `.github/workflows/ci.yml` (build + test) e `segredo.yml`.
- **77 issues abertas, 16 fechadas**, em 9 milestones (M0 Fundacao -> M8 LGPD).
- Ordem acordada: **alicerce antes de morador** — estrutura primeiro, funcionalidade
  depois.

---

## Comandos

Todos valem: a solution existe e a suite roda offline.

```bash
dotnet build                              # compila a solution
dotnet test                               # suite xUnit
dotnet format --verify-no-changes         # estilo: reprova sem alterar arquivo
dotnet run --project src/Copiloto.Api     # sobe a API

docker compose up -d                      # so o Postgres (padrao)
docker compose --profile distribuido up -d  # + Redis e RabbitMQ
docker compose ps
```

`.env` **nao existe por padrao** — e `cp .env.example .env` e preencher.
`POSTGRES_PASSWORD` sem valor derruba o compose de proposito.

---

## Regras que nao se negociam

- **A IA nunca escreve para o cliente.** Ver a tese acima.
- **Regra de ancoragem.** Escassez, prova social, desconto, prazo e preco so
  viram sugestao se existir dado no CRM que sustente. Sem dado, o agente devolve
  como **pergunta ao vendedor**, nunca como fala pronta. Sugerir "restam 2
  unidades" quando existem 200 e publicidade enganosa, cria passivo para a
  empresa que usa o produto e queima o vendedor com o cliente (#15, #16).
  O dado vem de ferramenta consultada ANTES da fala (#57), e **dado que existe e
  nao sustenta a fala barra a sugestao igual a dado ausente**: com 140kg em
  estoque nao ha caminho de codigo que produza escassez.
- **Todo sinal do dossie cita a fala que o originou.** Sem citacao, o bloco nao e
  exibido. E o que transforma "a IA ta ruim" de reclamacao subjetiva em defeito
  verificavel.
- **`Copiloto.Dominio` nao tem `PackageReference`.** Nenhum. E a prova mecanica do
  dominio POCO da #48: sem pacote, o projeto **nao consegue** compilar um `[Table]`
  ou um `DbContext`. Ha teste que le o `.csproj` e reprova se um pacote aparecer.
- **Sem Semantic Kernel e sem LangChain.** Router, cascata e circuit breaker sao
  escritos a mao — um framework de orquestracao esconderia exatamente o que este
  projeto existe para mostrar.
- **Sem banco vetorial dedicado.** `pgvector` roda no Postgres que ja esta aqui,
  com o mesmo backup, a mesma transacao e a mesma credencial (#60).
- **PII mascarada antes de sair da rede**, com teste que **falha o build** se
  vazar em log ou payload (#43). E pseudonimizacao, nao anonimizacao: continua
  sendo dado pessoal sob a LGPD (#83).
- **Fake e o padrao.** `CONVERSATION_SOURCE=fake` e `MODEL_PROVIDER=fake`: a suite
  e a demo rodam **offline e de graca**. Teste que precisa de rede ou de chave de
  provedor esta errado por construcao.
- **Nenhuma dependencia adotada de memoria.** Antes de fixar pacote em `.csproj`
  ou em issue, validar no terminal e **registrar a versao verificada**.

---

## KISS e DRY: os gatilhos

Bateu qualquer um, para e decide (#74):

| Gatilho | Limite |
|---|---|
| Tamanho de arquivo | ~300 linhas |
| Tamanho de metodo | ~40 linhas |
| Parametros por metodo | 4 |
| Niveis de aninhamento | 3 |
| Repeticao do mesmo bloco logico | 3a aparicao |
| Interface com uma implementacao | so com teste que a justifique |

**A regra dos tres:** nao abstrair na segunda ocorrencia — duas coisas parecidas
ainda nao mostraram qual e o padrao entre elas, e abstracao errada e mais cara de
desfazer que duplicacao.

**Duplicacao acidental nao e duplicacao.** Teste antes de unificar: *"se um desses
mudar, o outro tem que mudar junto, sempre?"* Se nao for um sim claro, deixa
duplicado.

**Ao bater um gatilho: abre issue com `kiss-dry` + `refatoracao` e SEGUE o
trabalho.** Refatoracao misturada com feature produz diff que ninguem revisa.
Excecao: limpeza trivial no arquivo ja tocado, em **commit separado**.

**Excecoes conscientes ja decididas:** `IConversationSource`, `IModelProvider`,
`IDistributedState` e `IQueue` nascem com mais de uma implementacao **real**
(#17, #27, #66) — nao sao abstracao prematura.

---

## Fluxo de trabalho

**Uma issue por branch, uma branch por PR.** As issues sao o plano; trabalho
que nao tem issue, abre issue antes.

```bash
git switch -c 17-conversation-source     # <numero>-<slug>
# ... commits ...
gh pr create --fill                      # corpo com "Closes #17"
```

Commit: `tipo: o efeito`, em portugues sem acento, descrevendo **o que mudou para
quem usa** — nao quais arquivos foram editados. O corpo conta o achado, a causa e
por que a correcao foi feita ali.

```
feat: Redis e RabbitMQ atras de interface, com in-memory como padrao
```

---

## Documento que afirma o que o codigo faz precisa de quem confira

`README.md` e `docs/ARQUITETURA.md` descrevem router, ledger, MCP e RAG que **ainda
nao existem**. Hoje isso e tese declarada, e esta tudo bem. O que nao pode e virar
promessa desatualizada em silencio.

Regra: **ao fechar uma issue, se o documento passou a divergir do codigo, corrige
no mesmo PR.** Esses dois arquivos circulam fora do produto — sao a primeira coisa
que alguem le, e a ultima que alguem confere.

---

## Quem confere o que

**Build e teste sao trabalho da esteira, nao de agente.** O GitHub Actions roda
`dotnet build` e `dotnet test` em todo push e todo PR: ele nao esquece, nao
parafraseia mensagem de compilador, roda igual para todo mundo e nao custa token.
Agente rodando suite so repete — mais caro e menos confiavel — o que a esteira ja
faz de graca.

Os gates da esteira, cada um com issue propria:

| Gate | Politica de falha |
|---|---|
| `build` + `test` | reprova o PR |
| Varredura de segredo (#47) | reprova o PR |
| Estilo e formatacao (#75) | reprova o PR |
| Gatilho de tamanho e duplicacao (#75) | **avisa e exige issue aberta**, nao reprova |

A ultima linha e deliberada: gate que reprova build por um metodo de 42 linhas vira
gate que todo mundo aprende a contornar — e a partir dai ele nao mede mais nada.

**Nada entra na `main` sem a esteira verde.** E esteira que nao rodou nao e verde:
verde por nao ter olhado e pior que vermelho, porque vermelho manda consertar e
verde falso manda seguir.

O que sobra para o agente (Opus) e exatamente o que a esteira nao sabe fazer:
escrever o codigo, decidir desenho, e julgar se uma mudanca fere um invariante que
nenhum analisador conhece — a IA nao escreve ao cliente, o sinal cita a fala, a
sugestao tem dado do CRM que a sustente.

---

## Memoria historica: o GitHub e parte do sistema

O codigo responde **o que o sistema faz hoje**; a historia responde **por que**. As duas
coisas sao parte do produto, e a segunda mora no GitHub — issues, comentarios, PRs e
testes —, para sobreviver a troca de agente, modelo, maquina ou sessao.

O metodo esta em **[docs/MEMORIA-HISTORICA.md](docs/MEMORIA-HISTORICA.md)**: quando
registrar e quando nao registrar, o formato do comentario (`HISTORY:<TYPE>`), os tipos
(`BUSINESS_RULE`, `ARCH_DECISION`, `IMPL_ERROR`, `LEGACY_BEHAVIOR`, `TECH_CONSTRAINT`,
`RISK`…), o nivel de confianca, a memoria em camadas e o fechamento de issue.

Duas regras de la que mudam o trabalho de todo dia:

- **Evidencia antes de conclusao.** Observacao nao vira regra de negocio sem evidencia;
  sem ela, registrar como `BUSINESS_AMBIGUITY`. Nunca inventar memoria para parecer
  confiante.
- **Conhecimento nao pode viver so no contexto da sessao.** O que a proxima pessoa
  economizaria tempo sabendo, vai para o GitHub — nao para a mensagem de commit, que
  ninguem pesquisa.

---

## Fora de escopo, por decisao

Multi-tenant · importacao de planilha · relatorios · permissao granular ·
transcricao de audio · **envio automatico de mensagem ao cliente**.

O ultimo e a tese do produto, nao uma limitacao. O vendedor escreve.
