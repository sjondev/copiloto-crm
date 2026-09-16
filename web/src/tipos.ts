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
