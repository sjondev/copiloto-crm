import { useState } from "react";
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
  // Sem router e sem tela de lista ainda: o lead vem da URL, e a #12 e a #50
  // trazem navegacao. Campo na tela porque digitar um Guid na barra de
  // enderecos e pior que cola-lo num input.
  const [leadId, setLeadId] = useState(() =>
    new URLSearchParams(window.location.search).get("lead") ?? ""
  );

  const { conversa, dossie, carregando, erro } = usarLeitura(leadId);

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
          O erro aparece como aviso e a tela CONTINUA mostrando o que ja tinha.
          Trocar o conteudo por uma tela de erro apagaria a conversa que o
          vendedor esta lendo por causa de uma requisicao que falhou — e ele
          esta no meio de uma venda.
        */}
        {erro && <span className="cabecalho__erro" role="status">sem conexao com a API — {erro}</span>}
      </header>

      {!leadId ? (
        <p className="aviso">Informe um lead para ver a conversa e a leitura.</p>
      ) : carregando ? (
        <p className="aviso">Carregando…</p>
      ) : (
        <main className="painel">
          <section className="painel__lado painel__lado--conversa">
            <h2 className="painel__titulo">Conversa</h2>
            <PainelDaConversa falas={conversa} />
          </section>

          <section className="painel__lado painel__lado--dossie">
            <h2 className="painel__titulo">O que lemos</h2>
            <PainelDoDossie dossie={dossie} />
          </section>
        </main>
      )}
    </div>
  );
}
