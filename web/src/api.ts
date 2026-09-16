import type { Dossie, Fala } from "./tipos";

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

async function buscar<T>(url: string, sinal: AbortSignal): Promise<T | null> {
  const resposta = await fetch(url, { signal: sinal });

  if (resposta.status === 404) return null;
  if (!resposta.ok) throw new FalhaDeRede(resposta.status);

  return (await resposta.json()) as T;
}

export const buscarDossie = (leadId: string, sinal: AbortSignal) =>
  buscar<Dossie>(`/leads/${leadId}/dossie`, sinal);

export const buscarConversa = (leadId: string, sinal: AbortSignal) =>
  buscar<Fala[]>(`/leads/${leadId}/conversa`, sinal);
