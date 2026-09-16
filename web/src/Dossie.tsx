import type { Dossie, Objecao, Sinal } from "./tipos";

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

const ROTULO: Record<string, string> = {
  Preco: "preço",
  Timing: "momento",
  Autoridade: "quem decide",
  Concorrente: "concorrente",
  Necessidade: "necessidade",
  Confianca: "confiança",
  NaoClassificada: "não classificada",
};

function BlocoDeObjecao({ objecao }: { objecao: Objecao }) {
  return (
    <li className="objecao">
      <div className="objecao__topo">
        <span className="objecao__tipo">{ROTULO[objecao.tipo] ?? objecao.tipo}</span>

        {/*
          A etiqueta existe porque esta e a leitura que o vendedor NAO faria
          sozinho: ninguem percebe que as respostas do cliente encolheram pela
          metade ao longo de tres dias. Marcar de onde veio e o que faz ele
          aprender a confiar nessa parte.
        */}
        {objecao.porComportamento && (
          <span className="objecao__origem" title="detectada pela forma da conversa, não pelo texto">
            padrão
          </span>
        )}
      </div>

      <p className="objecao__descricao">{objecao.descricao}</p>
      <blockquote className="sinal__citacao">{objecao.trechoCitado}</blockquote>
    </li>
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
    dossie.objecoes.length === 0 &&
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

      {dossie.objecoes.length > 0 && (
        <section className="dossie__secao">
          {/*
            Objecao vem DEPOIS dos sinais e antes das lacunas: o vendedor le o
            que esta acontecendo, depois o que trava, depois o que perguntar.
          */}
          <h3 className="dossie__titulo">
            Resistência <span className="dossie__contagem">{dossie.objecoes.length}</span>
          </h3>
          <ul className="dossie__lista">
            {dossie.objecoes.map((o) => (
              <BlocoDeObjecao key={`${o.tipo}-${o.trechoCitado}`} objecao={o} />
            ))}
          </ul>
        </section>
      )}

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
