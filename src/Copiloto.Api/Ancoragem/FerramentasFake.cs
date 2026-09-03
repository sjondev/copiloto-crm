using System.Globalization;
using Copiloto.Dominio.Ancoragem;

namespace Copiloto.Api.Ancoragem;

/// <summary>Um item do catalogo: o que existe, e por quanto sai o quilo.</summary>
public record ProdutoDoCatalogo(string Nome, int EstoqueEmKg, decimal PrecoPorKg);

/// <summary>
/// As ferramentas de ancoragem sobre um catalogo em memoria (#57).
///
/// E o padrao, pelo mesmo motivo do <c>FakeSource</c>: a suite e a demo rodam
/// offline e de graca. Trocar por ERP de verdade e implementar esta interface
/// noutra classe — o dominio nao fica sabendo, porque so conhece o contrato.
///
/// O que NAO e fake aqui e o comportamento de nao achar: sao os buracos do
/// catalogo que fazem a sugestao virar pergunta, e um fake que responde tudo
/// esconderia justamente o caminho que a issue existe para provar.
/// </summary>
public class FerramentasFake : IFerramentasDeAncoragem
{
    /// <summary>
    /// Abaixo disto, prova social identifica gente. "Dois clientes do seu porte
    /// na sua regiao compraram" e, para quem conhece o mercado local, um nome —
    /// entao o piso e da ferramenta, e nao um cuidado que quem chama precise
    /// lembrar de ter (#62, #89).
    /// </summary>
    public const int MinimoParaProvaSocialNaoIdentificar = 5;

    private static readonly CultureInfo Brasil = new("pt-BR");

    private readonly IReadOnlyDictionary<string, ProdutoDoCatalogo> _catalogo;
    private readonly IReadOnlyDictionary<string, string> _politicaPorPerfil;
    private readonly IReadOnlyDictionary<string, int> _compradoresPorPerfil;
    private readonly IReadOnlyDictionary<string, string> _prazoPorRegiao;

    public FerramentasFake(
        IEnumerable<ProdutoDoCatalogo> catalogo,
        IReadOnlyDictionary<string, string> politicaPorPerfil,
        IReadOnlyDictionary<string, int> compradoresPorPerfil,
        IReadOnlyDictionary<string, string> prazoPorRegiao)
    {
        _catalogo = catalogo.ToDictionary(p => p.Nome, StringComparer.OrdinalIgnoreCase);
        _politicaPorPerfil = politicaPorPerfil;
        _compradoresPorPerfil = compradoresPorPerfil;
        _prazoPorRegiao = prazoPorRegiao;
    }

    /// <summary>
    /// O cenario de cafe do `seed/conversas` — mesmos produtos e mesmos precos
    /// das conversas gravadas. Quando a demo mostra o dossie da Marina e a
    /// ancora diz "R$ 68,00/kg", o numero e o mesmo que o vendedor falou ali.
    /// </summary>
    public static FerramentasFake DoCenarioDeCafe() => new(
        catalogo: new[]
        {
            new ProdutoDoCatalogo("Bourbon Amarelo", 140, 68m),
            new ProdutoDoCatalogo("Catuai Vermelho", 60, 54m),
            // Microlote: e o unico produto em que escassez e verdade, e por isso
            // ele existe no seed — sem ele, o caminho ancorado nunca roda na demo.
            new ProdutoDoCatalogo("Geisha Microlote", 4, 190m),
        },
        politicaPorPerfil: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cafeteria"] = "ate 12% a partir de 5kg, faturado em 21 dias",
            ["revenda"] = "ate 18% a partir de 20kg, com contrato",
        },
        compradoresPorPerfil: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["cafeteria"] = 23,
            ["revenda"] = 8,
            // Tres compradores: existe o dado e ele NAO pode virar fala.
            ["assinatura corporativa"] = 3,
        },
        prazoPorRegiao: new Dictionary<string, string>
        {
            ["04"] = "2 dias uteis",
            ["01"] = "1 dia util",
            ["13"] = "4 dias uteis",
        });

    public Achado ConsultarEstoque(string produto) =>
        _catalogo.TryGetValue(produto.Trim(), out var item)
            ? Achado.De(item.EstoqueEmKg.ToString(CultureInfo.InvariantCulture))
            : Achado.Nada;

    public Achado PrecoVigente(string produto, int quantidade)
    {
        if (!_catalogo.TryGetValue(produto.Trim(), out var item)) return Achado.Nada;

        var desconto = DescontoPorFaixa(quantidade);
        var porKg = item.PrecoPorKg * (1 - desconto);
        var preco = $"{porKg.ToString("C", Brasil)}/kg";

        return Achado.De(desconto == 0
            ? preco
            : $"{preco} (-{desconto:P0} na faixa de {quantidade}kg)");
    }

    /// <summary>
    /// A faixa e da tabela, nao do vendedor: desconto negociado na conversa e
    /// margem que ninguem aprovou.
    /// </summary>
    private static decimal DescontoPorFaixa(int quantidade) => quantidade switch
    {
        >= 20 => 0.15m,
        >= 5 => 0.08m,
        _ => 0m,
    };

    public Achado PoliticaDesconto(string perfilCliente) =>
        _politicaPorPerfil.TryGetValue(perfilCliente.Trim(), out var politica)
            ? Achado.De(politica)
            // Perfil sem politica nao e' "desconto zero": e' "ninguem decidiu".
            // Devolver 0% autorizaria o agente a afirmar que nao ha desconto.
            : Achado.Nada;

    public Achado ClientesSemelhantesQueCompraram(string perfil)
    {
        if (!_compradoresPorPerfil.TryGetValue(perfil.Trim(), out var quantos))
            return Achado.Nada;

        return quantos < MinimoParaProvaSocialNaoIdentificar
            ? Achado.Nada
            : Achado.De($"{quantos} clientes do perfil {perfil.Trim()} nos ultimos 90 dias");
    }

    public Achado PrazoEntrega(string cep, string produto)
    {
        var digitos = new string(cep.Where(char.IsDigit).ToArray());
        if (digitos.Length < 8) return Achado.Nada;

        // Sem estoque, o prazo da regiao e' mentira: o relogio so comeca quando
        // o lote existe, e essa e' a data que o cliente vai cobrar.
        if (!_catalogo.TryGetValue(produto.Trim(), out var item) || item.EstoqueEmKg == 0)
            return Achado.Nada;

        return _prazoPorRegiao.TryGetValue(digitos[..2], out var prazo)
            ? Achado.De(prazo)
            : Achado.Nada;
    }
}
