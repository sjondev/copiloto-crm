import type { Dossie, Sinal } from "./tipos";

function BlocoDeSinal({ sinal }: { sinal: Sinal }) {
  return (
    <li className={`sinal sinal--${sinal.tipo.toLowerCase()}`}>
      <p className="sinal__descricao">{sinal.descricao}</p>

      {/*
        A citacao NAO e decoracao: e a regra que nao se negocia. Ela e o que
        transforma "a IA ta ruim" de reclamacao subjetiva em defeito que o
        vendedor confere com os proprios olhos, sem abrir a conversa inteira.
      */}
      <blockquote className="sinal__citacao">{sinal.trechoCitado}</blockquote>
    </li>
  );
}

function Coluna({ titulo, sinais }: { titulo: string; sinais: Sinal[] }) {
  if (sinais.length === 0) return null;

  return (
    <section className="dossie__secao">
      <h3 className="dossie__titulo">
        {titulo} <span className="dossie__contagem">{sinais.length}</span>
      </h3>
      <ul className="dossie__lista">
        {sinais.map((s) => (
          <BlocoDeSinal key={`${s.mensagemId}-${s.descricao}`} sinal={s} />
        ))}
      </ul>
    </section>
  );
}

export function PainelDoDossie({ dossie }: { dossie: Dossie | null }) {
  if (dossie === null) {
    // 404 da API. Nao e erro: e "ainda nao analisamos". Dizer isso e diferente
    // de mostrar um dossie vazio, que pareceria leitura feita que nao achou
    // nada — e o vendedor leria "nenhum sinal" como certeza.
    return (
      <div className="dossie dossie--vazio">
        <p>Ainda nao analisamos esta conversa.</p>
        <p className="dossie--vazio__nota">
          A leitura aparece aqui assim que o cliente falar.
        </p>
      </div>
    );
  }

  const nada =
    dossie.sinaisDeCompra.length === 0 &&
    dossie.sinaisDeFuga.length === 0 &&
    dossie.lacunas.length === 0;

  return (
    <div className="dossie">
      {/*
        A temperatura sai com DIRECAO. "Morno" descreve tanto quem estava frio e
        esquentou quanto quem estava quente e esfriou, e os dois pedem coisas
        opostas do vendedor — um quer que ele avance, o outro quer que ele
        descubra o que mudou.
      */}
      {dossie.resumo && (
        <header className={`termometro termometro--${(dossie.direcao ?? "").toLowerCase()}`}>
          <span className="termometro__rotulo">temperatura</span>
          <strong className="termometro__valor">{dossie.resumo}</strong>
        </header>
      )}

      <Coluna titulo="Sinais de compra" sinais={dossie.sinaisDeCompra} />
      <Coluna titulo="Sinais de fuga" sinais={dossie.sinaisDeFuga} />

      {dossie.lacunas.length > 0 && (
        <section className="dossie__secao dossie__secao--lacunas">
          {/*
            As lacunas vem por ultimo na ordem de leitura, mas sao a parte mais
            util: e o que faz o vendedor PERGUNTAR em vez de supor.
          */}
          <h3 className="dossie__titulo">O que ainda nao sabemos</h3>
          <ul className="dossie__lista">
            {dossie.lacunas.map((l) => (
              <li key={l} className="lacuna">
                {l}
              </li>
            ))}
          </ul>
        </section>
      )}

      {nada && <p className="dossie__silencio">A leitura nao encontrou sinal nem lacuna.</p>}
    </div>
  );
}
