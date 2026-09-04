# Demo de 5 minutos, rodando offline

Roteiro para mostrar o Copiloto sem internet, sem chave de provedor e sem infraestrutura
além do que já está na máquina (#55).

**Os números aqui foram medidos**, nesta ordem, em 04/09/2026: a API sobe em ~1s, o webhook
responde **202 em 25ms**, `dotnet build` leva 2,6s e a suíte inteira — 237 testes — roda em
**5,2s**. Os tempos de fala são estimativa; os de comando, não.

> **O que esta demo não mostra**, e é melhor dizer antes de alguém perguntar: não há tela
> (#50), botão "por que essa sugestão" (#51) nem painel de ROI. O pipeline de modelo também
> não existe ainda — o que existe são as garantias em volta dele, que é justamente o que
> este projeto tem de diferente.

---

## Antes de começar (fora do relógio)

```bash
cd copiloto-crm
dotnet build          # ~3s, para o primeiro comando da demo não gastar esse tempo
```

Deixe dois terminais abertos: um para a API, outro para os comandos. **Não crie o `.env`** —
a ausência dele é parte da demonstração.

---

## Ato 1 · Sobe sem nada (40s)

```bash
dotnet run --project src/Copiloto.Api --urls http://localhost:5199
curl -s localhost:5199/saude
```

> "Não tem `.env`, não tem Postgres no ar, não tem chave de IA em lugar nenhum. Subiu em um
> segundo. Isso é decisão: `CONVERSATION_SOURCE=fake` e `MODEL_PROVIDER=fake` são o padrão,
> então a suíte e a demo rodam offline e de graça. Demo que depende de cinco contêineres no
> ar tem cinco maneiras de falhar ao vivo."

## Ato 2 · O webhook responde na hora, e a IA nunca roda no handler (60s)

```bash
curl -i -X POST localhost:5199/webhook/whatsapp \
  -H 'Content-Type: application/json' \
  -d '{"providerMessageId":"wamid.demo1","de":"+55 11 98888-1111",
       "para":"+55 11 3333-4444","texto":"to com refluxo, posso tomar cafe?",
       "enviadaEm":"2026-09-04T13:00:00Z"}'
```

Aponte para os dois lugares: o `202` no terminal do curl, e a linha do worker no terminal
da API — *"processada fora do webhook"*.

> "Vinte e cinco milissegundos, e o `202` é deliberado: `200` diria 'processado', e o que
> aconteceu foi 'recebido e enfileirado'. Processar IA dentro do handler é o erro clássico
> — provedor lento vira timeout na origem, timeout vira reentrega, e reentrega vira **custo
> duplicado**. Aqui o custo é dinheiro, não CPU."

## Ato 3 · A regra que o sistema não deixa quebrar (70s)

```bash
dotnet test --filter "AncoragemMcpTeste|DadoSensivelTeste"
```

Leia dois nomes de teste em voz alta, que é onde está o argumento:

- `Com_estoque_farto_o_agente_nao_consegue_produzir_escassez`
- `O_trecho_sensivel_nao_chega_ao_modelo_mas_o_pedido_chega`

> "A ancoragem não é uma instrução no prompt — é o formato da chamada. Nenhum método aceita
> o número que vai ser dito ao cliente: quem quer sugerir escassez informa o produto, e o
> valor vem da ferramenta. E olha o segundo caso: com 140kg em estoque, a ferramenta
> **responde** — e o dado não sustenta a fala. Sugerir 'restam poucas' com o depósito cheio
> é publicidade enganosa, e o erro não seria do modelo: seria de quem só conferiu se veio
> resposta."
>
> "A mensagem que eu mandei tinha 'refluxo'. Isso é dado de saúde, art. 11. O trecho não
> chega ao modelo — mas o *pedido* chega, porque a venda está na segunda metade da frase."

## Ato 4 · A ficha, e o que o cliente pode exigir dela (60s)

```bash
dotnet test --filter "FatoOuImpressaoTeste|FichaSobLgpdTeste"
```

> "A ficha separa **fato** de **impressão**, com procedência. Numa lista só, 'é gerente de
> compras' e 'parece desconfiado' chegam ao modelo com o mesmo peso — e o palpite do
> vendedor volta para ele reembalado como conclusão do sistema, o que confirma o viés em
> vez de corrigir. Impressão não ancora escassez, prazo nem preço."
>
> "E o vendedor vê na tela que o cliente pode pedir para ler o que está escrito ali. Isso
> muda o que se escreve: vira 'prefere objetividade' em vez de 'chato pra caramba' — e o
> primeiro continua servindo para vender."

## Ato 5 · A prova de que é sistema, e não brinquedo (70s)

```bash
docker ps            # nenhum contêiner do projeto no ar
dotnet test          # 237 testes, ~5s, sem rede
```

> "Reparem que **o Postgres nunca subiu** nesta demo. O webhook respondeu, o worker
> processou, e a suíte inteira passa — 237 testes em cinco segundos, offline. O que quebra
> quando a infraestrutura cai está resolvido por desenho, não por sorte: fila fora faz o
> webhook devolver 503 para o WhatsApp **reentregar**, em vez de aceitar e perder."

Se quiser encerrar com o argumento mais curto do projeto:

> "O robô não fala com o cliente. Sugestão de fala pronta falha na frente do cliente e
> queima a confiança do vendedor para sempre; leitura de conversa falha **na tela**, antes
> de qualquer dano. O vendedor discorda, ajusta, e segue usando."

---

## Se algo der errado ao vivo

| Sintoma | O que fazer, e o que dizer |
|---|---|
| Porta 5199 ocupada | `--urls http://localhost:5299`. Diga que a porta é configuração, não código |
| `dotnet run` demora | O build ficou para trás; rode `dotnet build` antes e siga pelo Ato 3, que não depende da API |
| Um teste falha | **Mostre a falha.** Uma suíte que reprova é o argumento, não o constrangimento — leia a mensagem do teste em voz alta e diga o que ela protege |
| Perguntarem pela tela | "Não existe ainda, é a #50. O que existe é o que decide se a tela vale alguma coisa" |

## O que entra quando existir

- **Passo do provedor derrubado ao vivo** (o esboço original da issue): depende do provedor
  real e do `/saude` por dependência (#72). Hoje o equivalente honesto é o Ato 5 — mostrar
  que nada disso está no ar e o sistema funciona mesmo assim.
- Dossiê com objeção velada e lacunas (#41 em diante), botão "por que essa sugestão" (#51),
  painel de ROI com a ressalva de viés dita em voz alta (#34).
