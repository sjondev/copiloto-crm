using System.Reflection;

namespace Copiloto.Testes;

/// <summary>
/// O termo certo para o que o PII Shield faz, cobrado pelo build (#83).
///
/// Pseudonimizacao e anonimizacao nao sao sinonimos: a primeira e reversivel
/// por quem tem a tabela e continua sob a LGPD; a segunda e irreversivel e sai
/// do escopo da lei. O escudo faz a primeira, porque o mapa que remonta fica do
/// nosso lado por construcao.
///
/// Isto e teste e nao convencao porque o termo errado e CONFORTAVEL: e mais
/// curto, soa mais forte, e ninguem revisando um PR de codigo vai reler o
/// README para conferir vocabulario juridico. A confusao gera decisao errada em
/// cascata — quem acredita ter anonimizado conclui que pode reter para sempre,
/// indexar a vontade e dispensar controle de acesso.
/// </summary>
public class LinguagemDaLgpdTeste
{
    /// <summary>
    /// Palavras que TRANSFORMAM a mencao em correcao, e nao em afirmacao.
    ///
    /// So se fala de anonimizacao aqui para dizer que NAO e isso — entao a
    /// mencao precisa vir com negacao ou com a explicacao do que a distingue.
    /// </summary>
    private static readonly string[] Ressalvas =
        ["não", "nao", "nunca", "irrevers", "sai do escopo", "≠", "acredita"];

    public static TheoryData<string> Documentos => new()
    {
        "README.md",
        Path.Combine("docs", "ARQUITETURA.md"),
        Path.Combine("docs", "LGPD.md"),
        Path.Combine("docs", "CONTRATO-TRATAMENTO.md"),
    };

    [Theory]
    [MemberData(nameof(Documentos))]
    public void Nenhum_documento_chama_o_escudo_de_anonimizacao(string relativo)
    {
        var caminho = Path.Combine(RaizDoRepositorio(), relativo);
        Assert.True(File.Exists(caminho), $"documento nao encontrado: {caminho}");

        // A checagem e por PARAGRAFO, e nao por linha: markdown quebra a frase
        // no meio, e a ressalva costuma cair na linha seguinte da que tem o
        // termo. Por linha, o gate acusaria o proprio texto que explica a
        // distincao — e gate que acusa o certo e desligado no mesmo dia.
        var afirmacoes = Paragrafos(File.ReadAllLines(caminho))
            .Where(p => p.Texto.Contains("anonimiz", StringComparison.OrdinalIgnoreCase))
            .Where(p => !Ressalvas.Any(r => p.Texto.Contains(r, StringComparison.OrdinalIgnoreCase)))
            .Select(p => $"{relativo}:{p.Linha}: {p.Texto.Trim()}")
            .ToList();

        Assert.True(afirmacoes.Count == 0,
            "Mencao a anonimizacao sem a ressalva de que NAO e o que fazemos. "
            + "O escudo pseudonimiza: o mapa que remonta fica do nosso lado, entao o "
            + "texto mascarado continua sendo dado pessoal sob a LGPD.\n"
            + string.Join("\n", afirmacoes));
    }

    [Fact]
    public void O_documento_de_LGPD_diz_que_dado_pseudonimizado_continua_sob_a_lei()
    {
        // A ausencia dessa frase e o modo silencioso de o documento perder o
        // ponto: ele continuaria falando de mascaramento, sem dizer o que isso
        // NAO resolve.
        var texto = File.ReadAllText(Path.Combine(RaizDoRepositorio(), "docs", "LGPD.md"));

        Assert.Contains("continua sendo\ndado pessoal", texto.Replace("**", ""),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void O_indice_do_RAG_esta_registrado_como_base_de_dado_pessoal()
    {
        // E a linha que mais escapa: vetor nao PARECE dado pessoal, parece
        // numero. Apagar o Lead sem apagar o vetor deixa o dado vivo depois de
        // o titular pedir exclusao.
        var texto = File.ReadAllText(Path.Combine(RaizDoRepositorio(), "docs", "LGPD.md"));

        Assert.Contains("índice de embeddings", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("base de dado pessoal", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void O_log_mascarado_esta_registrado_como_dado_pessoal()
    {
        var texto = File.ReadAllText(Path.Combine(RaizDoRepositorio(), "docs", "LGPD.md"));

        Assert.Contains("Log da aplicação", texto, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Blocos separados por linha em branco, com a linha de inicio.</summary>
    private static IEnumerable<(int Linha, string Texto)> Paragrafos(string[] linhas)
    {
        var atual = new List<string>();
        var inicio = 1;

        for (var i = 0; i < linhas.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(linhas[i]))
            {
                if (atual.Count > 0) yield return (inicio, string.Join(" ", atual));
                atual.Clear();
                continue;
            }

            if (atual.Count == 0) inicio = i + 1;
            atual.Add(linhas[i]);
        }

        if (atual.Count > 0) yield return (inicio, string.Join(" ", atual));
    }

    [Fact]
    public void Os_papeis_estao_definidos_com_quem_e_cada_um()
    {
        // Papel nao e formalidade: e quem responde perante o titular e a ANPD
        // (#78). Documento que fala de LGPD sem dizer quem e controlador
        // descreve obrigacoes sem dono.
        var texto = File.ReadAllText(Path.Combine(RaizDoRepositorio(), "docs", "LGPD.md"));

        Assert.Contains("Controlador", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Operador", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("empresa cliente", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void O_provedor_de_modelo_esta_mapeado_como_suboperador()
    {
        // E o item descoberto tarde: mandar a conversa para um provedor de IA e
        // subcontratar tratamento, e sem isso a empresa cliente compartilha
        // dado dos clientes dela com um terceiro que ela nao sabe que existe.
        var texto = File.ReadAllText(Path.Combine(RaizDoRepositorio(), "docs", "LGPD.md"));

        Assert.Contains("suboperador", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Provedor de modelo", texto, StringComparison.OrdinalIgnoreCase);
    }

    private static string RaizDoRepositorio()
    {
        var raiz = typeof(LinguagemDaLgpdTeste).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "RaizDoRepositorio")
            ?.Value;

        Assert.False(string.IsNullOrWhiteSpace(raiz),
            "Metadado RaizDoRepositorio ausente — ele e escrito pelo "
            + "Copiloto.Testes.csproj em tempo de compilacao.");

        return raiz!;
    }
}
