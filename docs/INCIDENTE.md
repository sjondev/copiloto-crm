# Resposta a incidente com dado pessoal

Procedimento para quando dado pessoal escapa, é acessado indevidamente ou se perde
(art. 48 · #84). **Minuta técnica: precisa de revisão jurídica junto do controlador**, que
é quem comunica a ANPD e os titulares — o Copiloto é operador e comunica **o controlador**
([LGPD.md](LGPD.md#25-quem-responde-pelo-quê)).

Este documento é curto de propósito. Plano de incidente que ninguém lê inteiro em cinco
minutos não é usado no dia em que precisa.

---

## 1. Os cinco passos, na ordem

| Passo | O que fazer | Quem |
|---|---|---|
| **Detectar** | Registrar o horário em que se soube, e por qual sinal | Quem percebeu |
| **Conter** | Cortar o acesso antes de investigar: revogar credencial, desligar integração, girar chave | Quem estiver de plantão, **sem esperar autorização** |
| **Avaliar** | Quais dados, quantos titulares, se há risco relevante | Responsável técnico + controlador |
| **Comunicar** | Controlador imediatamente; ANPD e titulares conforme a avaliação | Controlador decide; operador fornece os fatos |
| **Registrar** | O que aconteceu, o que se fez, o que mudou para não repetir | Responsável técnico |

**Conter vem antes de avaliar, e isso é decisão.** A tentação é entender primeiro para não
agir errado — mas cada minuto de investigação com o acesso ainda aberto é um minuto de
incidente ainda acontecendo. Credencial revogada por engano custa uma reconexão; credencial
viva durante a investigação custa o incidente inteiro de novo.

## 2. Quem decide, e em quanto tempo

- **Contenção:** imediata, sem autorização. Quem está de plantão corta.
- **Comunicação ao controlador:** sem demora injustificada, com o que se sabe **até
  então** — esperar o quadro completo é o erro que atrasa a comunicação legal dele.
- **Comunicação à ANPD e aos titulares:** decisão do **controlador**, com base na avaliação
  de risco. O operador entrega os fatos e a lista de afetados; não decide por ele.
- **Prazo interno alvo:** primeira comunicação ao controlador em **até 4 horas** do
  conhecimento. É meta operacional, não prazo legal.

## 3. A pergunta que decide o tamanho: quem foi afetado?

Sem trilha de auditoria, a resposta honesta é "não sabemos" — e "não sabemos" obriga a
tratar **todos** os titulares como afetados. Um incidente de 40 pessoas vira um evento de
reputação sobre a base inteira.

A trilha (`acessos`) responde três perguntas, e as três estão em código com teste:

- **quais titulares uma credencial alcançou** numa janela → vazamento de credencial;
- **quem tocou neste titular** → pergunta do próprio titular;
- **o que saiu da nossa rede** (`EnviouParaModelo`) → chave de provedor vazada.

A trilha guarda quem, qual titular, o quê, quando e por onde — **e não o conteúdo**. Trilha
que copia dado pessoal vira a segunda cópia a proteger, e a primeira a vazar junto.

## 4. Cenários deste projeto

Um CRM comum não tem as duas primeiras superfícies.

### 4.1 Vazamento do índice de embeddings

**O que é:** vetores derivados de conversas reais, recuperáveis por semelhança.

**Por que é pior do que parece:** vetor não parece dado pessoal — parece número —, então
tende a ficar fora do inventário e do controle de acesso. E o conteúdo é conversa de
cliente, não metadado.

**Conter:** cortar o acesso ao banco vetorial; girar credenciais; suspender indexação.
**Avaliar:** quais conversas foram indexadas na janela (a indexação é registrada).
**Não repetir:** PII mascarada antes de indexar e isolamento por titular já são requisito
(#62); o incidente testa se eles estavam mesmo ligados.

### 4.2 Credencial do servidor MCP exposta

**O que é:** o servidor MCP existe para um agente consumir **em volume** — é a superfície
com maior razão de dano por minuto do projeto.

**Conter:** revogar o token, derrubar o servidor MCP se necessário. O CRM continua
funcionando sem ele.
**Avaliar:** `VolumeAnormalPorMcp` e a lista de titulares alcançados pelo token.
**Não repetir:** escopo por usuário, limite de volume e auditoria por chamada (#58).

### 4.3 Chave de provedor de IA vazada

**O que é:** custo alheio na nossa conta e, dependendo do provedor, acesso a histórico de
chamadas — que contém conversa pseudonimizada.

**Conter:** girar a chave no provedor **antes** de investigar; conferir o gasto.
**Avaliar:** `TitularesEnviadosParaModelo` na janela.
**Não repetir:** chave só por variável de ambiente, varredura de segredo no CI (#47).

### 4.4 Dump do Postgres

**O que é:** o pior caso — tudo, sem máscara, incluindo o mapa que reverte a
pseudonimização.

**Conter:** girar credenciais do banco, revogar acesso de rede, preservar log para
investigação.
**Avaliar:** o dump alcança **todos** os titulares; aqui não há redução por trilha.
**Não repetir:** acesso ao banco só pela aplicação, backup cifrado, credencial nunca em
repositório.

## 5. Modelos de comunicação

### 5.1 Ao controlador (imediata, incompleta é aceitável)

> **Incidente de segurança — comunicação inicial**
> Detectamos em *[data e hora]* que *[o que aconteceu, em uma frase]*.
> **Já contivemos** *[o que foi cortado]*.
> **Ainda estamos apurando** *[o que falta]*.
> **Dados possivelmente envolvidos:** *[categorias]*.
> **Titulares possivelmente afetados:** *[número, ou "em apuração"]*.
> Próxima atualização em *[prazo]*.

### 5.2 À ANPD (o controlador comunica)

Natureza dos dados, titulares envolvidos, medidas técnicas e de segurança utilizadas,
riscos, motivo da demora se houver, e medidas adotadas para reverter ou mitigar.

### 5.3 Ao titular

> Olá, *[nome]*. Precisamos avisar que, em *[data]*, *[o que aconteceu]*, e isso pode ter
> envolvido *[quais dados seus]*. **O que já fizemos:** *[contenção]*. **O que você pode
> fazer:** *[orientação prática, se houver]*. Se quiser saber exatamente o que temos sobre
> você, é só pedir: *[canal]*.

Sem juridiquês e sem "lamentamos o ocorrido" antes de dizer o que aconteceu — a pessoa
precisa entender o risco dela em uma leitura.

## 6. Depois: o registro

Todo incidente encerra com um registro do que aconteceu, o que foi feito, e **o que mudou
no sistema** para não repetir. Incidente que fecha sem mudança vira o mesmo incidente
depois — e, na segunda vez, com a agravante de já se saber.
