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

## 10. O que ficou de fora, de propósito

Multi-tenant · importação de planilha · relatórios · permissão granular · transcrição de
áudio · **envio automático de mensagem ao cliente**.

O último não é limitação técnica: é a tese do produto. O vendedor escreve.
