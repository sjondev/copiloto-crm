# Registro das operações de tratamento

O que o Copiloto faz com dado pessoal, operação por operação (art. 37 · #76).

Este documento é **descritivo, não aspiracional**: cada operação diz o que já existe no
código e o que ainda é issue. Registro que descreve o sistema que se pretende ter, e não o
que está rodando, é pior que registro nenhum — ele passa em auditoria e mente para quem
decide.

Papéis (controlador é a empresa cliente; operador é o Copiloto) estão em
[LGPD.md](LGPD.md#25-quem-responde-pelo-quê). **A base legal de cada operação é a #77** e
está marcada como pendente onde ainda não foi decidida — preencher por analogia seria
inventar a parte que mais importa.

Cada operação abaixo declara, obrigatoriamente: **Finalidade**, **Titulares**, **Dados**,
**Base legal**, **Compartilhamento**, **Retenção**, **Segurança** e **Estado**. Há teste
que reprova o build se algum desses rótulos faltar.

---

## 1. Ingestão de conversa do WhatsApp

- **Finalidade:** receber as mensagens trocadas entre o vendedor e o cliente, que são a
  matéria-prima de todo o resto.
- **Titulares:** clientes da empresa; vendedores; terceiros citados na conversa.
- **Dados:** telefone, nome quando aparece, conteúdo livre das mensagens, data e hora.
- **Base legal:** pendente (#77) — provável execução de contrato/legítimo interesse do
  controlador na relação comercial.
- **Compartilhamento:** nenhum nesta etapa. O webhook grava e enfileira.
- **Retenção:** a definir por finalidade com o controlador (#77). Hoje não há expurgo
  automático — e isso está declarado, não escondido.
- **Segurança:** webhook exige identificador do provedor; conteúdo do cliente entra
  delimitado como dado não confiável; dado sensível é retirado antes de qualquer análise
  (#82).
- **Estado:** existe.

## 2. Cadastro e resolução de Lead

- **Finalidade:** saber de quem é cada conversa, e ligar mensagens ao mesmo cliente.
- **Titulares:** clientes da empresa.
- **Dados:** telefone normalizado, nome quando informado, data de criação.
- **Base legal:** pendente (#77).
- **Compartilhamento:** nenhum.
- **Retenção:** enquanto durar a relação comercial; exclusão a pedido do titular (#46).
- **Segurança:** telefone normalizado antes de casar, para não criar dois cadastros da
  mesma pessoa; acesso por perfil (#49).
- **Estado:** existe.

## 3. Ficha do Cliente

- **Finalidade:** guardar o que o vendedor já sabia antes de falar, para a conversa não
  começar do zero.
- **Titulares:** **terceiros que não estão na conversa** — a pessoa pesquisada não sabe que
  há registro sobre ela. É o ponto sensível desta operação (#89).
- **Dados:** ramo, porte, cargo, papel na decisão, necessidade, orçamento estimado, e
  impressões do vendedor, cada linha marcada como fato ou impressão, com fonte.
- **Base legal:** pendente (#77) — provável legítimo interesse comercial em contexto B2B.
- **Compartilhamento:** vai ao provedor de modelo junto do contexto, quando houver um
  configurado.
- **Retenção:** enquanto o negócio estiver ativo; **12 meses** após negócio perdido (#89).
- **Segurança:** categoria sensível é recusada na escrita; o vendedor vê aviso de que o
  titular pode pedir para ler; a ficha sai na exportação de acesso (#81).
- **Estado:** existe.

## 4. Análise por IA (dossiê, sinais, plano)

- **Finalidade:** produzir, para o vendedor, leitura do que aconteceu na conversa — nunca
  mensagem para o cliente.
- **Titulares:** clientes; terceiros citados.
- **Dados:** conversa pseudonimizada, ficha, playbook. **E as inferências geradas**, que
  são dado pessoal novo, criado por nós, sobre o titular.
- **Base legal:** pendente (#77). Se for legítimo interesse, depende do canal de oposição
  funcionando (#81) — e ele existe.
- **Compartilhamento:** provedor de modelo, quando configurado. Hoje `MODEL_PROVIDER=fake`
  e **nada sai da máquina**. Onde o provedor processa é a #79.
- **Retenção:** o dossiê é recalculado, não guardado. O que persiste é o custo (operação 5).
- **Segurança:** PII Shield antes da saída; dado sensível removido do contexto (#82);
  oposição do titular suspende a análise sem apagar o histórico (#81).
- **Estado:** parcial — o pipeline de modelo ainda não existe; as defesas em volta dele já.

## 5. Ledger de custo de IA

- **Finalidade:** saber quanto cada negócio custou em IA, e se o produto se paga.
- **Titulares:** clientes (indiretamente, pelo vínculo com o negócio).
- **Dados:** modelo, custo, data, `deal_id` — que aponta para uma pessoa.
- **Base legal:** pendente (#77) — provável legítimo interesse do controlador em controle
  de custos.
- **Compartilhamento:** nenhum.
- **Retenção:** prazo contábil/fiscal do controlador; não segue o prazo da conversa.
- **Segurança:** não guarda conteúdo de mensagem, apenas o vínculo e o valor.
- **Estado:** existe.

## 6. Log da aplicação

- **Finalidade:** operar e depurar o sistema.
- **Titulares:** clientes; vendedores.
- **Dados:** identificadores, marcadores de PII (`[TEL_1]`) e metadados. **Mesmo mascarado é
  dado pessoal**: o marcador é reversível pelo mapa, e o `deal_id` ao lado identifica.
- **Base legal:** pendente (#77).
- **Compartilhamento:** nenhum hoje; ferramenta de observabilidade externa entraria como
  suboperador.
- **Retenção:** a definir; menor que a da conversa.
- **Segurança:** teste que **falha o build** se PII vazar em log ou payload (#43).
- **Estado:** existe.

## 7. Varredura de negócios parados (Vigia)

- **Finalidade:** avisar o vendedor sobre negócio esquecido, cliente calado e proposta
  esfriando.
- **Titulares:** clientes.
- **Dados:** datas de mensagem e de estágio; a última fala do cliente é citada no alerta.
- **Base legal:** pendente (#77).
- **Compartilhamento:** nenhum — a varredura é determinística e **não chama modelo**.
- **Retenção:** não cria registro novo além do alerta em log.
- **Segurança:** só varre negócios abertos; não repete o mesmo alerta.
- **Estado:** issue #53.

## 8. Índice de embeddings (RAG)

- **Finalidade:** recuperar precedentes de conversas que fecharam com objeção semelhante.
- **Titulares:** clientes; terceiros citados nas conversas indexadas.
- **Dados:** trechos de conversa vetorizados. **Vetor é dado pessoal** — derivado do texto,
  recuperável por semelhança, apontando para o mesmo titular.
- **Base legal:** pendente (#77).
- **Compartilhamento:** provedor de embeddings, quando houver.
- **Retenção:** igual à da conversa de origem, e **expurgo em cascata** com o Lead — apagar
  o Lead sem apagar o vetor deixa o dado vivo depois de o titular pedir exclusão.
- **Segurança:** PII mascarada antes de indexar; isolamento entre titulares; dado sensível
  nunca indexado (#62, #82).
- **Estado:** issue #60, #62 — **não existe**.

## 9. Atendimento a pedidos de titular

- **Finalidade:** dar ao controlador o meio de responder confirmação, acesso, correção,
  portabilidade, compartilhamento, oposição e exclusão.
- **Titulares:** quem pediu.
- **Dados:** o próprio pedido, data de recebimento e de atendimento.
- **Base legal:** cumprimento de obrigação legal.
- **Compartilhamento:** nenhum.
- **Retenção:** prazo de comprovação do atendimento.
- **Segurança:** exportação identada e legível; titular inexistente devolve nada, e não um
  arquivo vazio que pareceria cadastro em branco; prazo de 15 dias medido em código (#81).
- **Estado:** existe.

---

## O que este registro ainda não fecha

| Assunto | Issue |
|---|---|
| Base legal por finalidade, e o teste de legítimo interesse | #77 |
| Onde o provedor de IA processa fisicamente | #79 |
| Prazos de retenção acordados com o controlador | #77 |
| Aviso de transparência ao titular | #80 |
| Exclusão em cascata, incluindo índice e cache | #46 |
