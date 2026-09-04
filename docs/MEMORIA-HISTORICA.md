# Memoria historica: o GitHub como parte do sistema

> Escrito por Jonatas. Este documento define COMO trabalhar; o `CLAUDE.md` define
> o que o produto e e o que nao se negocia nele. Os dois valem juntos.

## Memoria historica do produto — o GitHub e parte do sistema

O projeto nao tem apenas codigo.

O projeto tem uma historia.

A historia registra como uma regra foi descoberta, por que uma decisao foi tomada, quais erros revelaram informacoes novas, quais comportamentos legados precisaram ser preservados e quais limites tecnicos condicionam o desenho atual.

**GitHub e a memoria persistente dessa historia.**

Issues, comentarios, PRs, commits e testes formam uma memoria de engenharia que deve sobreviver a troca de desenvolvedor, agente, modelo, computador ou sessao.

O objetivo nao e apenas construir o Copiloto.

O objetivo e fazer com que o proximo agente consiga entender **por que o Copiloto e como e** sem precisar redescobrir tudo.

### Principio fundamental

O codigo responde:

> O que o sistema faz hoje?

A historia responde:

> Por que ele faz isso?

Os dois sao parte do produto.

---

## O agente deve aprender durante a implementacao

Nunca assumir que a issue original contem todo o conhecimento necessario.

Durante a investigacao, procurar:

* regras de negocio implicitas;
* regras de negocio nao documentadas;
* contradicoes entre requisito e codigo;
* comportamento legado;
* decisoes arquiteturais existentes;
* decisoes de produto;
* dependencias entre funcionalidades;
* limitacoes tecnicas;
* riscos;
* melhorias futuras;
* erros que revelem conhecimento novo.

Quando encontrar algo relevante, o agente deve:

**descobrir → registrar → reavaliar → implementar → verificar**

Nunca continuar uma implementacao baseada em uma premissa que acabou de ser invalidada.

---

## Tipos de conhecimento historico

Toda descoberta relevante deve pertencer a exatamente um tipo:

* `IMPL_ERROR` — erro de implementacao
* `BUSINESS_RULE` — regra de negocio descoberta
* `BUSINESS_AMBIGUITY` — regra de negocio ambigua
* `ARCH_DECISION` — decisao arquitetural
* `PRODUCT_DECISION` — decisao de produto
* `LEGACY_BEHAVIOR` — comportamento legado
* `TECH_CONSTRAINT` — limitacao tecnica
* `FEATURE_DEPENDENCY` — dependencia entre funcionalidades
* `RISK` — risco descoberto
* `FUTURE_IMPROVEMENT` — melhoria futura

Nao criar categoria nova sem necessidade.

---

## O que merece entrar na historia

Nao registrar ruido.

Nao transformar cada erro de compilacao, typo, comando falho ou ajuste trivial em historia do produto.

Registrar quando o acontecimento produzir conhecimento reutilizavel.

### Registrar

* uma regra de negocio descoberta;
* uma premissa que se mostrou falsa;
* uma decisao arquitetural relevante;
* uma decisao de produto;
* comportamento legado que precisa ser preservado;
* uma limitacao tecnica que condiciona futuras implementacoes;
* dependencia importante entre funcionalidades;
* risco real identificado;
* melhoria que deve ser tratada posteriormente.

### Nao registrar

* erro trivial de sintaxe;
* erro temporario de comando;
* formatacao;
* typo;
* tentativa descartada sem aprendizado;
* informacao ja registrada.

A historia deve ter **alta densidade de conhecimento**.

---

## Evidencia antes de conclusao

Nunca transformar observacao em regra de negocio sem evidencia.

Usar:

**OBSERVACAO**
→ **EVIDENCIA**
→ **HIPOTESE**
→ **CONFIANCA**
→ **DECISAO**

Exemplo:

Errado:

> Pedidos pagos nunca podem ser cancelados.

Correto:

> O PaymentService rejeita o cancelamento de pedidos pagos.

Depois investigar por que.

Se testes, historico, codigo ou comportamento consistente demonstrarem que o motivo e uma regra financeira, registrar a regra.

Se nao houver evidencia suficiente, registrar como `BUSINESS_AMBIGUITY`.

**Nunca inventar memoria para parecer confiante.**

---

## Nivel de confianca

Descobertas podem possuir:

* `HIGH`
* `MEDIUM`
* `LOW`

### HIGH

Existe evidencia forte em multiplas fontes, como:

* codigo;
* testes;
* documentacao;
* historico Git;
* comportamento observavel.

### MEDIUM

Existe evidencia consistente, mas nao completa.

### LOW

Existe principalmente inferencia.

Uma descoberta `LOW` que alteraria significativamente o comportamento do produto nao deve virar decisao silenciosa.

Registrar a ambiguidade.

---

## Formato dos registros historicos

Quando uma descoberta relevante acontecer, adicionar um comentario conciso na propria Issue.

O comentario deve seguir:

```text
<!-- HISTORY:<TYPE> -->

### <ID> — <Titulo curto>

**Discovery**
<O que foi descoberto?>

**Evidence**
<Qual e a evidencia?>

**Impact**
<Como isso afeta a implementacao?>

**Decision**
<Qual decisao foi tomada?>

**Confidence**
HIGH | MEDIUM | LOW
```

O registro deve ser curto, factual e pesquisavel.

Nao escrever ensaios.

Nao repetir informacoes ja registradas.

---

## IMPL_ERROR

Usar quando o agente cometer um erro de implementacao que gere conhecimento reutilizavel.

```text
<!-- HISTORY:IMPL_ERROR -->

### IMPL-<ID> — <Titulo>

**Failure**
<O que falhou?>

**Incorrect Assumption**
<O que foi assumido incorretamente?>

**Root Cause**
<Por que estava errado?>

**Correction**
<O que foi alterado?>

**Reusable Knowledge**
<O que futuros agentes devem saber?>

**Confidence**
HIGH | MEDIUM | LOW
```

Um erro trivial nao precisa ser registrado.

---

## BUSINESS_RULE

Usar quando uma regra de negocio real for descoberta.

```text
<!-- HISTORY:BUSINESS_RULE -->

### BR-<ID> — <Titulo>

**Discovery**
<Regra descoberta>

**Evidence**
<Evidencia>

**Impact**
<Impacto>

**Decision**
<Como a implementacao deve respeitar a regra>

**Confidence**
HIGH | MEDIUM | LOW
```

Sempre que possivel, transformar a regra em teste automatizado.

---

## BUSINESS_AMBIGUITY

Usar quando nao houver evidencia suficiente para determinar o comportamento correto.

```text
<!-- HISTORY:BUSINESS_AMBIGUITY -->

### BA-<ID> — <Titulo>

**Ambiguity**
<O que nao esta claro?>

**Evidence**
<Quais evidencias entram em conflito?>

**Possible Interpretations**
<Interpretacoes possiveis>

**Safe Behavior**
<O que pode ser feito sem assumir uma regra inventada?>

**Required Clarification**
<O que precisa ser decidido?>

**Confidence**
LOW | MEDIUM
```

Nao escolher arbitrariamente entre regras de negocio conflitantes.

---

## ARCH_DECISION

Usar para decisoes arquiteturais relevantes.

```text
<!-- HISTORY:ARCH_DECISION -->

### ARCH-<ID> — <Titulo>

**Context**
<Problema>

**Options**
<Alternativas consideradas>

**Decision**
<Decisao>

**Reason**
<Por que?>

**Trade-offs**
<O que foi ganho/perdido?>

**Consequences**
<O que futuros agentes precisam saber?>

**Confidence**
HIGH | MEDIUM
```

Respeitar as decisoes arquiteturais ja existentes no projeto.

Nao substituir uma decisao registrada sem evidencia de que ela deve mudar.

---

## PRODUCT_DECISION

Usar quando o comportamento do produto for definido ou esclarecido.

```text
<!-- HISTORY:PRODUCT_DECISION -->

### PD-<ID> — <Titulo>

**Context**
<Problema>

**Decision**
<Comportamento escolhido>

**Reason**
<Por que?>

**Impact**
<Impacto no produto>

**Confidence**
HIGH | MEDIUM
```

Diferenciar comportamento do produto de detalhe de implementacao.

---

## LEGACY_BEHAVIOR

Usar quando um comportamento existente parecer estranho, mas houver razao para preserva-lo.

```text
<!-- HISTORY:LEGACY_BEHAVIOR -->

### LEGACY-<ID> — <Titulo>

**Observed Behavior**
<O que existe hoje?>

**Evidence**
<Evidencia>

**Reason for Preservation**
<Por que pode existir dependencia?>

**Risk of Changing**
<O que pode quebrar?>

**Decision**
<Preservar / alterar / investigar>

**Confidence**
HIGH | MEDIUM | LOW
```

Nunca remover comportamento legado apenas porque parece errado.

Investigar primeiro.

---

## TECH_CONSTRAINT

Usar para limitacoes tecnicas que possam influenciar implementacoes futuras.

```text
<!-- HISTORY:TECH_CONSTRAINT -->

### TECH-<ID> — <Titulo>

**Constraint**
<Limitacao>

**Evidence**
<Evidencia>

**Impact**
<Impacto>

**Required Approach**
<Como futuras implementacoes devem respeitar isso>

**Confidence**
HIGH | MEDIUM
```

---

## FEATURE_DEPENDENCY

Usar quando uma funcionalidade depender de outra.

```text
<!-- HISTORY:FEATURE_DEPENDENCY -->

### DEP-<ID> — <Titulo>

**Feature**
<Funcionalidade atual>

**Depends On**
<Dependencia>

**Reason**
<Por que?>

**Impact**
<Consequencia>

**Future Direction**
<Manter / reduzir / remover>

**Confidence**
HIGH | MEDIUM
```

---

## RISK

Usar somente para riscos reais apoiados por evidencia.

```text
<!-- HISTORY:RISK -->

### RISK-<ID> — <Titulo>

**Risk**
<O que pode acontecer?>

**Evidence**
<Por que isso e um risco real?>

**Probability**
LOW | MEDIUM | HIGH

**Impact**
LOW | MEDIUM | HIGH

**Mitigation**
<Como reduzir o risco?>

**Confidence**
HIGH | MEDIUM
```

Nao criar riscos especulativos apenas para preencher historico.

---

## FUTURE_IMPROVEMENT

Quando uma melhoria valida for descoberta fora do escopo atual:

Nao implementar automaticamente.

Registrar:

```text
<!-- HISTORY:FUTURE_IMPROVEMENT -->

### FUTURE-<ID> — <Titulo>

**Observation**
<Limitacao atual>

**Improvement**
<O que poderia melhorar?>

**Benefit**
<Beneficio>

**Reason Out of Scope**
<Por que nao agora?>

**Suggested Follow-up**
<Se deve virar nova Issue>
```

Se a melhoria for relevante, criar uma nova Issue em vez de contaminar a Issue atual.

---

# Memoria em camadas

O agente NAO deve ler todo o historico do projeto a cada Issue.

Isso desperdicaria contexto.

A memoria deve ser recuperada por relevancia.

### Camada 1 — Issue atual

Sempre ler:

* descricao;
* comentarios;
* labels;
* estado;
* links relevantes.

### Camada 2 — Historico relacionado

Pesquisar apenas conceitos relevantes para a Issue atual:

* entidades;
* funcionalidades;
* regras;
* componentes;
* servicos;
* dependencias.

### Camada 3 — Historico arquitetural e de produto

Consultar decisoes globais quando a implementacao tocar:

* arquitetura;
* dominio;
* seguranca;
* LGPD;
* persistencia;
* integracoes;
* orquestracao;
* comportamento central do produto.

### Camada 4 — Git

Usar:

```bash
git log
git log -- <arquivo>
git blame <arquivo>
```

quando for necessario descobrir por que um comportamento existe.

Nao usar Git history indiscriminadamente.

---

# O historico deve ser recuperavel

Ao registrar uma descoberta, utilizar:

* labels quando apropriado;
* referencias para Issues relacionadas;
* IDs estaveis;
* nomes curtos;
* termos pesquisaveis.

Exemplos:

```text
BR-014
ARCH-008
LEGACY-003
TECH-011
DEP-007
```

O objetivo e permitir que um agente futuro encontre rapidamente a informacao.

---

# Reavaliacao obrigatoria

Uma descoberta relevante pode invalidar trabalho que ja foi feito.

Quando isso acontecer:

**DISCOVER**
→ **DOCUMENT**
→ **RE-EVALUATE**
→ **CORRECT**
→ **TEST**
→ **REVIEW**

Perguntar internamente:

1. Minha implementacao ainda esta correta?
2. Alguma premissa foi invalidada?
3. Alguma regra de negocio mudou?
4. Algum teste precisa mudar?
5. Alguma outra parte do codigo foi afetada?
6. Existe risco de regressao?
7. Outra Issue passou a depender desta descoberta?

Nunca preservar uma implementacao apenas porque ela ja foi escrita.

A evidencia mais recente prevalece sobre uma premissa anterior, desde que a evidencia seja confiavel.

---

# Testes como memoria executavel

Sempre que uma regra importante puder ser expressa por teste, fazer isso.

A historia explica:

**POR QUE.**

O teste protege:

**O QUE NAO PODE DEIXAR DE SER VERDADE.**

Exemplo:

```text
BR-014
Pedidos pagos exigem fluxo de estorno.

        ↓

Test
cancellation_of_paid_order_requires_refund

        ↓

Implementation
```

Nao enfraquecer ou remover teste apenas para deixar a esteira verde.

---

# Continuidade entre agentes

O agente deve assumir que:

* outro agente pode ter trabalhado antes;
* outro agente pode trabalhar depois;
* a sessao atual pode ser encerrada a qualquer momento;
* o modelo pode ser trocado;
* o desenvolvedor pode ser trocado.

Portanto, conhecimento importante nao deve existir apenas no contexto atual.

Se algo for importante para uma futura implementacao, registre no GitHub.

**O conhecimento nao pode depender da memoria da sessao.**

---

# GitHub como memoria, nao como diario

Nao registrar cada passo do agente.

Nao escrever:

> "Agora vou abrir o arquivo X."

Nao escrever:

> "Executei o comando Y."

Nao escrever:

> "Depois disso eu pensei em Z."

Registrar apenas conhecimento que outro engenheiro ou agente possa reutilizar.

A pergunta para decidir se algo merece historia e:

> "Um futuro engenheiro economizaria tempo sabendo disso?"

Se nao, provavelmente nao precisa ser registrado.

---

# Economia de contexto

Contexto e recurso de engenharia.

Maximizar:

**CONHECIMENTO RELEVANTE / TOKEN CONSUMIDO**

Evitar:

* ler arquivos inteiros sem necessidade;
* ler todas as Issues;
* repetir buscas;
* repetir conclusoes;
* copiar logs enormes;
* gerar comentarios longos;
* explicar cada comando executado;
* recuperar historico irrelevante.

Preferir:

* busca direcionada;
* contexto minimo necessario;
* registros estruturados;
* testes especificos;
* diffs pequenos;
* evidencias concretas.

O objetivo nao e gastar o maximo de tokens.

O objetivo e obter o maximo de **engenharia verificavel por token**.

---

# Estado interno compacto

Quando o contexto crescer, manter mentalmente um estado compacto:

```text
CURRENT ISSUE:
OBJECTIVE:
RELEVANT HISTORY:
CONFIRMED BUSINESS RULES:
DISCOVERIES:
ARCHITECTURAL DECISIONS:
LEGACY BEHAVIOR:
CURRENT IMPLEMENTATION:
FAILING TESTS:
AMBIGUITIES:
NEXT ACTION:
```

Nao repetir investigacoes ja concluídas.

---

# Fechamento da Issue

Antes de fechar uma Issue:

1. revisar o requisito original;
2. revisar descobertas;
3. revisar regras de negocio;
4. revisar decisoes arquiteturais;
5. revisar comportamento legado;
6. revisar dependencias;
7. revisar diff;
8. verificar testes e esteira;
9. verificar se documentacao passou a divergir do codigo;
10. registrar o resultado historico.

Adicionar um comentario final:

```text
## Engineering History

**Original Objective**
<Objetivo>

**Important Discoveries**
- <descoberta>

**Business Rules**
- <regra>

**Technical Decisions**
- <decisao>

**Legacy Behavior**
- <comportamento>

**Implementation**
<O que mudou>

**Verification**
<Testes / CI>

**Remaining Risks**
<Riscos reais>

**Follow-ups**
<Issues futuras>
```

Depois disso, fechar a Issue somente se estiver realmente concluida.

---

# Regra de continuidade

Depois de fechar uma Issue:

1. atualizar a fila;
2. identificar a proxima Issue acionavel;
3. recuperar somente o historico relevante;
4. continuar.

Nao pedir autorizacao entre Issues independentes.

Nao parar simplesmente porque uma Issue foi concluida.

Parar somente quando:

* nao houver Issues acionaveis;
* todas as restantes estiverem bloqueadas;
* for necessaria uma decisao humana material;
* o ambiente impedir o trabalho;
* o contexto, tokens ou ferramentas nao permitirem continuar.

---

# Principio final

O agente nao deve apenas modificar o estado do software.

Ele deve aumentar o conhecimento do software.

Cada Issue pode produzir:

**CODIGO**
+
**TESTES**
+
**REGRAS**
+
**DECISOES**
+
**HISTORIA**

O trabalho de hoje deve reduzir a quantidade de redescoberta necessaria amanha.

O proximo agente deve conseguir olhar para o GitHub e entender:

* o que foi pedido;
* o que foi descoberto;
* o que estava errado;
* quais regras existem;
* por que determinadas decisoes foram tomadas;
* quais comportamentos devem ser preservados;
* quais limitacoes existem;
* o que ainda precisa ser feito.

**Construir o produto e construir sua memoria sao parte do mesmo trabalho.**