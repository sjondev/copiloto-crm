import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import type { HubConnection } from "@microsoft/signalr";
import type { Dossie } from "./tipos";

export const ROTA = "/tempo-real/dossie";

/** `null` quando a leitura degradou: a tela desliga o "analisando" sem trocar o dossie. */
export type AoAtualizar = (dossie: Dossie | null) => void;
export type AoAnalisar = () => void;

interface Ouvintes {
  aoAtualizar: AoAtualizar;
  aoAnalisar: AoAnalisar;
  aoMudarEstado: (conectado: boolean) => void;
}

/**
 * A conexao de tempo real (#50).
 *
 * `withAutomaticReconnect` com atrasos EXPLICITOS em vez do padrao. O padrao
 * desiste depois de ~60s, e desistir e o pior desfecho: o vendedor fica com uma
 * tela que parece viva e nao atualiza mais. Aqui a tentativa continua, so que
 * cada vez mais espacada — e enquanto ela nao volta, o polling assume.
 */
/** Quanto esperar para tentar DE NOVO depois que o SignalR desistiu por conta propria. */
const RETOMADA_MS = 30_000;

export function conectar(leadId: string, ouvintes: Ouvintes): () => void {
  const conexao: HubConnection = new HubConnectionBuilder()
    .withUrl(ROTA)
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000, 30000, 60000])
    .configureLogging(LogLevel.Warning)
    .build();

  let vivo = true;
  let retomada: number | undefined;

  conexao.on("dossieAtualizado", (dossie: Dossie | null) => ouvintes.aoAtualizar(dossie));
  conexao.on("analisando", () => ouvintes.aoAnalisar());

  /** Inscreve no lead. Sem isto a conexao fica saudavel e muda. */
  async function acompanhar() {
    // O grupo mora na CONEXAO, e toda reconexao cria uma nova. Esquecer de
    // reinscrever e o defeito mais dificil de notar do tempo real: nada
    // quebra, nenhum erro aparece, e a tela simplesmente para de atualizar.
    await conexao.invoke("Acompanhar", leadId);
  }

  async function iniciar() {
    try {
      await conexao.start();
      if (!vivo) return;

      await acompanhar();
      ouvintes.aoMudarEstado(true);
    } catch {
      // Falhar em conectar nao e erro de tela: e o caso em que o polling
      // assume. Backend sem SignalR de pe continua dando um CRM que funciona,
      // so que sem o "ao vivo".
      ouvintes.aoMudarEstado(false);
      agendarRetomada();
    }
  }

  /**
   * O `withAutomaticReconnect` DESISTE quando a lista de atrasos acaba — em
   * pouco mais de dois minutos, que e o tempo de um deploy. Desistir em
   * silencio deixaria o vendedor no polling pelo resto do dia sem nenhum
   * sinal de que algo mudou, ate ele recarregar a pagina por outro motivo.
   *
   * Entao a tentativa recomeca do zero, para sempre, no ritmo mais espacado.
   */
  function agendarRetomada() {
    if (!vivo || retomada !== undefined) return;

    retomada = window.setTimeout(() => {
      retomada = undefined;
      if (vivo) void iniciar();
    }, RETOMADA_MS);
  }

  conexao.onreconnected(() => {
    void acompanhar()
      .then(() => ouvintes.aoMudarEstado(true))
      .catch(() => ouvintes.aoMudarEstado(false));
  });

  conexao.onreconnecting(() => ouvintes.aoMudarEstado(false));

  conexao.onclose(() => {
    ouvintes.aoMudarEstado(false);
    agendarRetomada();
  });

  void iniciar();

  return () => {
    vivo = false;
    window.clearTimeout(retomada);
    ouvintes.aoMudarEstado(false);
    if (conexao.state !== HubConnectionState.Disconnected) void conexao.stop();
  };
}
