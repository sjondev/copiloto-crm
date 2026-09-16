/**
 * A credencial do vendedor, do login ate cada chamada (#182).
 *
 * Fica em `sessionStorage`, e nao em `localStorage`: token de oito horas que
 * sobrevive a fechar o navegador e token esquecido num computador
 * compartilhado. Aqui ele morre junto com a aba, que e o comportamento que o
 * vendedor espera de "fechei o sistema".
 *
 * Nao ha refresh token. Expirou, faz login de novo — oito horas cobrem o turno,
 * e renovacao silenciosa e uma superficie a mais para guardar mal.
 */

const CHAVE = "copiloto.token";

export type Sessao = { token: string; perfil: string; expiraEm: string };

export function tokenGuardado(): string | null {
  try {
    return sessionStorage.getItem(CHAVE);
  } catch {
    // Navegador com armazenamento bloqueado. Sem token guardado a tela pede
    // login de novo, que e degradacao aceitavel — melhor que a tela branca que
    // uma excecao aqui produziria.
    return null;
  }
}

export function guardar(token: string): void {
  try {
    sessionStorage.setItem(CHAVE, token);
  } catch {
    // Ignorado pelo mesmo motivo: a sessao vale enquanto a pagina viver.
  }
}

export function esquecer(): void {
  try {
    sessionStorage.removeItem(CHAVE);
  } catch {
    // Idem.
  }
}

export class CredencialRecusada extends Error {
  constructor() {
    super("email ou senha invalidos");
  }
}

export async function entrar(email: string, senha: string): Promise<string> {
  const resposta = await fetch("/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, senha }),
  });

  if (resposta.status === 401) throw new CredencialRecusada();
  if (!resposta.ok) throw new Error(`a API respondeu ${resposta.status}`);

  const { token } = (await resposta.json()) as Sessao;
  guardar(token);

  return token;
}
