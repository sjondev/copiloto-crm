# Base legal por finalidade

**Minuta técnica, escrita por quem conhece o sistema — precisa de revisão jurídica antes
do primeiro dado real** (#77). Ela existe para que essa revisão comece do que o software
faz, e não de um modelo genérico.

Base legal não é formalidade: **ela determina o que o produto pode fazer**. Se fosse
consentimento, o sistema precisaria de revogação que parasse o tratamento na hora; sendo
legítimo interesse, precisa de canal de oposição funcionando. São funcionalidades
diferentes, e descobrir isso depois de construir custa retrabalho — por isso a decisão
vem antes.

---

## 1. A escolha, por finalidade

| Finalidade | Base legal | Por quê |
|---|---|---|
| Receber e guardar a conversa | Execução de contrato ou procedimentos preliminares (art. 7, V) | O cliente mandou mensagem **para comprar**. Guardar o que ele disse é o próprio atendimento |
| Cadastrar e identificar o Lead | Art. 7, V | Sem saber de quem é a conversa não há atendimento |
| **Analisar a conversa por IA** (dossiê, sinais, lacunas) | **Legítimo interesse (art. 7, IX)** | Não é necessário para atender o pedido — é para atender *melhor*. Exige a avaliação da seção 2 |
| Sugerir abordagem ao vendedor | Legítimo interesse (art. 7, IX) | Mesma análise, mesmo balanceamento |
| Ficha do Cliente (dado de terceiro B2B) | Legítimo interesse (art. 7, IX) | Contato profissional em contexto de negócio, com as salvaguardas da #89 |
| Ledger de custo e medição | Legítimo interesse (art. 7, IX) | Do controlador, sobre a própria operação; usa o vínculo, não o conteúdo |
| Indexar precedentes (RAG) | Legítimo interesse (art. 7, IX), **com a salvaguarda mais forte** | É onde a expectativa do titular é mais frágil: a conversa dele ajuda a vender para outra pessoa |
| Atender pedidos de titular | Cumprimento de obrigação legal (art. 7, II) | O pedido obriga a resposta |
| **Dado sensível** (saúde, fé, política…) | **Nenhuma — por isso não é tratado** | O art. 11 exigiria consentimento específico e destacado, que ninguém deu. A decisão foi **remover** o trecho antes da análise (#82), e não achar uma base para ele |

A última linha é a mais importante da tabela: quando não há base legal, a saída correta
não é procurar uma justificativa melhor — é **não tratar**.

## Por que não consentimento

Consentimento é revogável a qualquer momento, e um produto cujo funcionamento depende dele
para de funcionar sem aviso. Pior: o consentimento pedido no meio de uma conversa de venda
— "aceita que a IA leia isto?" — não é livre de verdade, porque quem quer comprar clica em
qualquer coisa para seguir. Consentimento coletado assim é **frágil na forma e frágil no
mérito**.

Consentimento continua sendo a base certa para o que estiver fora do atendimento — por
exemplo, usar a conversa para marketing, ou para treinar modelo. Nada disso está no
escopo, e o [contrato](CONTRATO-TRATAMENTO.md) proíbe o segundo sem autorização específica.

---

## 2. Avaliação de legítimo interesse

Três etapas, na ordem em que a lei pede. Só passa quem passa nas três.

### 2.1 A finalidade é legítima?

Sim: ajudar um vendedor a entender a conversa que **ele mesmo está tendo** com o cliente
dele, e a não esquecer negócio em aberto. É atividade comercial ordinária, e o titular é
parte dela — ele iniciou o contato para comprar.

### 2.2 O tratamento é necessário?

Necessário para a finalidade, e limitado a ela:

- a análise usa **a conversa daquele cliente com aquela empresa**, não dados comprados,
  não enriquecimento externo, não rede social;
- o produto **não envia mensagem ao cliente**, então não há tratamento para influenciar
  diretamente o titular sem intermediação humana;
- o dado sensível é **removido** do contexto, e não usado com cuidado redobrado — o que
  não está lá não influencia nada.

Onde o tratamento **não** era necessário, ele foi cortado: o Vigia varre datas sem chamar
modelo, e o catálogo vai inteiro no contexto em vez de virar índice (#63).

### 2.3 O balanceamento, com a pergunta desconfortável

> **O cliente que manda mensagem para uma torrefação espera que aquilo seja analisado por
> IA para traçar o perfil dele?**

A resposta honesta é **provavelmente não**. Ele espera ser atendido por uma pessoa.

Isso não impede o legítimo interesse — impede que ele seja assumido de graça. A expectativa
frustrada é o que exige salvaguarda proporcionalmente mais forte, e é o que justifica cada
uma das decisões abaixo, que existem **no código**, não só neste documento:

| Salvaguarda | Onde está |
|---|---|
| A IA nunca escreve ao cliente; quem fala é o vendedor | Tese do produto, e o escopo inteiro depende dela |
| Dado sensível sai da fala antes de qualquer análise | #82, na moldura de contexto |
| PII mascarada antes de sair da rede, com teste que falha o build | #43 |
| Toda conclusão cita a fala que a originou — o titular pode contestar o dado *e* a leitura | Regra de ancoragem |
| Fato e impressão separados, com procedência | #88 |
| Categoria sensível recusada na Ficha, e aviso ao vendedor de que o titular pode ler | #89 |
| **Canal de oposição que suspende a análise sem apagar o histórico** | #81 |
| Exportação inclui o que o sistema *produziu*, não só o que entrou | #81 |
| Retenção por finalidade, com prazo declarado | seção 3 |
| Escopo por vendedor: cada um enxerga os próprios leads | #49, #58 |
| Sem uso para treinar modelo sem autorização específica | Cláusula 2 do contrato |

**A oposição é a que sustenta a base.** Legítimo interesse sem canal de oposição real é
legítimo interesse no papel; por isso ela é código, e por isso opor-se **não** custa o
histórico comercial do cliente — se custasse, ninguém se oporia, e o canal seria decorativo.

E a expectativa frustrada é também o que torna a transparência (#80) obrigatória, e não
cortesia: o titular precisa **saber** que existe análise para poder se opor a ela.

---

## 3. Prazos de retenção, por finalidade

Propostos aqui, para o controlador confirmar em contrato. Prazo que ninguém escreve vira
"guardamos desde 2019 porque ninguém definiu".

| Dado | Prazo | Por quê |
|---|---|---|
| Conversa e Lead | 24 meses após o último contato | Cobre a recompra e a retomada; além disso é arquivo morto com dado pessoal dentro |
| Ficha do Cliente | Enquanto o negócio estiver ativo; 12 meses após perdido | #89 |
| Dado sensível detectado | 30 dias | Regime próprio, mais curto que tudo (#82) |
| Log da aplicação | 6 meses | Prazo de depuração real; além disso ninguém abre |
| Ledger de custo | Prazo fiscal do controlador | É documento contábil, não histórico de conversa |
| Índice de embeddings | Igual à conversa de origem, com expurgo em cascata | Apagar o Lead sem apagar o vetor deixa o dado vivo (#46, #62) |

---

## 4. O que falta antes do primeiro dado real

- **Revisão jurídica** desta minuta, com o controlador.
- Onde o provedor de modelo processa fisicamente (#79) — muda o balanceamento se houver
  transferência internacional.
- Aviso de transparência ao titular (#80), que a seção 2.3 torna obrigatório.
