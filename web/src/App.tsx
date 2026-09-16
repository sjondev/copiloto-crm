import { useState } from "react";
import { Entrar } from "./Entrar";
import { Fila } from "./Fila";
import { Plano } from "./Plano";
import { esquecer, tokenGuardado } from "./sessao";
import { PainelDaConversa } from "./Conversa";
import { PainelDoDossie } from "./Dossie";
import { usarLeitura } from "./usarLeitura";

/**
 * A tela dividida (#164): a conversa a esquerda, o que lemos dela a direita.
 *
 * O layout carrega a tese do produto. O robo nao fala com o cliente — a esquerda
 * e do vendedor e do cliente, e a direita e contexto para ele decidir. Nao ha
 * caixa de "enviar sugestao" nesta tela, e isso e decisao, nao falta de tempo.
 */
export function App() {
  // A credencial vem antes de tudo (#182). Sem ela as rotas de leitura
  // respondem 401, e a tela mostraria "sem conexao com a API" para um problema
  // que e de login — o vendedor tentaria recarregar para sempre.
  const [token, setToken] = useState(() => tokenGuardado());

  if (token === null) return <Entrar aoEntrar={setToken} />;

  return <Copiloto aoSair={() => { esquecer(); setToken(null); }} />;
}

function Copiloto({ aoSair }: { aoSair: () => void }) {
  // Aba, e nao terceira coluna: a leitura e o plano competem pela mesma
  // atencao, e tres colunas numa tela de notebook deixam as tres ilegiveis.
  const [aba, setAba] = useState<"leitura" | "plano">("leitura");
  // Sem router e sem tela de lista ainda: o lead vem da URL, e a #12 e a #50
  // trazem navegacao. Campo na tela porque digitar um Guid na barra de
  // enderecos e pior que cola-lo num input.
  const [leadId, setLeadId] = useState(() =>
    new URLSearchParams(window.location.search).get("lead") ?? ""
  );

  const { conversa, dossie, carregando, analisando, canal, erro } = usarLeitura(leadId);

  return (
    <div className="tela">
      <header className="cabecalho">
        <h1 className="cabecalho__marca">Copiloto</h1>

        <label className="cabecalho__lead">
          <span>lead</span>
          <input
            value={leadId}
            onChange={(e) => setLeadId(e.target.value.trim())}
            placeholder="cole o id do lead"
            spellCheck={false}
          />
        </label>

        {/*
          O canal aparece SEMPRE, e nao so quando quebra. "verificando" nao e
          aviso de erro: e o estado honesto de quem esta buscando de tempos em
          tempos em vez de receber na hora — e o vendedor merece saber a
          diferenca antes de confiar que a tela esta em dia.
        */}
        {leadId && (
          <span className={`canal canal--${canal}`} title={
            canal === "ao-vivo"
              ? "recebendo a leitura assim que ela fica pronta"
              : "sem tempo real: verificando a cada poucos segundos"
          }>
            {canal === "ao-vivo" ? "ao vivo" : "verificando"}
          </span>
        )}

        {/*
          O erro aparece como aviso e a tela CONTINUA mostrando o que ja tinha.
          Trocar o conteudo por uma tela de erro apagaria a conversa que o
          vendedor esta lendo por causa de uma requisicao que falhou — e ele
          esta no meio de uma venda.
        */}
        {erro === "a sessao expirou" ? (
          <span className="cabecalho__erro" role="status">
            sessao expirada — <button type="button" className="cabecalho__sair" onClick={aoSair}>entrar de novo</button>
          </span>
        ) : erro ? (
          <span className="cabecalho__erro" role="status">sem conexao com a API — {erro}</span>
        ) : null}

        <button type="button" className="cabecalho__sair" onClick={aoSair}>sair</button>
      </header>

      {!leadId ? (
        // Sem lead escolhido, a porta e a FILA — e nao um campo pedindo Guid
        // (#174). O campo continua no cabecalho para quem ja tem o id na mao.
        <Fila aoEscolher={setLeadId} />
      ) : carregando ? (
        <p className="aviso">Carregando…</p>
      ) : (
        <main className="painel">
          <section className="painel__lado painel__lado--conversa">
            <h2 className="painel__titulo">Conversa</h2>
            <PainelDaConversa falas={conversa} />
          </section>

          <section className="painel__lado painel__lado--dossie">
            <h2 className="painel__titulo">
              <button
                type="button"
                className={`aba ${aba === "leitura" ? "aba--ativa" : ""}`}
                onClick={() => setAba("leitura")}
              >
                O que lemos
              </button>

              {/*
                O plano do VENDEDOR (#12). Fica ao lado da leitura porque e onde
                ele age em cima do que foi lido — e ele e' quem escreve.
              */}
              <button
                type="button"
                className={`aba ${aba === "plano" ? "aba--ativa" : ""}`}
                onClick={() => setAba("plano")}
              >
                Meu plano
              </button>

              {/*
                O intervalo entre a fala chegar e o dossie ficar pronto e visivel
                a olho nu. Tela parada nesse intervalo parece tela quebrada: o
                vendedor recarrega, nada acontece, e ele conclui que a ferramenta
                nao funciona.
              */}
              {analisando && aba === "leitura" && (
                <span className="analisando" role="status">analisando…</span>
              )}
            </h2>

            {aba === "leitura" ? <PainelDoDossie dossie={dossie} /> : <Plano leadId={leadId} />}
          </section>
        </main>
      )}
    </div>
  );
}
