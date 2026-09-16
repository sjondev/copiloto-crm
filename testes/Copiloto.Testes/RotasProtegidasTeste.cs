using System.Text.RegularExpressions;

namespace Copiloto.Testes;

/// <summary>
/// Toda rota nova nasce exigindo credencial, ou esta listada aqui com o motivo
/// (#182).
///
/// Este teste le o CODIGO-FONTE, e nao a aplicacao de pe. E feio de proposito: a
/// suite chama os metodos de rota DIRETO, sem HTTP, por decisao escrita no
/// EndpointsDeLeitura — e autorizacao so existe na camada HTTP. Sem alguma
/// guarda, nenhum teste desta suite percebe que uma rota ficou aberta, que e
/// exatamente como a #49 entregou auth completo sem proteger um endpoint
/// sequer, e a conversa do cliente respondeu 200 sem token por dias.
///
/// A alternativa seria WebApplicationFactory, que e um pacote novo e um teste
/// que sobe a aplicacao inteira para conferir uma linha. Este aqui roda em
/// milissegundos e reprova pelo mesmo motivo. Quando houver integracao de
/// verdade, este teste sai.
///
/// Precedente na casa: `DominioSemPacoteTeste` le o `.csproj` pelo mesmo tipo de
/// razao — a prova esta no arquivo, e nao no comportamento.
/// </summary>
public class RotasProtegidasTeste
{
    /// <summary>
    /// As rotas ABERTAS, cada uma com o motivo. Entrar nesta lista tem que ser
    /// decisao, e nao esquecimento — e por isso ela mora num teste, onde muda-la
    /// aparece no diff.
    /// </summary>
    private static readonly Dictionary<string, string> Abertas = new()
    {
        ["/saude"] =
            "quem consulta e o orquestrador. Health check que exige token e "
            + "health check que ninguem configura, e ai ninguem sabe se o sistema caiu.",

        ["/webhook/whatsapp"] =
            "quem chama e o provedor, que nao tem como carregar JWT nosso. A "
            + "autenticacao dele e segredo compartilhado (#25).",

        ["/auth/login"] =
            "e a rota que EMITE credencial. Exigir credencial nela seria pedir a "
            + "chave para entregar a chave.",
    };

    private static readonly string Raiz = RaizDoRepositorio();

    [Fact]
    public void Nenhuma_rota_fica_aberta_sem_estar_na_lista()
    {
        var abertasSemMotivo = new List<string>();

        foreach (var (arquivo, conteudo) in ArquivosComRotas())
        {
            foreach (var (rota, trecho) in RotasDe(conteudo))
            {
                if (Abertas.ContainsKey(rota)) continue;
                if (trecho.Contains("RequireAuthorization", StringComparison.Ordinal)) continue;

                abertasSemMotivo.Add($"{rota} (em {arquivo})");
            }
        }

        Assert.True(abertasSemMotivo.Count == 0,
            "Rota sem RequireAuthorization e fora da lista de abertas: "
            + string.Join(", ", abertasSemMotivo)
            + ". Se ela deve mesmo ficar aberta, some a lista `Abertas` COM O MOTIVO.");
    }

    [Fact]
    public void O_hub_de_tempo_real_exige_credencial()
    {
        // O hub e a porta que ninguem olha: o SignalR nao manda header no
        // handshake, entao ele e o primeiro candidato a "deixa aberto que depois
        // a gente ve". Por ali sai o dossie inteiro, empurrado.
        var programa = File.ReadAllText(Path.Combine(Raiz, "src/Copiloto.Api/Program.cs"));

        Assert.Contains("MapHub<DossieHub>(DossieHub.Rota).RequireAuthorization()",
            programa, StringComparison.Ordinal);
    }

    [Fact]
    public void O_token_do_SignalR_e_lido_da_query_e_so_na_rota_do_hub()
    {
        // Ler `access_token` da query em QUALQUER rota transformaria a query
        // string num segundo jeito de autenticar — e query string vai parar em
        // log de servidor, em histórico de navegador e em Referer.
        var programa = File.ReadAllText(Path.Combine(Raiz, "src/Copiloto.Api/Program.cs"));

        Assert.Contains("access_token", programa, StringComparison.Ordinal);
        Assert.Contains("StartsWithSegments(DossieHub.Rota)", programa, StringComparison.Ordinal);
    }

    // --- Leitura do fonte ---

    private static IEnumerable<(string Arquivo, string Conteudo)> ArquivosComRotas()
    {
        foreach (var caminho in Directory.EnumerateFiles(
                     Path.Combine(Raiz, "src/Copiloto.Api"), "*.cs", SearchOption.AllDirectories))
        {
            if (caminho.Contains("/obj/", StringComparison.Ordinal)) continue;

            var conteudo = File.ReadAllText(caminho);
            if (conteudo.Contains(".Map", StringComparison.Ordinal))
                yield return (Path.GetFileName(caminho), conteudo);
        }
    }

    /// <summary>
    /// Cada `MapGet`/`MapPost`/... com a rota e o trecho ate o proximo `app.Map`
    /// ou o fim — que e onde um `RequireAuthorization` encadeado estaria.
    /// </summary>
    private static IEnumerable<(string Rota, string Trecho)> RotasDe(string conteudo)
    {
        var mapeamentos = Regex.Matches(
            conteudo,
            @"\.Map(?:Get|Post|Put|Delete|Patch)\(\s*""(?<rota>[^""]+)""",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        for (var i = 0; i < mapeamentos.Count; i++)
        {
            var inicio = mapeamentos[i].Index;
            var fim = i + 1 < mapeamentos.Count ? mapeamentos[i + 1].Index : conteudo.Length;

            // A rota com parametro entra pelo prefixo: `/leads/{id:guid}/dossie`
            // e `/leads/{id}/dossie` sao a mesma decisao.
            yield return (mapeamentos[i].Groups["rota"].Value, conteudo[inicio..fim]);
        }
    }

    private static string RaizDoRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Copiloto.sln")))
            dir = dir.Parent;

        return dir?.FullName
               ?? throw new InvalidOperationException(
                   "Nao achei a raiz do repositorio a partir de " + AppContext.BaseDirectory);
    }
}
