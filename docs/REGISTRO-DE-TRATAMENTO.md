# Registro das operações de tratamento

O que o Copiloto faz com dado pessoal, operação por operação (art. 37 · #76).

Este documento é **descritivo, não aspiracional**: cada operação diz o que já existe no
código e o que ainda é issue. Registro que descreve o sistema que se pretende ter, e não o
que está rodando, é pior que registro nenhum — ele passa em auditoria e mente para quem
decide.

Papéis (controlador é a empresa cliente; operador é o Copiloto) estão em
[LGPD.md](LGPD.md#25-quem-responde-pelo-quê). A base legal de cada operação está em
[BASE-LEGAL.md](BASE-LEGAL.md), com a avaliação de legítimo interesse onde ela é a base.

Cada operação abaixo declara, obrigatoriamente: **Finalidade**, **Titulares**, **Dados**,
**Base legal**, **Compartilhamento**, **Retenção**, **Segurança** e **Estado**. Há teste
que reprova o build se algum desses rótulos faltar.

---

## 1. Ingestão de conversa do WhatsApp

- **Finalidade:** receber as mensagens trocadas entre o vendedor e o cliente, que são a
  matéria-prima de todo o resto.
- **Titulares:** clientes da empresa; vendedores; terceiros citados na conversa.
- **Dados:** telefone, nome quando aparece, conteúdo livre das mensagens, data e hora.
- **Base legal:** execução de contrato ou procedimentos preliminares (art. 7, V) — o
  cliente mandou mensagem para comprar ([BASE-LEGAL.md](BASE-LEGAL.md)).
- **Compartilhamento:** nenhum nesta etapa. O webhook grava e enfileira.
- **Retenção:** 24 meses após o último contato, proposto em
  [BASE-LEGAL.md](BASE-LEGAL.md#3-prazos-de-retenção-por-finalidade) para o controlador
  confirmar. Hoje não há expurgo automático (#46) — e isso está declarado, não escondido.
- **Segurança:** webhook exige identificador do provedor; conteúdo do cliente entra
  delimitado como dado não confiável; dado sensível é retirado antes de qualquer análise
  (#82).
- **Estado:** existe.

## 2. Cadastro e resolução de Lead

- **Finalidade:** saber de quem é cada conversa, e ligar mensagens ao mesmo cliente.
- **Titulares:** clientes da empresa.
- **Dados:** telefone normalizado, nome quando informado, data de criação.
- **Base legal:** art. 7, V — sem saber de quem é a conversa não há atendimento.
- **Compartilhamento:** nenhum.
- **Retenção:** 24 meses após o último contato; exclusão a pedido do titular (#46).
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
- **Base legal:** legítimo interesse (art. 7, IX), com as salvaguardas da #89 — contato
  profissional em contexto de negócio.
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
- **Base legal:** legítimo interesse (art. 7, IX), com a avaliação da
  [BASE-LEGAL.md](BASE-LEGAL.md#2-avaliação-de-legítimo-interesse). Depende do canal de
  oposição funcionando (#81) — e ele existe.
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
- **Base legal:** legítimo interesse do controlador (art. 7, IX) sobre a própria operação:
  usa o vínculo, não o conteúdo.
- **Compartilhamento:** nenhum.
- **Retenção:** prazo contábil/fiscal do controlador; não segue o prazo da conversa.
- **Segurança:** não guarda conteúdo de mensagem, apenas o vínculo e o valor.
- **Estado:** existe.

## 6. Log da aplicação

- **Finalidade:** operar e depurar o sistema.
- **Titulares:** clientes; vendedores.
- **Dados:** identificadores, marcadores de PII (`[TEL_1]`) e metadados. **Mesmo mascarado é
  dado pessoal**: o marcador é reversível pelo mapa, e o `deal_id` ao lado identifica.
- **Base legal:** legítimo interesse (art. 7, IX) na operação e depuração do sistema.
- **Compartilhamento:** nenhum hoje; ferramenta de observabilidade externa entraria como
  suboperador.
- **Retenção:** 6 meses — prazo de depuração real; além disso ninguém abre.
- **Segurança:** teste que **falha o build** se PII vazar em log ou payload (#43).
- **Estado:** existe.

## 7. Varredura de negócios parados (Vigia)

- **Finalidade:** avisar o vendedor sobre negócio esquecido, cliente calado e proposta
  esfriando.
- **Titulares:** clientes.
- **Dados:** datas de mensagem e de estágio; a última fala do cliente é citada no alerta.
- **Base legal:** legítimo interesse (art. 7, IX), o mesmo da análise, com tratamento
  menor: a varredura é determinística e não chama modelo.
- **Compartilhamento:** nenhum.
- **Retenção:** não cria registro novo além do alerta em log.
- **Segurança:** só varre negócios abertos; não repete o mesmo alerta.
- **Estado:** issue #53.

## 8. Índice de embeddings (RAG)

- **Finalidade:** recuperar precedentes de conversas que fecharam com objeção semelhante.
- **Titulares:** clientes; terceiros citados nas conversas indexadas.
- **Dados:** trechos de conversa vetorizados. **Vetor é dado pessoal** — derivado do texto,
  recuperável por semelhança, apontando para o mesmo titular.
- **Base legal:** legítimo interesse (art. 7, IX), com a salvaguarda mais forte — é onde a
  expectativa do titular é mais frágil: a conversa dele ajuda a vender para outra pessoa.
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

## 10. Trilha de auditoria

- **Finalidade:** saber quem acessou o dado de qual titular, para conter e dimensionar
  incidente (#84) e responder ao titular que pergunta.
- **Titulares:** clientes (como alvo do acesso); vendedores (como autores).
- **Dados:** id do usuário, id do lead, operação, origem, data e hora. **Não guarda
  conteúdo de mensagem nem valor de campo.**
- **Base legal:** cumprimento de obrigação legal (segurança e resposta a incidente) e
  legítimo interesse na proteção do próprio tratamento.
- **Compartilhamento:** nenhum; vai ao controlador e à ANPD apenas quando houver incidente.
- **Retenção:** 24 meses — precisa cobrir um incidente descoberto tarde, que é a regra e
  não a exceção.
- **Segurança:** registro imutável; índices por usuário e por titular para a consulta
  rodar no dia em que ninguém tem tempo.
- **Estado:** existe.

## 11. Dados de parceiro comercial

- **Finalidade:** manter a relação com fornecedores, transportadoras e representantes —
  compra, prazo de entrega, condição de fornecimento.
- **Titulares:** o contato na empresa parceira. **É pessoa física**, ainda que a relação
  seja B2B.
- **Dados:** nome, telefone, cargo, e o conteúdo da conversa comercial.
- **Base legal:** execução de contrato (art. 7, V) — base **própria**, distinta da usada
  para clientes.
- **Compartilhamento:** nenhum. Conversa de parceiro **não vai** para a base de precedentes
  de venda.
- **Retenção:** enquanto durar a relação de fornecimento, mais o prazo contratual.
- **Segurança:** minimização (não guardar o que não se usa); isolamento do índice de
  precedentes, cobrado por teste nas duas direções; consulta separável no banco.
- **Estado:** existe (a marcação e o isolamento; o índice em si é #60).

---

## O que este registro ainda não fecha

| Assunto | Issue |
|---|---|
| Onde o provedor de IA processa fisicamente | #79 |
| Confirmação dos prazos de retenção pelo controlador, em contrato | #77 |
| Aviso de transparência ao titular | #80 |
| Exclusão em cascata, incluindo índice e cache | #46 |
