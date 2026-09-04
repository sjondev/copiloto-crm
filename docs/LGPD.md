# LGPD: o que é dado pessoal aqui, e o que decorre disso

Este documento existe por causa de **uma confusão específica** (#83), e ela gera decisão
errada em cascata: quem acredita ter anonimizado conclui que pode reter para sempre,
indexar à vontade e dispensar controle de acesso — porque "não é mais dado pessoal". E é.

O que **não** está decidido aqui está nomeado no fim, com a issue que decide. Documento de
conformidade que preenche lacuna com suposição é pior que documento incompleto: ele
parece resposta.

---

## 1. O PII Shield pseudonimiza. Não anonimiza.

O escudo troca `João Silva, 11 98765-4321` por `[NOME_1], [TEL_1]` **mantendo o vínculo
com o Lead** — o mapa que remonta fica do nosso lado, por construção, porque a resposta do
modelo precisa voltar legível para o vendedor.

Isso é **pseudonimização**: reversível por quem tem a tabela.

> Anonimização de verdade é **irreversível**, inclusive com informação adicional. Só o
> dado anonimizado sai do escopo da LGPD. O nosso não sai.

Consequência direta, e é o ponto inteiro do documento: **texto mascarado continua sendo
dado pessoal**, e carrega junto todas as obrigações — base legal, prazo de retenção,
controle de acesso, direito de acesso e exclusão do titular.

Chamar o escudo de anonimização não seria imprecisão de vocabulário: seria **a base legal
errada**, escrita por engano num documento que alguém vai ler para decidir.

---

## 2. Onde há dado pessoal

Todo item desta lista é base de dado pessoal, com os mesmos controles. A coluna de estado
diz o que **existe hoje** — não o que se pretende.

| Onde | O que tem | Estado |
|---|---|---|
| Postgres (`leads`, `conversas`, `fichas_cliente`) | Nome, telefone, o que o cliente disse, o que o vendedor anotou | existe |
| Payload que sai para o provedor de IA | Conversa mascarada pelo escudo — **pseudonimizada, não anônima** | existe |
| Log da aplicação | Mesmo mascarado, é dado pessoal: o marcador é reversível pelo mapa, e o `deal_id` ao lado identifica | existe |
| Ficha do cliente | Inclui **dado de terceiro** (o que o vendedor ouviu falar), que tem regime próprio | existe · regime em #89 |
| Índice de embeddings (RAG) | Trechos de conversa vetorizados | não existe · #60, #62 |
| Ledger (`ai_invocations`) | Custo ligado ao `deal_id`, que aponta para uma pessoa | existe |

**O índice do RAG é a linha que mais escapa.** Vetor não parece dado pessoal — parece
número. Mas ele é derivado do texto, é recuperável por semelhança e aponta para o mesmo
titular: é base de dado pessoal como qualquer outra, com o mesmo controle de acesso e o
mesmo expurgo. Apagar o Lead sem apagar o vetor deixa o dado vivo depois de o titular
pedir exclusão.

---

## 2.5 Quem responde pelo quê

Quando uma empresa usa o Copiloto, os papéis não são negociáveis — eles decorrem de
quem decide **finalidade e meios**:

| Papel | Quem | Por quê |
|---|---|---|
| **Controlador** | A empresa cliente | É ela quem decide vender café por WhatsApp, quem contata, com que finalidade, e que dado pede ao cliente |
| **Operador** | O Copiloto | Trata em nome dela, seguindo as instruções dela, e não para finalidade própria |
| **Titulares** | Clientes da empresa, vendedores, e terceiros citados na conversa | Os três grupos, não só o primeiro |

Isso decide **quem responde pelo quê** perante o titular e a ANPD, e é o que precisa
estar no contrato antes do primeiro dado real entrar.

### Obrigações do operador, que são nossas

- **Seguir as instruções do controlador**, e só elas. Tratar para finalidade própria — por
  exemplo, usar conversa de um cliente para melhorar o produto — nos tornaria controlador
  *daquele* tratamento, com todas as obrigações que vêm junto.
- **Segurança**, incluindo o que já é código: PII Shield, controle de acesso, isolamento
  entre titulares no que for indexado.
- **Apoiar o controlador nos pedidos de titular** — acesso, correção, portabilidade,
  oposição e exclusão. O operador não responde direto ao titular; ele dá ao controlador o
  meio de responder. É por isso que a exportação (#81) é ferramenta, e não um canal
  público.
- **Avisar incidente** ao controlador, sem demora. O prazo e o rito são a #84.
- **Não subcontratar sem autorização** — o item abaixo, que é o que costuma ser
  descoberto tarde.

### A cadeia de suboperadores

**Mandar a conversa para um provedor de IA é subcontratar tratamento.** O controlador
precisa saber que esse terceiro existe e autorizá-lo; sem isso, a empresa cliente está
compartilhando dado dos clientes dela com alguém que ela não sabe que existe.

| Suboperador | O que trata | Estado hoje |
|---|---|---|
| Provedor de modelo | Conversa pseudonimizada, para análise | **Nenhum**: `MODEL_PROVIDER=fake` é o padrão, e nada sai da máquina. Ao trocar, entra aqui com nome e país |
| Hospedagem / banco | Todo o dado do CRM | A definir com o controlador; hoje roda em Postgres local |
| Servidor MCP | Depende de quem é o servidor | O nosso (#56) **não** é subcontratação — é o próprio operador. Ferramenta MCP de terceiro (ERP, tabela de frete) seria, e entra nesta tabela quando existir |

A coluna de estado não é enfeite: hoje, com os padrões do repositório, **não há
transferência a terceiro nenhum**. É o que torna a demo honesta — e é também o que muda no
dia em que uma chave de provedor real for configurada, que é o momento de o controlador
autorizar, não depois.

Onde o provedor processa fisicamente é transferência internacional, e isso é a #79.

---

## 2.9 O registro das operações

Cada tratamento — ingestão, cadastro, ficha, análise, ledger, log, varredura, índice e
atendimento a pedidos — está descrito em
[REGISTRO-DE-TRATAMENTO.md](REGISTRO-DE-TRATAMENTO.md), com finalidade, titulares, dados,
base legal, compartilhamento, retenção, segurança e **estado**.

A coluna de estado é o que separa registro de folheto: cada operação diz se já existe no
código ou se ainda é issue. E há teste que **reprova o build** quando uma operação não
declara algum desses campos — operação nova entra por PR de feature, e quem está
escrevendo código não volta ao documento a não ser que ele reprove.

---

## 3. O que decorre, na prática

- **Retenção tem prazo, e prazo tem finalidade.** "Pseudonimizado, então guardo para
  sempre" é a conclusão errada mais comum.
- **Expurgo em cascata.** Exclusão do titular alcança conversa, ficha, log e índice —
  qualquer um deles sozinho mantém o dado vivo.
- **Controle de acesso vale para a base mascarada** tanto quanto para a original.
- **A inferência também é dado pessoal.** "Sensível a preço", "esfriando" e "provável
  necessidade" são dado novo, criado por nós, sobre alguém que nunca nos forneceu aquilo
  — e o titular pode acessar e contestar. Protege-se o que entrou e esquece-se o que o
  sistema produziu.

---

## 4. O gate que mantém isto verdadeiro

Há teste que lê `README.md` e `docs/` e **reprova o build** se a palavra "anonimização"
aparecer descrevendo o que fazemos, em vez de dizendo o que **não** fazemos.

O motivo de ser teste, e não convenção: o termo errado é confortável — é mais curto, soa
mais forte, e ninguém revisando um PR de código vai reler o README para conferir
vocabulário jurídico. Convenção que depende de alguém lembrar tem prazo de validade.

---

## 5. O que este documento NÃO decide

| Assunto | Issue |
|---|---|
| Base legal por finalidade, e legítimo interesse | #77 |
| Onde o provedor de IA processa (transferência internacional) | #79 |
| Direitos do titular além da exclusão | #81 |
| Dado sensível em regime próprio | #82 |
| Aviso de transparência ao titular | #80 |
| Resposta a incidente e log de auditoria | #84 |
| Dado de terceiro na Ficha do Cliente | #89 |
