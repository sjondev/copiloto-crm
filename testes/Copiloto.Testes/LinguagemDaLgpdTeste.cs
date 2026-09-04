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
        Path.Combine("docs", "REGISTRO-DE-TRATAMENTO.md"),
        Path.Combine("docs", "BASE-LEGAL.md"),
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

    /// <summary>
    /// O que toda operacao do registro precisa declarar (#76).
    ///
    /// Sao os campos do art. 37 que decidem alguma coisa. Operacao sem
    /// RETENCAO e a que mais escapa, e e a que vira "guardamos desde 2019
    /// porque ninguem definiu prazo".
    /// </summary>
    private static readonly string[] CamposObrigatorios =
        ["**Finalidade:**", "**Titulares:**", "**Dados:**", "**Base legal:**",
         "**Compartilhamento:**", "**Retenção:**", "**Segurança:**", "**Estado:**"];

    [Fact]
    public void Toda_operacao_registrada_declara_os_campos_que_decidem()
    {
        // O registro e cobrado pelo build porque operacao nova entra por PR de
        // feature, e quem esta escrevendo codigo nao lembra de voltar no
        // documento — a nao ser que ele reprove.
        var caminho = Path.Combine(RaizDoRepositorio(), "docs", "REGISTRO-DE-TRATAMENTO.md");
        var texto = File.ReadAllText(caminho);

        var operacoes = texto.Split("\n## ")
            .Where(bloco => char.IsDigit(bloco.FirstOrDefault()))
            .ToList();

        Assert.True(operacoes.Count >= 5,
            $"O registro tem {operacoes.Count} operacoes numeradas — poucas para "
            + "descrever o que o sistema faz.");

        var faltando = operacoes
            .SelectMany(op => CamposObrigatorios
                .Where(campo => !op.Contains(campo))
                .Select(campo => $"{op.Split('\n')[0].Trim()}: falta {campo}"))
            .ToList();

        Assert.True(faltando.Count == 0,
            "Operacao de tratamento sem campo obrigatorio do art. 37:\n"
            + string.Join("\n", faltando));
    }

    [Fact]
    public void O_registro_diz_o_que_existe_e_o_que_ainda_e_issue()
    {
        // Registro que descreve o sistema pretendido, e nao o que roda, passa
        // em auditoria e mente para quem decide.
        var texto = File.ReadAllText(
            Path.Combine(RaizDoRepositorio(), "docs", "REGISTRO-DE-TRATAMENTO.md"));

        Assert.Contains("**Estado:** existe", texto);
        Assert.Contains("issue #", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Nenhuma_operacao_ficou_sem_base_legal_definida()
    {
        // Base legal determina o que o produto PODE fazer — consentimento pede
        // revogação que para na hora, legitimo interesse pede canal de
        // oposicao. Sao funcionalidades diferentes, e descobrir depois de
        // construir custa retrabalho (#77).
        var texto = File.ReadAllText(
            Path.Combine(RaizDoRepositorio(), "docs", "REGISTRO-DE-TRATAMENTO.md"));

        Assert.DoesNotContain("Base legal:** pendente", texto);
    }

    [Fact]
    public void A_avaliacao_de_legitimo_interesse_encara_a_pergunta_desconfortavel()
    {
        // O cliente que manda mensagem para uma torrefacao espera ser analisado
        // por IA? A resposta honesta e "provavelmente nao", e e ela que obriga
        // a salvaguarda mais forte. Uma LIA que so lista beneficios nao e
        // avaliacao, e folheto.
        var texto = File.ReadAllText(Path.Combine(RaizDoRepositorio(), "docs", "BASE-LEGAL.md"));

        Assert.Contains("provavelmente não", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("oposição", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("revisão jurídica", texto, StringComparison.OrdinalIgnoreCase);
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
