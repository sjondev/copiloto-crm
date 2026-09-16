import { useEffect, useState } from "react";
import { buscarConversa, buscarDossie } from "./api";
import type { Dossie, Fala } from "./tipos";

export interface Leitura {
  conversa: Fala[];
  dossie: Dossie | null;
  /** Primeira carga ainda em andamento. Recarga silenciosa nao acende isto. */
  carregando: boolean;
  erro: string | null;
}

const INTERVALO_MS = 4000;

/**
 * Busca conversa e dossie, e repete enquanto a aba estiver visivel.
 *
 * O polling PARA quando a aba sai de foco. Vendedor deixa o CRM aberto o dia
 * inteiro numa aba de fundo; sem isso seriam milhares de requisicoes por dia
 * por pessoa para redesenhar uma tela que ninguem esta olhando — e cada uma
 * delas e uma consulta ao banco.
 *
 * O tempo real e a #50. Este laco continua existindo depois dela: e a
 * degradacao quando o SignalR cai, e nao um rascunho a ser jogado fora.
 */
export function usarLeitura(leadId: string): Leitura {
  const [conversa, setConversa] = useState<Fala[]>([]);
  const [dossie, setDossie] = useState<Dossie | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!leadId) return;

    const controle = new AbortController();
    let cancelado = false;
    let timer: number | undefined;

    async function buscar() {
      try {
        const [falas, lido] = await Promise.all([
          buscarConversa(leadId, controle.signal),
          buscarDossie(leadId, controle.signal),
        ]);

        if (cancelado) return;

        setConversa(falas ?? []);
        setDossie(lido);
        setErro(null);
      } catch (e) {
        // AbortError e desmontagem do componente, nao falha: mostrar erro ali
        // acenderia aviso vermelho toda vez que alguem troca de lead.
        if (cancelado || (e instanceof DOMException && e.name === "AbortError")) return;

        setErro(e instanceof Error ? e.message : "nao foi possivel falar com a API");
      } finally {
        if (!cancelado) setCarregando(false);
      }
    }

    function agendar() {
      // Fora de foco nao agenda nada. `visibilitychange` religa ao voltar.
      if (document.visibilityState !== "visible") return;
      timer = window.setTimeout(async () => {
        // A aba pode ter saido de foco DURANTE a espera. Sem esta checagem o
        // laco faria exatamente uma requisicao a mais toda vez que alguem troca
        // de janela — e essa e a troca mais comum do dia.
        if (document.visibilityState === "visible") await buscar();
        agendar();
      }, INTERVALO_MS);
    }

    function aoVoltarParaAba() {
      if (document.visibilityState !== "visible") return;
      // Buscar na hora: quem volta para a aba quer o estado de AGORA, nao o de
      // quatro segundos a frente.
      void buscar().then(agendar);
    }

    void buscar().then(agendar);
    document.addEventListener("visibilitychange", aoVoltarParaAba);

    return () => {
      cancelado = true;
      controle.abort();
      window.clearTimeout(timer);
      document.removeEventListener("visibilitychange", aoVoltarParaAba);
    };
  }, [leadId]);

  return { conversa, dossie, carregando, erro };
}
