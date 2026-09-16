import type { Dossie, Fala, Objecao, Sinal } from "./tipos";

/**
 * Dados de exemplo para os stories (#170).
 *
 * Tipados com os MESMOS tipos da API, de proposito. Story que inventa um
 * formato proprio para se desenhar melhor deixa de provar que a tela aguenta o
 * dado real — e o dia em que a API mudar, ele continua verde mostrando uma tela
 * que ja nao existe.
 *
 * As conversas sao as do seed: "fecha" e "esfria e some". Inventar conversa
 * bonita aqui esconderia justamente o caso dificil que o produto existe para
 * ler.
 */

const ONTEM = "2026-09-15T12:00:00+00:00";

const id = (n: number) => `00000000-0000-0000-0000-${String(n).padStart(12, "0")}`;

export const falasDoCafe: Fala[] = [
  { id: id(1), autor: "Cliente", texto: "qual o valor do kg?", enviadaEm: ONTEM, midia: null },
  {
    id: id(2),
    autor: "Vendedor",
    texto: "o bourbon sai a 78, e o catuai a 65",
    enviadaEm: "2026-09-15T12:02:00+00:00",
    midia: null,
  },
  {
    id: id(3),
    autor: "Cliente",
    texto: "vou pensar melhor e te falo",
    enviadaEm: "2026-09-15T12:09:00+00:00",
    midia: null,
  },
];

export const falasComAudio: Fala[] = [
  ...falasDoCafe,
  {
    id: id(4),
    autor: "Cliente",
    texto: "[audio nao transcrito, 22s]",
    enviadaEm: "2026-09-15T12:20:00+00:00",
    midia: "Audio",
  },
];

export const falasDeVariosDias: Fala[] = [
  { id: id(10), autor: "Cliente", texto: "boa tarde, voces atendem empresa?", enviadaEm: "2026-09-10T09:00:00+00:00", midia: null },
  { id: id(11), autor: "Cliente", texto: "queria pra um escritorio, uns 30 litros por semana", enviadaEm: "2026-09-10T09:00:12+00:00", midia: null },
  { id: id(12), autor: "Vendedor", texto: "Atendemos sim! Consigo montar um plano mensal", enviadaEm: "2026-09-10T09:05:00+00:00", midia: null },
  { id: id(13), autor: "Cliente", texto: "manda a proposta pfv", enviadaEm: "2026-09-11T09:07:00+00:00", midia: null },
  { id: id(14), autor: "Vendedor", texto: "Enviado! Qualquer duvida me chama", enviadaEm: "2026-09-12T10:00:00+00:00", midia: null },
  { id: id(15), autor: "Cliente", texto: "vou pensar e te falo", enviadaEm: "2026-09-13T13:00:00+00:00", midia: null },
];

export const sinalDeCompra: Sinal = {
  tipo: "Compra",
  descricao: "preco perguntado duas vezes em tres dias",
  trechoCitado: "qual o valor do kg?",
  mensagemId: id(1),
};

export const sinalDeFuga: Sinal = {
  tipo: "Fuga",
  descricao: "objecao velada: adiou sem dizer nao",
  trechoCitado: "vou pensar melhor e te falo",
  mensagemId: id(3),
};

export const objecaoPorTexto: Objecao = {
  tipo: "Timing",
  descricao: "adiou sem dizer nao: 'vou pensar' e a objecao que mais parece interesse",
  trechoCitado: "vou pensar melhor e te falo",
  mensagemId: id(3),
  porComportamento: false,
};

export const objecaoPorComportamento: Objecao = {
  tipo: "NaoClassificada",
  descricao: "as respostas encurtaram: de 42 para 20 caracteres em media",
  trechoCitado: "vou pensar e te falo",
  mensagemId: id(15),
  porComportamento: true,
};

export const lacunas = [
  "Para quantas pessoas ele compra por mes? Sem isso nao da para dizer se o kg cabe no uso dele.",
  "Ele ja torra em casa ou compra moido? Muda o produto inteiro da conversa.",
  "Quem decide a compra e ele mesmo?",
];

/** Um dossie vazio, para as stories acrescentarem so o que interessa a cada uma. */
export function dossie(partes: Partial<Dossie> = {}): Dossie {
  return {
    id: id(100),
    leadId: id(101),
    geradoEm: ONTEM,
    temperatura: null,
    direcao: null,
    resumo: null,
    sinaisDeCompra: [],
    sinaisDeFuga: [],
    objecoes: [],
    lacunas: [],
    ...partes,
  };
}
