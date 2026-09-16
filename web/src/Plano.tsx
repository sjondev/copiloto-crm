import { useEffect, useState } from "react";
import { buscarPlano, escreverBloco } from "./api";
import type { PlanoDoLead } from "./tipos";

/**
 * O plano de abordagem (#12).
 *
 * A tela onde o vendedor e o protagonista. Nao ha botao de sugerir aqui, e isso
 * nao e uma etapa faltando: o criterio da issue e que o plano FUNCIONE sem
 * nunca clicar nele. Sugerir entra depois, e como insumo — em campo separado,
 * sem tocar no que ele escreveu.
 */

/** O rotulo e a PERGUNTA do bloco: ela e que decide o que o vendedor escreve. */
const PERGUNTA: Record<string, string> = {
  Objetivo: "O que eu quero que aconteça nesta conversa?",
  PrecisoDescobrir: "O que preciso descobrir antes de propor?",
  ObjecaoProvavel: "Que resistência eu espero encontrar?",
  ProximoPasso: "Como esta conversa termina?",
};

export function Plano({ leadId }: { leadId: string }) {
  const [plano, setPlano] = useState<PlanoDoLead | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState<string | null>(null);

  useEffect(() => {
    const controle = new AbortController();
    setPlano(null);
    setErro(null);

    buscarPlano(leadId, controle.signal)
      .then(setPlano)
      .catch((e: unknown) => {
        if (e instanceof DOMException && e.name === "AbortError") return;
        setErro(e instanceof Error ? e.message : "não foi possível abrir o plano");
      });

    return () => controle.abort();
  }, [leadId]);

  /**
   * Salva ao SAIR do campo, e nao a cada tecla.
   *
   * Salvar por tecla geraria uma versão por caractere, e a versão existe para
   * responder "o que ele tinha escrito quando falou com o cliente" — não para
   * contar digitação. O backend também ignora texto igual ao que já estava.
   */
  async function aoSair(bloco: string, texto: string) {
    if (plano === null) return;
    if (plano.blocos.find((b) => b.bloco === bloco)?.texto === texto.trim()) return;

    setSalvando(bloco);

    try {
      setPlano(await escreverBloco(leadId, bloco, texto));
      setErro(null);
    } catch (e) {
      // O texto digitado FICA na tela: trocar por uma mensagem de erro apagaria
      // o que ele acabou de pensar por causa de uma requisição que falhou.
      setErro(e instanceof Error ? e.message : "não foi possível salvar");
    } finally {
      setSalvando(null);
    }
  }

  if (erro !== null && plano === null) return <p className="aviso">{erro}</p>;
  if (plano === null) return <p className="aviso">Carregando…</p>;

  return (
    <div className="plano">
      {erro && <p className="plano__erro" role="status">{erro}</p>}

      {plano.blocos.map((bloco) => (
        <label key={bloco.bloco} className="plano__bloco">
          <span className="plano__pergunta">{PERGUNTA[bloco.bloco] ?? bloco.bloco}</span>

          <textarea
            defaultValue={bloco.texto}
            onBlur={(e) => aoSair(bloco.bloco, e.target.value)}
            rows={3}
            spellCheck
          />

          {salvando === bloco.bloco && <span className="plano__salvando">salvando…</span>}

          {/*
            A sugestão da IA vem em campo SEPARADO quando existir (#12).
            Hoje nunca vem, e o bloco abaixo é o lugar preparado para ela —
            aceitar, editar ou ignorar, nunca sobrescrever.
          */}
          {bloco.sugestao && (
            <span className="plano__sugestao">sugestão: {bloco.sugestao}</span>
          )}
        </label>
      ))}

      <p className="plano__versao">versão {plano.versao}</p>
    </div>
  );
}
