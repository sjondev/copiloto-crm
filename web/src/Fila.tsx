import { useEffect, useState } from "react";
import { buscarFila } from "./api";
import type { LinhaDaFila } from "./tipos";

/**
 * A porta de entrada do CRM (#174).
 *
 * Antes disto a unica forma de abrir um lead era colar um Guid no cabecalho, e
 * vendedor nao decora Guid.
 *
 * A ordem vem PRONTA do backend e a tela nao reordena: a regra de quem atender
 * primeiro e decisao de produto, e reimplementa-la aqui criaria duas versoes
 * dela — e uma delas erraria primeiro.
 */

const ROTULO: Record<string, string> = {
  ClienteEmSilencio: "sem responder",
  PropostaEnvelhecendo: "proposta parada",
  NegocioParado: "negócio parado",
};

export function Fila({ aoEscolher }: { aoEscolher: (leadId: string) => void }) {
  const [linhas, setLinhas] = useState<LinhaDaFila[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    const controle = new AbortController();

    buscarFila(controle.signal)
      .then(setLinhas)
      .catch((e: unknown) => {
        if (e instanceof DOMException && e.name === "AbortError") return;
        setErro(e instanceof Error ? e.message : "não foi possível carregar a fila");
      });

    return () => controle.abort();
  }, []);

  if (erro) return <p className="aviso">{erro}</p>;
  if (linhas === null) return <p className="aviso">Carregando…</p>;

  if (linhas.length === 0) {
    return (
      <p className="aviso">
        Nenhuma conversa ainda. Assim que uma mensagem chegar, ela aparece aqui.
      </p>
    );
  }

  return (
    <ul className="fila">
      {linhas.map((linha) => (
        <li key={linha.leadId}>
          <button type="button" className="fila__linha" onClick={() => aoEscolher(linha.leadId)}>
            <span className="fila__quem">
              <strong>{linha.nome ?? linha.telefone}</strong>
              {linha.estagio && <span className="fila__estagio">{linha.estagio}</span>}

              {/*
                Aparece na LISTA, e nao so na tela do lead: sem isso o vendedor
                abre e fica esperando uma leitura que nunca vem (#81).
              */}
              {linha.analiseSuspensa && (
                <span className="fila__suspensa" title="o titular se opôs à análise por IA">
                  sem análise
                </span>
              )}
            </span>

            {/*
              O alerta vem com a fala que o originou, igual ao dossiê: sem a
              citação, ele é opinião do sistema e o vendedor só pode aceitar ou
              ignorar.
            */}
            {linha.alerta && (
              <span className={`fila__alerta fila__alerta--${linha.alerta}`}>
                {ROTULO[linha.alerta] ?? linha.alerta}
                {linha.diasEmSilencio !== null && ` há ${linha.diasEmSilencio}d`}
              </span>
            )}

            {linha.temperatura && <span className="fila__temp">{linha.temperatura}</span>}
            {linha.objecao && <span className="fila__objecao">{linha.objecao}</span>}

            {linha.motivo && <span className="fila__motivo">{linha.motivo}</span>}
          </button>
        </li>
      ))}
    </ul>
  );
}
