using Copiloto.Dominio.Ia;

namespace Copiloto.Api.Ia;

/// <summary>
/// Quanto custou a chamada, para o ledger (#1).
///
/// O preco mora na TABELA e nao em codigo porque ele muda por decisao de
/// fornecedor, e um numero fixo aqui viraria conta errada em silencio — o tipo
/// de erro que so aparece quando alguem compara a fatura com o painel.
///
/// Modelo que nao esta na tabela custa ZERO e nao levanta excecao: derrubar a
/// sugestao do vendedor porque o preco nao foi cadastrado trocaria um numero
/// errado no relatorio por trabalho perdido na frente do cliente.
/// </summary>
public class PrecoDoModelo
{
    private readonly IReadOnlyDictionary<string, decimal> _porMilTokens;

    public PrecoDoModelo(IEnumerable<ModeloDisponivel> tabela)
    {
        ArgumentNullException.ThrowIfNull(tabela);

        _porMilTokens = tabela
            .GroupBy(m => m.Nome, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CustoPorMilTokens, StringComparer.OrdinalIgnoreCase);
    }

    public decimal De(string modelo, int tokens) =>
        _porMilTokens.TryGetValue(modelo, out var porMil) ? porMil * tokens / 1000m : 0m;
}
