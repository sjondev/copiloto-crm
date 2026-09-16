import type { Dossie, Fala, LinhaDaFila, PlanoDoLead } from "./tipos";
import { tokenGuardado } from "./sessao";

/**
 * As duas rotas de leitura (#161).
 *
 * 404 vira `null` e nao excecao, porque 404 aqui nao e falha: e "ainda nao
 * analisamos este lead". Tratar como erro mandaria a tela mostrar problema onde
 * so ha ausencia, e o vendedor aprenderia a ignorar o aviso.
 */

export class FalhaDeRede extends Error {
  constructor(public readonly status: number) {
    super(`a API respondeu ${status}`);
  }
}

/**
 * A credencial acabou ou nunca existiu (#182).
 *
 * Separada de `FalhaDeRede` porque a tela responde diferente: rede que falha
 * mantem o que ja estava na tela e avisa; credencial que expira precisa pedir
 * login, e insistir com token morto so gera 401 em laco.
 */
export class SessaoExpirada extends Error {
  constructor() {
    super("a sessao expirou");
  }
}

async function buscar<T>(url: string, sinal: AbortSignal): Promise<T | null> {
  const token = tokenGuardado();

  const resposta = await fetch(url, {
    signal: sinal,
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });

  if (resposta.status === 401 || resposta.status === 403) throw new SessaoExpirada();
  if (resposta.status === 404) return null;
  if (!resposta.ok) throw new FalhaDeRede(resposta.status);

  return (await resposta.json()) as T;
}

export const buscarDossie = (leadId: string, sinal: AbortSignal) =>
  buscar<Dossie>(`/leads/${leadId}/dossie`, sinal);

export const buscarConversa = (leadId: string, sinal: AbortSignal) =>
  buscar<Fala[]>(`/leads/${leadId}/conversa`, sinal);

/**
 * A fila de atendimento (#174).
 *
 * Lista vazia e resposta legitima — banco novo, ou vendedor sem carteira —,
 * entao o `?? []` evita a tela tratar "ninguem para atender" como falha.
 */
export const buscarFila = async (sinal: AbortSignal) =>
  (await buscar<LinhaDaFila[]>("/leads", sinal)) ?? [];

/**
 * O plano de abordagem daquele lead (#12).
 *
 * `null` quando o lead ainda nao tem negocio aberto — a tela mostra o aviso, e
 * nao um editor que nao tem onde salvar.
 */
export const buscarPlano = (leadId: string, sinal: AbortSignal) =>
  buscar<PlanoDoLead>(`/leads/${leadId}/plano`, sinal);

/** Salva um bloco e devolve o plano inteiro, ja com a versao nova. */
export async function escreverBloco(leadId: string, bloco: string, texto: string) {
  const token = tokenGuardado();

  const resposta = await fetch(`/leads/${leadId}/plano/${bloco}`, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify({ texto }),
  });

  if (resposta.status === 401 || resposta.status === 403) throw new SessaoExpirada();
  if (!resposta.ok) throw new FalhaDeRede(resposta.status);

  return (await resposta.json()) as PlanoDoLead;
}

/** As tres acoes de sugestao de um bloco (#189). Todas devolvem o plano inteiro. */
async function acaoNoBloco(
  leadId: string,
  bloco: string,
  caminho: string,
  metodo: "POST" | "DELETE",
) {
  const token = tokenGuardado();

  const resposta = await fetch(`/leads/${leadId}/plano/${bloco}/${caminho}`, {
    method: metodo,
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });

  if (resposta.status === 401 || resposta.status === 403) throw new SessaoExpirada();
  if (!resposta.ok) throw new FalhaDeRede(resposta.status);

  return (await resposta.json()) as PlanoDoLead;
}

export const sugerirBloco = (leadId: string, bloco: string) =>
  acaoNoBloco(leadId, bloco, "sugerir", "POST");

export const aceitarSugestao = (leadId: string, bloco: string) =>
  acaoNoBloco(leadId, bloco, "aceitar", "POST");

export const descartarSugestao = (leadId: string, bloco: string) =>
  acaoNoBloco(leadId, bloco, "sugestao", "DELETE");
