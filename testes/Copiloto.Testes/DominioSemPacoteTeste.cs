using System.Reflection;
using System.Xml.Linq;

namespace Copiloto.Testes;

/// <summary>
/// O dominio e POCO puro (#48). A ausencia de pacote e o que prova isso — e
/// prova que ninguem confere apodrece, entao aqui esta quem confere.
/// </summary>
public class DominioSemPacoteTeste
{
    [Fact]
    public void Dominio_nao_referencia_pacote_nenhum()
    {
        var pacotes = ElementosDoDominio("PackageReference");

        Assert.True(
            pacotes.Count == 0,
            $"Copiloto.Dominio ganhou pacote: {string.Join(", ", pacotes)}. " +
            "O dominio e POCO puro por decisao (#48): sem PackageReference ele " +
            "nao consegue compilar um [Table] nem um DbContext, e e essa " +
            "impossibilidade que sustenta a regra. Mapeamento e persistencia " +
            "moram na Api.");
    }

    [Fact]
    public void Dominio_nao_depende_de_nenhum_outro_projeto()
    {
        var projetos = ElementosDoDominio("ProjectReference");

        Assert.True(
            projetos.Count == 0,
            $"Copiloto.Dominio passou a depender de: {string.Join(", ", projetos)}. " +
            "A referencia anda num sentido so, Api -> Dominio. O contrario " +
            "transforma o dominio em mais uma camada de aplicacao.");
    }

    private static List<string> ElementosDoDominio(string nome)
    {
        var caminho = Path.Combine(
            RaizDoRepositorio(), "src", "Copiloto.Dominio", "Copiloto.Dominio.csproj");

        // Arquivo ausente nao pode virar "nenhum pacote encontrado": um teste
        // que passa por nao ter achado o que conferir e pior que teste nenhum.
        Assert.True(File.Exists(caminho), $"csproj do dominio nao encontrado em {caminho}");

        return XDocument.Load(caminho)
            .Descendants(nome)
            .Select(e => e.Attribute("Include")?.Value ?? "(sem Include)")
            .ToList();
    }

    private static string RaizDoRepositorio()
    {
        var raiz = typeof(DominioSemPacoteTeste).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "RaizDoRepositorio")
            ?.Value;

        Assert.False(
            string.IsNullOrWhiteSpace(raiz),
            "Metadado RaizDoRepositorio ausente — ele e escrito pelo " +
            "Copiloto.Testes.csproj em tempo de compilacao.");

        return raiz!;
    }
}
