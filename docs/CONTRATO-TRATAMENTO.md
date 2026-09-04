# Minuta de cláusulas de tratamento de dados

**Isto é uma minuta técnica, escrita por quem conhece o sistema — não é parecer
jurídico.** Ela existe para que a conversa com o advogado comece do que o software
realmente faz, em vez de um modelo genérico baixado da internet que descreve um produto
que não é este. Cada cláusula abaixo tem o comportamento correspondente no código, e é por
isso que ela pode ser cobrada.

Papéis, e o porquê deles, estão em [LGPD.md](LGPD.md#25-quem-responde-pelo-quê): a empresa
cliente é **controladora**, o Copiloto é **operador**.

---

## 1. Objeto e finalidade

O operador trata dados pessoais **exclusivamente** para a finalidade de ler as conversas
comerciais do controlador e produzir, para os vendedores dele, contexto sobre cada
negociação.

O operador **não** envia mensagem ao cliente final. Quem escreve é o vendedor.

> Esta cláusula não é redacional: é a tese do produto, e sustenta que a única saída do
> sistema é uma tela interna.

## 2. Instruções do controlador

O operador trata os dados apenas conforme as instruções documentadas do controlador, e
comunica a ele se entender que uma instrução viola a legislação.

Uso dos dados do controlador para **finalidade própria do operador** — incluindo treinar
ou ajustar modelos, medir qualidade de produto ou compor base de exemplos — exige
autorização específica e por escrito. Sem ela, não acontece.

## 3. Confidencialidade e acesso

Acesso limitado a quem precisa, com registro. O acesso vale também para o dado
**pseudonimizado**: marcador é reversível pelo mapa que fica do lado do operador, e por
isso texto mascarado não é dado "liberado".

## 4. Segurança

O operador mantém, no mínimo:

- mascaramento de dado pessoal antes de qualquer saída para terceiro;
- isolamento entre titulares no que for indexado para recuperação;
- controle de acesso por perfil;
- registro das operações relevantes.

## 5. Suboperadores

O operador **só subcontrata com autorização** do controlador, e mantém a lista atualizada
em [LGPD.md](LGPD.md#a-cadeia-de-suboperadores), com o que cada um trata e onde processa.

Enviar conversa a um provedor de modelo **é** subcontratação. A autorização precisa ser
dada antes de a chave do provedor ser configurada — não depois do primeiro envio.

## 6. Direitos do titular

O titular exerce seus direitos perante o **controlador**. O operador fornece os meios
técnicos para atendê-los — confirmação, acesso, correção, portabilidade em formato legível
por máquina, oposição e exclusão —, incluindo **as inferências geradas pelo sistema**
("sensível a preço", "esfriando"), que são dado pessoal criado sobre o titular.

Prazo de apoio do operador ao controlador: a definir junto do prazo legal de resposta
(#81).

## 7. Incidentes

O operador comunica o controlador **sem demora injustificada** ao tomar conhecimento de
incidente com dado pessoal, com o que se sabe até então, o que ainda se investiga e as
medidas já tomadas. Rito e prazo: #84.

## 8. Término

Encerrado o contrato, o operador **elimina ou devolve** os dados, à escolha do
controlador, incluindo cópias em índice de recuperação, cache e log — guardando apenas o
que a lei obrigue a guardar, pelo prazo em que obrigue.

> Cache e índice nesta cláusula não são detalhe: são as duas cópias que costumam
> sobreviver a um "apagamos tudo" feito só no banco principal.

---

## O que esta minuta não cobre

Base legal por finalidade (#77), registro das operações de tratamento (#76), transferência
internacional (#79) e o aviso de transparência ao titular (#80). Cada um tem issue própria
— e nenhum deles cabe numa cláusula escrita por analogia.
