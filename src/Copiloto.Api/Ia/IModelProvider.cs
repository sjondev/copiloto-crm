using Copiloto.Dominio.Ia;

namespace Copiloto.Api.Ia;

/// <summary>O que se pede ao modelo.</summary>
/// <param name="Tarefa">Decide qual resposta gravada o fake devolve, e o gasto que se aceita.</param>
/// <param name="Modelo">O nome que o router escolheu (#29).</param>
public record PedidoAoModelo(Tarefa Tarefa, string Modelo, string Prompt);

/// <summary>
/// O que voltou.
///
/// Os tokens vem SEPARADOS porque entrada e saida tem precos diferentes em todo
/// provedor real, e um total unico impede a conta do ledger (#1) — que e a
/// pergunta "vale trocar de modelo?".
/// </summary>
public record RespostaDoModelo(string Conteudo, int TokensEntrada, int TokensSaida)
{
    public int TokensTotais => TokensEntrada + TokensSaida;
}

/// <summary>
/// A porta de saida para qualquer modelo (#27).
///
/// A implementacao padrao e o <see cref="FakeProvider"/>, e isso e decisao:
/// construir a orquestracao contra um provedor falso e o que permite testar os
/// cenarios ruins com precisao. Com API real, reproduzir um 429 na hora certa e
/// quase impossivel — e o codigo que trata 429 acaba sendo o unico que nunca
/// roda antes de producao.
/// </summary>
public interface IModelProvider
{
    /// <summary>O valor de `MODEL_PROVIDER` que seleciona este provedor.</summary>
    string Nome { get; }

    Task<RespostaDoModelo> Responder(PedidoAoModelo pedido, CancellationToken ct);
}

/// <summary>O provedor nao respondeu: caiu, recusou conexao, devolveu 5xx.</summary>
public class ProvedorForaDoAr(string provedor, Exception? causa = null)
    : Exception($"Provedor '{provedor}' fora do ar.", causa)
{
    public string Provedor { get; } = provedor;
}

/// <summary>
/// HTTP 429. E excecao propria e nao <c>HttpRequestException</c> porque a
/// cascata (#30) e o circuito (#38) tratam limite de taxa de forma diferente de
/// provedor caido: um espera e tenta de novo, o outro troca de provedor.
/// </summary>
public class LimiteDeTaxaExcedido(string provedor, TimeSpan? tentarDepoisDe = null)
    : Exception($"Provedor '{provedor}' recusou por limite de taxa (429).")
{
    public string Provedor { get; } = provedor;

    /// <summary>O `Retry-After` do provedor, quando ele manda um.</summary>
    public TimeSpan? TentarDepoisDe { get; } = tentarDepoisDe;
}
