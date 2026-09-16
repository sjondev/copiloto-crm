import { useEffect, useRef, useState } from "react";
import { buscarConversa, buscarDossie } from "./api";
import { conectar } from "./tempoReal";
import type { Dossie, Fala } from "./tipos";

export type Canal = "ao-vivo" | "verificando";

export interface Leitura {
  conversa: Fala[];
  dossie: Dossie | null;
  carregando: boolean;
  /** A releitura comecou e o dossie novo ainda nao chegou. */
  analisando: boolean;
  canal: Canal;
  erro: string | null;
}

/** Com tempo real de pe, o polling vira rede de seguranca e espaca. */
const INTERVALO_AO_VIVO_MS = 30_000;
const INTERVALO_SEM_TEMPO_REAL_MS = 4_000;

/**
 * Busca conversa e dossie, por tempo real quando da e por polling quando nao da.
 *
 * O polling NAO e desligado quando o SignalR conecta, so espacado. A conexao
 * pode estar "aberta" e o evento nao chegar — proxy que corta, mensagem perdida
 * numa reconexao, backplane ausente com duas instancias (#70). Essas falhas nao
 * se anunciam, e uma verificacao a cada trinta segundos as corrige sozinha.
 *
 * E ele PARA quando a aba sai de foco: vendedor deixa o CRM aberto o dia inteiro
 * numa aba de fundo, e cada ciclo e uma consulta ao banco para redesenhar uma
 * tela que ninguem esta olhando.
 */
export function usarLeitura(leadId: string): Leitura {
  const [conversa, setConversa] = useState<Fala[]>([]);
  const [dossie, setDossie] = useState<Dossie | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [analisando, setAnalisando] = useState(false);
  const [aoVivo, setAoVivo] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  // O laco le o estado atual sem se reinscrever a cada mudanca: colocar `aoVivo`
  // nas dependencias do efeito derrubaria e recriaria a conexao toda vez que ela
  // mudasse de estado, que e exatamente o momento mais delicado.
  const aoVivoRef = useRef(false);
  aoVivoRef.current = aoVivo;

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
        setAnalisando(false);
        setErro(null);
      } catch (e) {
        if (cancelado || (e instanceof DOMException && e.name === "AbortError")) return;
        setErro(e instanceof Error ? e.message : "nao foi possivel falar com a API");
      } finally {
        if (!cancelado) setCarregando(false);
      }
    }

    function agendar() {
      if (cancelado || document.visibilityState !== "visible") return;

      timer = window.setTimeout(async () => {
        if (document.visibilityState === "visible") await buscar();
        agendar();
      }, aoVivoRef.current ? INTERVALO_AO_VIVO_MS : INTERVALO_SEM_TEMPO_REAL_MS);
    }

    function aoVoltarParaAba() {
      if (document.visibilityState !== "visible") return;
      window.clearTimeout(timer);
      void buscar().then(agendar);
    }

    const desconectar = conectar(leadId, {
      aoAtualizar: (novo) => {
        if (cancelado) return;

        setAnalisando(false);
        // `null` significa que a leitura degradou: mantem o dossie que estava na
        // tela em vez de apaga-lo. Dado levemente velho e melhor que tela vazia
        // no meio de uma venda (#30).
        if (novo !== null) setDossie(novo);

        // A conversa nao vem no evento: o dossie mudou porque uma fala nova
        // chegou, e a tela precisa das duas coisas.
        void buscarConversa(leadId, controle.signal)
          .then((falas) => { if (!cancelado) setConversa(falas ?? []); })
          .catch(() => {});
      },
      aoAnalisar: () => { if (!cancelado) setAnalisando(true); },
      aoMudarEstado: (conectado) => {
        if (cancelado) return;

        setAoVivo(conectado);
        // Perdeu o tempo real: reagenda AGORA no ritmo apertado, sem esperar o
        // ciclo lento terminar. Sem isso a tela ficaria ate trinta segundos
        // parada justamente quando deixou de receber eventos.
        if (!conectado) {
          window.clearTimeout(timer);
          agendar();
        }
      },
    });

    void buscar().then(agendar);
    document.addEventListener("visibilitychange", aoVoltarParaAba);

    return () => {
      cancelado = true;
      controle.abort();
      window.clearTimeout(timer);
      document.removeEventListener("visibilitychange", aoVoltarParaAba);
      desconectar();
    };
  }, [leadId]);

  return {
    conversa,
    dossie,
    carregando,
    analisando,
    canal: aoVivo ? "ao-vivo" : "verificando",
    erro,
  };
}
