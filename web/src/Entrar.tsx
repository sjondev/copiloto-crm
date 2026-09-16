import { useState } from "react";
import { CredencialRecusada, entrar } from "./sessao";

/**
 * A porta (#182).
 *
 * Existe porque os endpoints passaram a exigir credencial — e nao por capricho
 * de produto: o que sai deles e a conversa do cliente e a leitura que a IA fez
 * dele. Ate a #182 isso respondia 200 para quem alcancasse a porta.
 *
 * A mensagem de erro e a MESMA para email inexistente e senha errada, igual a
 * rota de login ja faz: dizer "usuario nao encontrado" entrega a quem tenta
 * metade do trabalho, que e descobrir quais emails existem.
 */
export function Entrar({ aoEntrar }: { aoEntrar: (token: string) => void }) {
  const [email, setEmail] = useState("");
  const [senha, setSenha] = useState("");
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function submeter(evento: React.FormEvent) {
    evento.preventDefault();
    setEnviando(true);
    setErro(null);

    try {
      aoEntrar(await entrar(email, senha));
    } catch (e) {
      setErro(
        e instanceof CredencialRecusada
          ? "email ou senha invalidos"
          : "nao foi possivel falar com a API",
      );
    } finally {
      setEnviando(false);
    }
  }

  return (
    <form className="entrar" onSubmit={submeter}>
      <h1 className="entrar__marca">Copiloto</h1>

      <label className="entrar__campo">
        <span>e-mail</span>
        <input
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          autoComplete="username"
          required
        />
      </label>

      <label className="entrar__campo">
        <span>senha</span>
        <input
          type="password"
          value={senha}
          onChange={(e) => setSenha(e.target.value)}
          autoComplete="current-password"
          required
        />
      </label>

      <button type="submit" disabled={enviando}>
        {enviando ? "entrando…" : "entrar"}
      </button>

      {erro && <p className="entrar__erro" role="alert">{erro}</p>}
    </form>
  );
}
