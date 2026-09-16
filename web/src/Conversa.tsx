import type { Fala } from "./tipos";

const HORA = new Intl.DateTimeFormat("pt-BR", { hour: "2-digit", minute: "2-digit" });
const DIA = new Intl.DateTimeFormat("pt-BR", { day: "2-digit", month: "short" });

export function PainelDaConversa({ falas }: { falas: Fala[] }) {
  if (falas.length === 0) {
    return (
      <div className="conversa conversa--vazia">
        <p>Nenhuma fala ainda.</p>
      </div>
    );
  }

  return (
    <ol className="conversa">
      {falas.map((fala, i) => {
        const quando = new Date(fala.enviadaEm);
        const anterior = i > 0 ? new Date(falas[i - 1]!.enviadaEm) : null;
        const mudouDeDia = anterior === null || anterior.toDateString() !== quando.toDateString();

        return (
          <li key={fala.id} className={`fala fala--${fala.autor.toLowerCase()}`}>
            {mudouDeDia && <div className="conversa__dia">{DIA.format(quando)}</div>}

            <div className="fala__balao">
              {/*
                Midia nao transcrita fica VISIVEL como lacuna, e nao escondida.
                O backend ja manda o marcador no texto ("[audio nao transcrito,
                14s]"); a etiqueta reforca que ali ha conteudo que ninguem leu.
              */}
              {fala.midia && <span className="fala__midia">{fala.midia.toLowerCase()}</span>}
              <p className="fala__texto">{fala.texto}</p>
              <time className="fala__hora" dateTime={fala.enviadaEm}>
                {HORA.format(quando)}
              </time>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
