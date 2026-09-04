using Copiloto.Dominio.Vendas;

namespace Copiloto.Dominio.Rag;

/// <summary>
/// Quem pode entrar na base de precedentes de venda, e o que pode sair dela
/// para cada atendimento (#85, #62).
///
/// A regra existe ANTES do RAG de proposito. Se ela nascesse junto com a
/// indexacao, nasceria como filtro numa consulta — e filtro em consulta e
/// esquecido na segunda consulta que alguem escrever. Aqui ela e do dominio, e
/// a indexacao pergunta a ela.
///
/// O risco nao e so de dado pessoal: se conversa com fornecedor entra no mesmo
/// indice que conversa com cliente, o sistema pode recuperar uma negociacao de
/// fornecimento — com margem e custo — enquanto o vendedor atende um comprador.
/// Vazamento comercial, saindo pela porta que ninguem estava vigiando.
/// </summary>
public static class BaseDePrecedentes
{
    /// <summary>
    /// Conversa de parceiro NAO entra na base de precedentes de venda.
    ///
    /// O "salvo decisao explicita" da issue nao virou parametro aqui: uma flag
    /// `incluirParceiros` seria ligada uma vez em algum lugar e nunca mais
    /// revisada. Se um dia houver base de precedentes de COMPRA, ela e outra
    /// base, com outro escopo — e nao esta com um booleano invertido.
    /// </summary>
    public static bool PodeIndexar(Lead lead)
    {
        ArgumentNullException.ThrowIfNull(lead);

        return lead.Relacao == Relacao.Cliente;
    }

    /// <summary>
    /// Os precedentes que podem ser mostrados no atendimento a este lead.
    ///
    /// Filtra pelos DOIS lados: nada de parceiro sai, e nada sai para um
    /// atendimento de parceiro. Conferir so a origem deixaria metade do
    /// problema em pe — o dossie de um fornecedor recheado com conversas de
    /// clientes e o mesmo vazamento na direcao contraria.
    /// </summary>
    public static IReadOnlyList<T> Filtrar<T>(
        IEnumerable<T> candidatos, Func<T, Lead> deQuem, Lead paraQuem)
    {
        ArgumentNullException.ThrowIfNull(paraQuem);

        if (paraQuem.Relacao != Relacao.Cliente) return [];

        return candidatos.Where(c => PodeIndexar(deQuem(c))).ToList();
    }
}
