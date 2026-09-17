import { useEffect, useState } from "react";
import { buscarMetricas } from "./api";
import type { Metricas as Dados } from "./tipos";

/**
 * O painel que responde se a IA se paga (#3).
 *
 * A ordem da tela é a ordem do argumento: primeiro o que rendeu por real gasto,
 * que é a resposta; depois os dois lados da conta; e a ressalva ANTES da quebra
 * por modelo, para ninguém ler o número sem ela.
 */

const reais = (v: number) =>
  v.toLocaleString("pt-BR", { style: "currency", currency: "BRL", minimumFractionDigits: 2 });

export function Metricas() {
  const [dados, setDados] = useState<Dados | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    const controle = new AbortController();

    buscarMetricas(controle.signal)
      .then(setDados)
      .catch((e: unknown) => {
        if (e instanceof DOMException && e.name === "AbortError") return;
        setErro(e instanceof Error ? e.message : "não foi possível carregar o painel");
      });

    return () => controle.abort();
  }, []);

  if (erro) return <p className="aviso">{erro}</p>;
  if (dados === null) return <p className="aviso">Carregando…</p>;

  return (
    <div className="metricas">
      <p className="metricas__periodo">
        de {new Date(dados.de).toLocaleDateString("pt-BR")} a{" "}
        {new Date(dados.ate).toLocaleDateString("pt-BR")}
      </p>

      <div className="metricas__destaque">
        <strong>
          {dados.receitaPorRealGasto === null
            ? "—"
            : `${reais(dados.receitaPorRealGasto)} por real gasto`}
        </strong>
        <span>
          {reais(dados.receitaInfluenciada)} influenciados ÷ {reais(dados.custoIaEmReais)} de IA
        </span>
      </div>

      {/*
        A ressalva vem ANTES da quebra por modelo, e não num rodapé (#6). Número
        de ROI solto vira slide, e slide com viés de seleção não declarado é o
        jeito mais rápido de perder a confiança de quem entende do assunto.
      */}
      <p className="metricas__ressalva">{dados.ressalva}</p>

      <dl className="metricas__grade">
        <div>
          <dt>negócios ganhos</dt>
          <dd>{dados.negociosGanhos}</dd>
        </div>
        <div>
          <dt>ganhos com IA no caminho</dt>
          <dd>{dados.ganhosComIa}</dd>
        </div>
        <div>
          <dt>receita ganha</dt>
          <dd>{reais(dados.receitaGanha)}</dd>
        </div>
        <div>
          <dt>custo por negócio ganho</dt>
          <dd>{dados.custoPorNegocioGanho === null ? "—" : reais(dados.custoPorNegocioGanho)}</dd>
        </div>
        <div>
          <dt>invocações</dt>
          <dd>{dados.invocacoes}</dd>
        </div>
        <div>
          {/*
            As que falharam aparecem ao lado do total, e não escondidas: elas
            custaram token e o provedor cobrou por elas (#1).
          */}
          <dt>que falharam</dt>
          <dd>{dados.invocacoesQueFalharam}</dd>
        </div>
      </dl>

      <h3 className="metricas__titulo">Onde o dinheiro foi</h3>
      <ul className="metricas__lista">
        {dados.porAgente.map((a) => (
          <li key={a.nome}>
            <span>{a.nome}</span>
            <span>{reais(a.custoEmReais)} · {a.invocacoes} chamadas</span>
          </li>
        ))}
      </ul>

      <ul className="metricas__lista">
        {dados.porModelo.map((m) => (
          <li key={m.nome}>
            <span>{m.nome}</span>
            <span>{reais(m.custoEmReais)} · {m.invocacoes} chamadas</span>
          </li>
        ))}
      </ul>

      <h3 className="metricas__titulo">Quanto o vendedor aproveita</h3>
      {dados.aceitePorModelo.length === 0 ? (
        <p className="metricas__vazio">
          Ninguém decidiu sobre nenhuma sugestão ainda. Indecisão não é recusa, então
          não há taxa a mostrar.
        </p>
      ) : (
        <ul className="metricas__lista">
          {dados.aceitePorModelo.map((a) => (
            <li key={a.nome}>
              <span>{a.nome}</span>
              <span>
                {a.taxa === null ? "—" : `${Math.round(a.taxa * 100)}%`}
                {" · "}{a.aceitas} usadas, {a.ignoradas} ignoradas
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
