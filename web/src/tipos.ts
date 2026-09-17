/**
 * O que a API devolve. Espelha os DTOs de `EndpointsDeLeitura.cs` — e espelhar
 * e deliberado: gerar tipo a partir de OpenAPI traria gerador, schema e passo de
 * build para economizar trinta linhas que quase nunca mudam.
 */

export type TipoDeSinal = "Compra" | "Fuga";

export interface Sinal {
  tipo: TipoDeSinal;
  descricao: string;
  /** A frase literal da conversa. Sem ela o sinal nao e exibido. */
  trechoCitado: string;
  mensagemId: string;
}

export type TipoDeObjecao =
  | "Preco" | "Timing" | "Autoridade" | "Concorrente"
  | "Necessidade" | "Confianca" | "NaoClassificada";

export interface Objecao {
  tipo: TipoDeObjecao;
  descricao: string;
  trechoCitado: string;
  mensagemId: string;
  /** Detectada pela forma da conversa, nao pelo que foi dito. */
  porComportamento: boolean;
}

export interface Dossie {
  id: string;
  leadId: string;
  geradoEm: string;
  temperatura: string | null;
  direcao: string | null;
  /** "morna e esfriando" — ja pronto pelo backend. */
  resumo: string | null;
  sinaisDeCompra: Sinal[];
  sinaisDeFuga: Sinal[];
  objecoes: Objecao[];
  lacunas: string[];
}

export interface Fala {
  id: string;
  autor: "Cliente" | "Vendedor";
  texto: string;
  enviadaEm: string;
  /** "Audio", "Imagem", "Documento", "Outro" — ou null quando e texto puro. */
  midia: string | null;
}

/** Uma linha da fila de atendimento (#174). */
export interface LinhaDaFila {
  leadId: string;
  nome: string | null;
  telefone: string;
  estagio: string | null;
  /** "morna e esfriando" — ja pronto pelo backend. */
  temperatura: string | null;
  objecao: string | null;
  /** "ClienteEmSilencio", "PropostaEnvelhecendo", "NegocioParado" — ou null. */
  alerta: string | null;
  /** O texto do alerta, com a fala que o originou. */
  motivo: string | null;
  ultimaFala: string | null;
  diasEmSilencio: number | null;
  analiseSuspensa: boolean;
}

/** Um bloco do plano de abordagem (#12). */
export interface BlocoDoPlano {
  /** "Objetivo", "PrecisoDescobrir", "ObjecaoProvavel", "ProximoPasso". */
  bloco: string;
  texto: string;
  /** O que a IA propos, em campo separado. Hoje sempre null. */
  sugestao: string | null;
  /** Se da para perguntar "por que essa sugestao?" (#51). */
  temProcedencia: boolean;
}

export interface PlanoDoLead {
  planoId: string;
  dealId: string;
  /** Quantas vezes o VENDEDOR mexeu. Sugestao nao conta. */
  versao: number;
  blocos: BlocoDoPlano[];
}

/** O que sustentou a sugestao (#51). */
export interface PorQue {
  modelo: string;
  versaoDoPrompt: string | null;
  custoEmReais: number;
  latenciaMs: number;
  tokensEntrada: number;
  tokensSaida: number;
  tentativas: number;
  sucesso: boolean;
  quando: string;
  contextoEnviado: string | null;
}

/** Quanto se gastou com um agente, ou com um modelo (#3). */
export interface GastoPor {
  nome: string;
  custoEmReais: number;
  invocacoes: number;
}

/** Quantas sugestoes daquele modelo o vendedor usou (#3). */
export interface AceitePor {
  nome: string;
  aceitas: number;
  ignoradas: number;
  /** Nulo quando ninguem decidiu: indecisao nao e recusa. */
  taxa: number | null;
}

export interface Metricas {
  de: string;
  ate: string;
  custoIaEmReais: number;
  invocacoes: number;
  invocacoesQueFalharam: number;
  porAgente: GastoPor[];
  porModelo: GastoPor[];
  negociosGanhos: number;
  receitaGanha: number;
  ganhosComIa: number;
  receitaInfluenciada: number;
  receitaPorRealGasto: number | null;
  custoPorNegocioGanho: number | null;
  aceitePorModelo: AceitePor[];
  /** A ressalva de vies, que viaja junto do numero (#6). */
  ressalva: string;
}
