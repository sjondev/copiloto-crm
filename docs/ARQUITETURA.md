# Arquitetura

Documento de decisões. Cada seção responde a uma pergunta que um entrevistador faria.

---

## 1. Por que a IA entrega contexto em vez de escrever a mensagem

Decisão de produto com consequência técnica direta.

Uma sugestão de fala pronta falha **na frente do cliente**. O custo do erro é a confiança
do vendedor na ferramenta, e essa confiança não volta. Um dossiê de contexto falha **na
tela do vendedor**, antes de qualquer dano: ele discorda, corrige e segue usando.

Consequência de engenharia: o sistema é otimizado para **precisão de leitura**, não para
fluência de escrita. Por isso todo sinal do dossiê é obrigado a citar o trecho da conversa
que o originou — sem citação, o bloco não é exibido. Isso transforma "dossiê genérico" de
um problema subjetivo ("a IA tá ruim") em um defeito verificável.

---

## 2. As quatro camadas de contexto

Conversa de WhatsApp de três meses não cabe em janela de contexto nenhuma. O contexto é
montado em camadas com orçamento de tokens:

| Camada | Conteúdo | Orçamento | Cortável? |
|---|---|---|---|
| **C0** | Identidade do agente, o que pode afirmar, regra de ancoragem | ~200 | **nunca** |
| **C1** | Playbook da empresa: produto, preço, política de desconto, tom | ~800 | não |
| **C2** | Ficha do negócio: lead, valor, estágio, dias parado, proposta | ~1000 | parcial |
| **C3** | A conversa: mensagens literais + resumo progressivo do que veio antes | resto | sim |

**Regra de corte:** estourou o orçamento, corta C3 do mais antigo para o mais novo,
substituindo mensagens literais por resumo. C0 nunca é cortada.

**Por que C3 precisa de texto cru:** tom, ironia e objeção velada ("vou pensar", "manda
por escrito") só sobrevivem no literal. Resumir a conversa recente destrói exatamente o
sinal que o dossiê existe para captar. Por isso o resumo progressivo age no passado
distante, nunca nas últimas mensagens.

---

## 3. Roteamento de modelos

Não existe "o modelo do projeto". Existe uma tabela de capacidade, custo e latência, e
uma escolha por tarefa:

- **Triagem** (`A0`): a pergunta é "isso muda o dossiê?". Resposta binária, milhares de
  vezes por dia. Modelo mais barato disponível, latência em milissegundos.
- **Leitura e detecção** (`A1`, `A3`, `A4`): classificação estruturada sobre texto curto.
  Modelo médio.
- **Conselho de plano** (`A5`): raciocínio sobre estratégia comercial. Modelo forte, pode
  gastar segundos — e é **sob demanda**, disparado por clique, o que limita o custo por
  natureza.

O router também consulta o **estado do circuito** de cada provedor: modelo com circuito
aberto não é escolhido, mesmo sendo o ideal para a tarefa.

---

## 4. Contrato de saída e recuperação de erro

Todo agente declara um JSON Schema. O fluxo de falha:

1. Saída não valida contra o schema → **reprompt** incluindo a mensagem de erro do
   validador.
2. Segunda falha → **próximo modelo da cascata**.
3. Cascata esgotada → **degradação silenciosa**: o dossiê mantém o último estado válido e
   exibe um aviso discreto.

O passo 3 é decisão de produto: o vendedor está no meio de uma venda com o cliente
digitando do outro lado. Erro na tela naquele momento é pior que informação levemente
desatualizada.

---

## 5. Idempotência

Webhook de WhatsApp **reentrega mensagem**. Sem chave de idempotência, uma reentrega
significa analisar de novo, pagar de novo e possivelmente gerar um dossiê conflitante.

Cada mensagem entra com um identificador estável da origem; invocações de IA carregam
`Idempotency-Key` derivada do estado da conversa. Reprocessar é barato e não cobra duas
vezes.

---

## 6. Fila e o webhook

O webhook **responde 200 imediatamente** e publica em uma fila em memória (`Channel<T>`).
Processar IA dentro do handler do webhook é o erro clássico: provedor lento vira timeout
na origem, que vira reentrega, que vira custo duplicado.

Fila em memória e não RabbitMQ porque o volume não justifica — YAGNI. A troca está
isolada atrás de uma interface se e quando doer.

---

## 7. PII e LGPD

Conversa com cliente é dado pessoal. Enviar para um modelo de terceiro é transferência de
dado pessoal.

- **PII Shield**: CPF, telefone, e-mail e endereço são substituídos por marcadores
  (`[CPF_1]`) antes da saída da rede, e remontados na volta.
- Teste automatizado **falha o build** se PII aparecer em log ou payload de saída.
- Retenção configurável: conversa antiga vira resumo ou é expurgada.
- Exclusão em cascata: apagar um Lead apaga conversa, dossiê e sugestões.

---

## 8. Injeção de prompt

Aqui o vetor é **externo e hostil**: o cliente pode digitar "ignore as instruções
anteriores" dentro do WhatsApp, e essa mensagem entra no contexto por construção.

Mitigação: a mensagem do cliente entra **delimitada** e explicitamente marcada como dado
não confiável, nunca como instrução. Há teste dedicado com payloads de injeção conhecidos.

---

## 9. O ledger e a conta fechada

`ai_invocations` registra por chamada: agente, modelo, tokens de entrada e saída, custo
estimado, latência, número de tentativas, `correlation_id` e **`deal_id`**.

O vínculo com o Deal é o que permite fechar a conta — e é caro de enxertar depois, por
isso entra no modelo desde a primeira migration.

Métricas derivadas:

- custo de IA por deal ganho
- receita influenciada ÷ custo de IA
- taxa de aceite por agente, por modelo e **por técnica de persuasão**
- custo acumulado dos blocos **ignorados** (o desperdício, explícito)

Sobre a comparação entre deals com e sem uso do copiloto: **não é experimento
controlado**, há viés de seleção, e a ressalva está escrita na própria tela do painel.

---

## 10. MCP: o CRM como servidor, e as ferramentas de ancoragem

MCP (Model Context Protocol) entra em **dois sentidos opostos**, e cada um resolve
um problema diferente.

### 10.1 O CRM **é** um servidor MCP

O CRM expõe suas próprias capacidades como ferramentas MCP (`ModelContextProtocol.AspNetCore`):
consultar o dossiê de um lead, listar negócios parados, ler o playbook, buscar o
histórico de um cliente.

Consequência: qualquer cliente MCP passa a operar sobre o CRM. O gestor abre um
assistente e pergunta "quais negócios travaram no preço este mês?" sem que exista
uma tela para isso. O CRM deixa de ser um destino e vira uma **capacidade
componível**.

Escopo é obrigatório: ferramenta MCP é superfície de acesso a dado de cliente. O
servidor exige credencial, respeita o perfil do usuário (vendedor só enxerga os
próprios leads) e nunca expõe escrita destrutiva.

### 10.2 O agente **consome** ferramentas MCP — e é isso que sustenta a ancoragem

Este é o encaixe que justifica MCP no projeto.

A [regra de ancoragem](#7-pii-e-lgpd) proíbe o agente de afirmar escassez, prazo,
preço ou prova social sem dado que sustente. A pergunta natural é: **de onde vem
esse dado?**

Vem de ferramentas. Estoque real, tabela de preço vigente, política de desconto
ativa, agenda de entrega. Expostas como ferramentas MCP, o agente **busca antes de
afirmar** — e quando a busca não retorna nada, ele não tem o que afirmar.

Isso muda a natureza do guardrail: a ancoragem deixa de ser uma instrução no prompt
(que um modelo pode ignorar) e passa a ser uma **propriedade da arquitetura** (não
existe o dado no contexto, então não há o que inventar). É a diferença entre pedir
para o modelo se comportar e tornar o mau comportamento impossível.

---

## 11. RAG: onde entra e — mais importante — onde não entra

RAG entra em **um** lugar com força, em um segundo com ressalva, e é recusado em
dois onde pareceria natural.

### 11.1 Onde entra: precedentes da própria empresa

O vendedor está travado numa objeção de preço. A pergunta útil não é "o que a
teoria de vendas diz sobre objeção de preço" — é **"como os últimos clientes que
travaram no preço aqui nesta empresa foram convertidos?"**

Recuperar trechos de conversas passadas que **fecharam** com objeção semelhante dá
ao agente `A5` precedente real em vez de teoria genérica. É a diferença entre um
conselho que serve para qualquer negócio e um conselho que só faz sentido neste.

### 11.2 Onde entra com ressalva: catálogo de produto

Fichas técnicas, notas de degustação, origem, torra, moagem recomendada. Vale RAG
**se** o catálogo for grande o bastante para não caber no contexto. Com poucos
produtos, é mais barato e mais confiável mandar o catálogo inteiro.

Critério de decisão, não de gosto: se o catálogo cabe no orçamento de tokens, não
há RAG.

### 11.3 Onde **não** entra, e por quê

**No playbook.** O playbook são ~800 tokens e cabe inteiro na camada C1. Montar
embedding, índice e recuperação para buscar dentro de um documento que já cabe no
contexto adiciona latência, custo e um modo de falha novo (recuperar o pedaço
errado) para resolver um problema que não existe. É o caso de manual de
overengineering.

**Em banco vetorial dedicado.** Postgres já está no projeto. `pgvector` roda a busca
por similaridade na mesma instância, com o mesmo backup, a mesma transação e a mesma
credencial. Trazer Qdrant, Pinecone ou Weaviate significaria um segundo banco para
operar, monitorar e manter consistente — em troca de nada, nesta escala.

Se um dia a escala exigir, a busca está atrás de uma interface e a troca é local.

### 11.4 O risco que RAG traz para este projeto em específico

Recuperar conversa de um cliente e injetá-la no contexto de outro é **vazamento de
dado pessoal entre titulares**. É o risco mais sério que o RAG introduz aqui, e ele
não é hipotético: é o comportamento padrão de uma busca por similaridade mal
isolada.

Mitigação: o que é indexado passa pelo PII Shield **antes** de virar embedding, e a
recuperação devolve padrão e técnica, não transcrição literal de terceiros.

### 11.5 RAG precisa provar que serve

Antes de virar padrão, a recuperação é comparada contra o baseline sem RAG nas
conversas do seed. Se a qualidade do dossiê não melhorar de forma observável, RAG
sai — porque custa embedding, latência e complexidade.

Adotar RAG por ser esperado, e não por medir melhora, é como o projeto vira caro
sem ficar melhor.

---

## 12. Estado compartilhado e durabilidade: Redis e RabbitMQ

### 12.1 A inconsistência que motivou isso

Quatro decisões deste documento assumem, sem dizer, que existe **uma única instância
da aplicação**:

| Mecanismo | O que quebra com duas instâncias |
|---|---|
| Idempotência do webhook | Cada instância tem seu próprio registro de mensagens vistas. A reentrega cai na outra instância e é processada de novo — **e cobrada de novo**. |
| Circuit breaker | Três instâncias, três circuitos independentes. Cada uma precisa falhar N vezes por conta própria antes de proteger. O provedor caído é golpeado 3N vezes. |
| Rate limit por usuário | O limite vira o limite × número de instâncias. |
| Cache de análise | Taxa de acerto cai proporcionalmente às instâncias. |

Isso não é um problema de volume — é um problema de **corretude**. Um sistema cuja
proteção contra gasto duplicado depende de rodar em processo único tem uma restrição
de implantação não declarada.

### 12.2 A fila em memória perde mensagem

O webhook responde `200` e publica em `Channel<T>`. Se o processo reinicia com a fila
cheia, aquelas mensagens **somem** — e o WhatsApp não vai reentregar, porque já
recebeu o `200`.

O resultado é a pior falha possível neste produto: uma conversa que o vendedor viu
acontecer, e que o dossiê simplesmente ignora, sem erro em lugar nenhum.

O argumento correto para RabbitMQ aqui não é vazão. É **durabilidade**: mensagem
confirmada só sai da fila depois de processada, e o que falha vai para uma *dead
letter queue* em vez de evaporar.

### 12.3 Como entram, sem virar peso morto

Ambos entram atrás de interface, seguindo o mesmo padrão dos `Fake*` do projeto:

```
IDistributedState   ->  InMemoryState   (padrão)  |  RedisState
IQueue              ->  ChannelQueue    (padrão)  |  RabbitMqQueue
```

Escolha por configuração (`STATE_BACKEND`, `QUEUE_BACKEND`), e o `docker-compose`
usa profile: `docker compose up` sobe só o Postgres; `--profile distribuido` sobe
Redis e RabbitMQ.

Consequência prática, que é o ponto: **a demo continua rodando com um único
`docker compose up`**, sem broker e sem cache, enquanto o mesmo código roda
distribuído em produção. Uma demonstração que depende de cinco contêineres no ar tem
cinco maneiras de falhar ao vivo.

### 12.4 Redis também é backplane do SignalR

Com mais de uma instância, o vendedor conectado à instância A não recebe o evento
gerado na instância B — o dossiê simplesmente não atualiza, sem erro visível.
`Microsoft.AspNetCore.SignalR.StackExchangeRedis` resolve, e é o motivo menos citado
e mais frequentemente descoberto tarde.

### 12.5 O critério honesto

Estas peças precisam ser defendidas uma a uma. "Usei Redis e RabbitMQ" não é
argumento; "a idempotência do webhook precisa de estado fora do processo, senão a
reentrega é cobrada duas vezes" é.

Se em algum ponto uma das duas não tiver justificativa nomeada, ela sai.

---

## 13. O que ficou de fora, de propósito

Multi-tenant · importação de planilha · relatórios · permissão granular · transcrição de
áudio · **envio automático de mensagem ao cliente**.

O último não é limitação técnica: é a tese do produto. O vendedor escreve.
