using System.ComponentModel;
using System.Reflection;
using Copiloto.Api.Mcp;
using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Fichas;
using Copiloto.Dominio.Vendas;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Copiloto.Testes;

/// <summary>
/// O CRM exposto como servidor MCP, so leitura (#56).
///
/// O contrato aqui tem duas metades. A primeira e o que a ferramenta responde,
/// testado contra o banco. A segunda e o que ela DIZ de si mesma: do outro lado
/// nao ha uma pessoa lendo a tela, ha um agente escolhendo sozinho qual chamar,
/// e descricao vaga nao produz erro — produz o agente chamando a ferramenta
/// errada e respondendo com confianca.
/// </summary>
public class ServidorMcpTeste : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _conexao;
    private readonly DbContextOptions<CopilotoDbContext> _opcoes;

    public ServidorMcpTeste()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<CopilotoDbContext>().UseSqlite(_conexao).Options;
        using var ctx = new CopilotoDbContext(_opcoes);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _conexao.Dispose();

    private Guid SemearMarina()
    {
        using var ctx = new CopilotoDbContext(_opcoes);
        var leadId = Guid.NewGuid();

        ctx.Leads.Add(new Lead(leadId, "+55 11 98888-1111", T0.AddDays(-30), "Marina"));

        var deal = new Deal(Guid.NewGuid(), leadId, T0.AddDays(-30));
        deal.MoverPara(Estagio.Qualificacao, T0.AddDays(-20));
        ctx.Deals.Add(deal);

        var conversa = new Conversa(Guid.NewGuid(), leadId);
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, "qual o valor do kg?", T0.AddDays(-9)));
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Vendedor, "R$ 68", T0.AddDays(-9).AddMinutes(4)));
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, "vou pensar", T0.AddDays(-8)));
        ctx.Conversas.Add(conversa);

        var ficha = new FichaCliente(Guid.NewGuid(), leadId, T0.AddDays(-30));
        ficha.Atualizar(T0.AddDays(-30), empresa: new SobreAEmpresa(Ramo: "cafeteria de bairro"));
        ctx.Fichas.Add(ficha);

        ctx.SaveChanges();
        return leadId;
    }

    // --- O que as ferramentas respondem ---

    [Fact]
    public async Task Buscar_lead_acha_por_nome_parcial()
    {
        SemearMarina();
        using var ctx = new CopilotoDbContext(_opcoes);

        var achados = await ConsultasDoCrm.BuscarLead(ctx, "mari", default);

        Assert.Single(achados);
        Assert.Equal("Marina", achados[0].Nome);
    }

    [Fact]
    public async Task Buscar_lead_acha_por_telefone_em_qualquer_formato()
    {
        // O agente do outro lado copia o telefone como veio na conversa.
        SemearMarina();
        using var ctx = new CopilotoDbContext(_opcoes);

        Assert.Single(await ConsultasDoCrm.BuscarLead(ctx, "(11) 98888-1111", default));
        Assert.Single(await ConsultasDoCrm.BuscarLead(ctx, "11988881111", default));
    }

    [Fact]
    public async Task Busca_vazia_devolve_lista_vazia_e_nao_a_base_inteira()
    {
        // Termo em branco vindo de um agente e o caminho mais curto para
        // exportar a carteira sem querer.
        SemearMarina();
        using var ctx = new CopilotoDbContext(_opcoes);

        Assert.Empty(await ConsultasDoCrm.BuscarLead(ctx, "   ", default));
    }

    [Fact]
    public async Task A_ficha_traz_o_anotado_a_lacuna_e_os_dois_relogios()
    {
        var leadId = SemearMarina();
        using var ctx = new CopilotoDbContext(_opcoes);

        var ficha = await ConsultasDoCrm.ObterFicha(ctx, leadId, T0, default);

        Assert.NotNull(ficha);
        Assert.Equal("cafeteria de bairro", ficha!.Anotado["Ramo"]);
        Assert.Contains("Cargo", ficha.Lacunas);
        Assert.Equal("Qualificacao", ficha.Estagio);
        Assert.Equal(20, ficha.DiasNoEstagio);
        Assert.Equal(8, ficha.DiasSemFalarComOCliente);
    }

    [Fact]
    public async Task Lead_inexistente_devolve_nulo_e_nao_ficha_vazia()
    {
        using var ctx = new CopilotoDbContext(_opcoes);

        Assert.Null(await ConsultasDoCrm.ObterFicha(ctx, Guid.NewGuid(), T0, default));
    }

    [Fact]
    public async Task Negocios_parados_contam_da_ultima_mudanca_de_estagio()
    {
        // Negocio de dois meses que andou ontem nao esta parado.
        SemearMarina();
        using var ctx = new CopilotoDbContext(_opcoes);

        var parados = await ConsultasDoCrm.ListarNegociosParados(ctx, dias: 10, T0, default);

        var parado = Assert.Single(parados);
        Assert.Equal(20, parado.DiasParado);
        Assert.Equal("Marina", parado.Nome);

        Assert.Empty(await ConsultasDoCrm.ListarNegociosParados(ctx, dias: 30, T0, default));
    }

    [Fact]
    public async Task Negocio_fechado_nao_entra_na_lista_de_parados()
    {
        var leadId = SemearMarina();
        using (var ctx = new CopilotoDbContext(_opcoes))
        {
            var deal = ctx.Deals.Single(d => d.LeadId == leadId);
            deal.MoverPara(Estagio.Ganho, T0.AddDays(-1));
            ctx.SaveChanges();
        }

        using var leitura = new CopilotoDbContext(_opcoes);

        Assert.Empty(await ConsultasDoCrm.ListarNegociosParados(leitura, dias: 0, T0, default));
    }

    [Fact]
    public async Task O_historico_vem_em_ordem_cronologica()
    {
        var leadId = SemearMarina();
        using var ctx = new CopilotoDbContext(_opcoes);

        var falas = await ConsultasDoCrm.HistoricoDaConversa(ctx, leadId, limite: 20, default);

        Assert.Equal(3, falas.Count);
        Assert.Equal("qual o valor do kg?", falas[0].Texto);
        Assert.Equal("vou pensar", falas[2].Texto);
    }

    [Fact]
    public async Task O_historico_traz_as_MAIS_RECENTES_quando_o_limite_corta()
    {
        // Cortar pelas primeiras devolveria o comeco da conversa e esconderia a
        // objecao, que e sempre a ultima coisa dita.
        var leadId = SemearMarina();
        using var ctx = new CopilotoDbContext(_opcoes);

        var falas = await ConsultasDoCrm.HistoricoDaConversa(ctx, leadId, limite: 1, default);

        Assert.Single(falas);
        Assert.Equal("vou pensar", falas[0].Texto);
    }

    [Fact]
    public async Task O_teto_vale_mesmo_quando_o_agente_pede_mais()
    {
        // Do outro lado ha um agente que nao sente a conta crescer.
        var leadId = SemearMarina();
        using var ctx = new CopilotoDbContext(_opcoes);

        var falas = await ConsultasDoCrm.HistoricoDaConversa(ctx, leadId, limite: 5000, default);

        Assert.True(falas.Count <= ConsultasDoCrm.TetoDeResultados);
    }

    // --- O que as ferramentas dizem de si mesmas ---

    private static IReadOnlyList<MethodInfo> Ferramentas() =>
        typeof(FerramentasDoCrm).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .ToList();

    [Fact]
    public void As_quatro_ferramentas_de_leitura_estao_expostas()
    {
        var nomes = Ferramentas()
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()!.Name)
            .ToList();

        Assert.Contains("buscar_lead", nomes);
        Assert.Contains("obter_ficha", nomes);
        Assert.Contains("listar_negocios_parados", nomes);
        Assert.Contains("historico_conversa", nomes);
    }

    [Fact]
    public void Toda_ferramenta_diz_o_que_faz_e_quando_usar()
    {
        // Criterio de aceite da issue: descricao boa o bastante para o cliente
        // MCP escolher sozinho. Uma linha generica ("busca dados") passaria em
        // qualquer teste de existencia e falharia na hora que importa.
        foreach (var ferramenta in Ferramentas())
        {
            var descricao = ferramenta.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";

            Assert.True(descricao.Length >= 120,
                $"{ferramenta.Name}: descricao com {descricao.Length} caracteres, curta "
                + "demais para um agente escolher sozinho.");
        }
    }

    [Fact]
    public void Todo_parametro_de_ferramenta_e_descrito()
    {
        // Parametro sem descricao e onde o agente inventa: manda o nome no
        // campo que espera id, recebe vazio, e conclui que nao ha dado.
        foreach (var ferramenta in Ferramentas())
        {
            foreach (var p in ferramenta.GetParameters())
            {
                if (p.ParameterType == typeof(CopilotoDbContext)
                    || p.ParameterType == typeof(CancellationToken)) continue;

                Assert.True(p.GetCustomAttribute<DescriptionAttribute>() is not null,
                    $"{ferramenta.Name}: parametro '{p.Name}' sem descricao.");
            }
        }
    }

    [Fact]
    public void Nenhuma_ferramenta_altera_estado()
    {
        // Limite desta issue, e conferido no FONTE porque reflexao nao le corpo
        // de metodo. Escrita por MCP significaria um agente movendo negocio de
        // estagio a partir de uma frase — e o produto existe para o contrario.
        var pasta = Path.Combine(RaizDoRepositorio(), "src", "Copiloto.Api", "Mcp");
        var proibidos = new[] { "SaveChanges", ".Add(", ".Remove(", ".Update(", ".ExecuteDelete" };

        foreach (var arquivo in Directory.GetFiles(pasta, "*.cs"))
        {
            var codigo = File.ReadAllText(arquivo);
            foreach (var termo in proibidos)
            {
                Assert.False(codigo.Contains(termo, StringComparison.Ordinal),
                    $"{Path.GetFileName(arquivo)} contem '{termo}': a superficie MCP e "
                    + "somente leitura nesta issue (#56).");
            }
        }
    }

    private static string RaizDoRepositorio()
    {
        var raiz = typeof(ServidorMcpTeste).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "RaizDoRepositorio")?.Value;

        Assert.False(string.IsNullOrWhiteSpace(raiz), "Metadado RaizDoRepositorio ausente.");
        return raiz!;
    }
}
