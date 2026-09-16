import type { Dossie, Fala } from "./tipos";
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
