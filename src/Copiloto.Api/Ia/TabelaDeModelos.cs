using Copiloto.Dominio.Ia;

namespace Copiloto.Api.Ia;

/// <summary>
/// Carrega a tabela do router a partir da configuracao (#29).
///
/// Ela vem de FORA por decisao. Trocar de modelo e a operacao mais frequente da
/// vida deste sistema — preco muda, modelo novo sai, provedor cai. Em codigo,
/// cada troca vira deploy; e o efeito pratico de "trocar exige deploy" e que
/// ninguem troca, e o sistema fica no modelo de dois anos atras.
///
/// O padrao do `appsettings.json` e o provedor FAKE, com custo zero: a suite e
/// a demo rodam offline e de graca, e ninguem sobe o projeto pela primeira vez
/// gastando dinheiro sem ter pedido.
/// </summary>
public static class TabelaDeModelos
{
    public const string Secao = "Modelos";

    public static IReadOnlyList<ModeloDisponivel> Carregar(IConfiguration config)
    {
        var lidos = config.GetSection(Secao).Get<List<ModeloConfigurado>>() ?? [];

        if (lidos.Count == 0)
            throw new InvalidOperationException(
                $"Nenhum modelo na secao '{Secao}' do appsettings. O router nao tem o "
                + "que escolher, e subir assim adiaria o erro para a primeira conversa "
                + "real — que e o pior momento para descobrir.");

        return lidos.Select(m => new ModeloDisponivel(
            m.Nome, m.Provedor, m.CustoPorMilTokens, m.LatenciaTipicaMs,
            m.Atende.Select(Enum.Parse<Tarefa>).ToList())).ToList();
    }

    /// <summary>A forma do JSON. Existe so para o binder da configuracao.</summary>
    private sealed class ModeloConfigurado
    {
        public string Nome { get; set; } = "";
        public string Provedor { get; set; } = "";
        public decimal CustoPorMilTokens { get; set; }
        public int LatenciaTipicaMs { get; set; }
        public List<string> Atende { get; set; } = [];
    }
}
