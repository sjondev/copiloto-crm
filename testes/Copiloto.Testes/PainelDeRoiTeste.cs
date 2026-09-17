using System.Diagnostics;
using Copiloto.Api.Leitura;
using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Ia;
using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Testes;

/// <summary>
/// O painel que responde se a IA se paga (#3).
///
/// Quase todo projeto de IA sabe dizer o que GASTOU. Quase nenhum sabe dizer o
/// que rendeu — e sem os dois lados o numero nao responde nada. Os testes abaixo
/// fixam as duas metades e, principalmente, o que NAO pode ser creditado ao
/// copiloto.
/// </summary>
/// <remarks>
/// Roda contra POSTGRES, e nao contra o SQLite da suite. O painel agrega no
/// banco porque o criterio da issue e responder com 10 mil invocacoes em menos
/// de um segundo — e o SQLite nao traduz comparacao de `DateTimeOffset` nem no
/// WHERE:
///
///   The LINQ expression '.Where(a =&gt; a.Quando &gt;= de &amp;&amp; a.Quando &lt;= ate)'
///   could not be translated.
///
/// E o TECH-005 outra vez, e desta vez mais fundo: ele ja era conhecido no ORDER
/// BY, e o contorno de sempre — trazer para a memoria e ordenar la — nao serve
/// aqui, porque trazer dez mil linhas para somar e exatamente o que o criterio
/// de tempo proibe.
///
/// Sem POSTGRES_URL estes testes PULAM, em vez de passar sem ter rodado.
/// </remarks>
[Collection(BancoPostgres.Nome)]
public class PainelDeRoiTeste : IAsyncLifetime
{
    private static string? Url => BancoPostgres.Cadeia();

    private CopilotoDbContext? _ctx;

    public async Task InitializeAsync()
    {
        if (Url is null) return;

        _ctx = new CopilotoDbContext(new DbContextOptionsBuilder<CopilotoDbContext>()
            .UseNpgsql(Url, npg => npg.UseVector()).Options);

        await _ctx.Database.EnsureDeletedAsync();
        await _ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_ctx is not null) await _ctx.DisposeAsync();
    }

    private static CopilotoDbContext NovoContexto() => new(
        new DbContextOptionsBuilder<CopilotoDbContext>()
            .UseNpgsql(Url, npg => npg.UseVector()).Options);

    private static readonly DateTimeOffset T0 = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Comeco = T0.AddDays(-30);

    private int _proximoNumero;

    private Guid SemearLead(Guid? vendedor = null)
    {
        using var ctx = NovoContexto();

        var lead = new Lead(
            Guid.NewGuid(), $"+55119{(++_proximoNumero):D4}0000", Comeco, "Cliente");
        if (vendedor is { } dono) lead.Assumir(dono);

        ctx.Leads.Add(lead);
        ctx.SaveChanges();

        return lead.Id;
    }

    /// <summary>Um negocio com o custo de IA e, se ganho, o valor que rendeu.</summary>
    private static Deal SemearDeal(
        Guid leadId, decimal[] custos, decimal? ganhouPor = null,
        string modelo = "fake-forte", bool? aceite = null)
    {
        using var ctx = NovoContexto();

        var deal = new Deal(Guid.NewGuid(), leadId, Comeco);

        foreach (var custo in custos)
        {
            var invocacao = new AiInvocation(
                Guid.NewGuid(), Tarefa.Plano,
                new MedicaoDaChamada(modelo, 100, 40, 12, 1, true, custo),
                T0.AddDays(-1), deal.Id);

            if (aceite is { } decidiu) invocacao.RegistrarAceite(decidiu);

            deal.RegistrarInvocacao(invocacao);
        }

        if (ganhouPor is { } valor) deal.MoverPara(Estagio.Ganho, T0.AddDays(-1), valor);

        ctx.Deals.Add(deal);
        ctx.SaveChanges();

        return deal;
    }

    private static MetricasNaTela Painel(Guid? vendedor = null)
    {
        using var ctx = NovoContexto();
        return EndpointsDasMetricas.Montar(ctx, Comeco, T0, vendedor, CancellationToken.None).Result;
    }

    // --- Os dois lados da conta ---

    [SkippableFact]
    public void O_custo_soma_as_invocacoes_do_periodo()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: o painel agrega no banco");

        SemearDeal(SemearLead(), [0.15m, 0.25m]);
        SemearDeal(SemearLead(), [0.60m]);

        var painel = Painel();

        Assert.Equal(1.00m, painel.CustoIaEmReais);
        Assert.Equal(3, painel.Invocacoes);
    }

    [SkippableFact]
    public void A_receita_soma_o_valor_dos_negocios_GANHOS()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: o painel agrega no banco");

        SemearDeal(SemearLead(), [0.10m], ganhouPor: 2000m);
        SemearDeal(SemearLead(), [0.10m]);   // ainda aberto

        var painel = Painel();

        Assert.Equal(1, painel.NegociosGanhos);
        Assert.Equal(2000m, painel.ReceitaGanha);
    }

    /// <summary>
    /// A distincao que sustenta o numero: somar TODOS os ganhos creditaria ao
    /// copiloto a venda que aconteceu sem ele.
    /// </summary>
    [SkippableFact]
    public void A_receita_INFLUENCIADA_ignora_o_ganho_que_nao_usou_IA()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: o painel agrega no banco");

        SemearDeal(SemearLead(), [0.50m], ganhouPor: 3000m);   // usou
        SemearDeal(SemearLead(), [], ganhouPor: 9000m);        // ganhou sozinho

        var painel = Painel();

        Assert.Equal(12000m, painel.ReceitaGanha);
        Assert.Equal(3000m, painel.ReceitaInfluenciada);
        Assert.Equal(1, painel.GanhosComIa);
    }

    [SkippableFact]
    public void A_razao_e_receita_influenciada_por_real_gasto()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: o painel agrega no banco");

        SemearDeal(SemearLead(), [2m], ganhouPor: 1000m);

        Assert.Equal(500m, Painel().ReceitaPorRealGasto);
    }

    [SkippableFact]
    public void Sem_gasto_nenhum_a_razao_e_nula_e_nao_infinita()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: o painel agrega no banco");

        SemearDeal(SemearLead(), [], ganhouPor: 1000m);

        // Dividir por zero daria "infinito por real gasto", que e um numero
        // impressionante e sem sentido.
        Assert.Null(Painel().ReceitaPorRealGasto);
    }

    // --- A ressalva viaja junto (#6) ---

    /// <summary>
    /// Numero de ROI solto vira slide, e slide com vies de selecao nao declarado
    /// e o jeito mais rapido de perder a confianca de quem entende do assunto.
    /// </summary>
    [SkippableFact]
    public void A_ressalva_de_vies_vem_na_RESPOSTA_e_nao_so_na_tela()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: o painel agrega no banco");

        var painel = Painel();

        Assert.Contains("NAO e um experimento controlado", painel.Ressalva, StringComparison.Ordinal);
        Assert.Contains("correlacao, nao causa", painel.Ressalva, StringComparison.Ordinal);
    }

    // --- Quebra por agente e por modelo ---

    [SkippableFact]
    public void O_custo_se_quebra_por_modelo()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: o painel agrega no banco");

        SemearDeal(SemearLead(), [1m], modelo: "caro");
        SemearDeal(SemearLead(), [0.10m, 0.10m], modelo: "barato");

        var porModelo = Painel().PorModelo;

        Assert.Equal(1m, Assert.Single(porModelo, m => m.Nome == "caro").CustoEmReais);
        Assert.Equal(2, Assert.Single(porModelo, m => m.Nome == "barato").Invocacoes);
    }

    // --- Taxa de aceite ---

    [SkippableFact]
    public void A_taxa_de_aceite_conta_usadas_sobre_decididas()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: o painel agrega no banco");

        SemearDeal(SemearLead(), [0.10m, 0.10m, 0.10m], modelo: "fake-forte", aceite: true);
        SemearDeal(SemearLead(), [0.10m], modelo: "fake-forte", aceite: false);

        var aceite = Assert.Single(Painel().AceitePorModelo, a => a.Nome == "fake-forte");

        Assert.Equal(3, aceite.Aceitas);
        Assert.Equal(1, aceite.Ignoradas);
        Assert.Equal(0.75m, aceite.Taxa);
    }

    /// <summary>
    /// Indecisao NAO e recusa: taxa que conta o que ninguem olhou como "ignorou"
    /// mede a velocidade do vendedor, e nao a qualidade do modelo.
    /// </summary>
    [SkippableFact]
    public void Sugestao_que_ninguem_decidiu_fica_fora_da_taxa()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: o painel agrega no banco");

        SemearDeal(SemearLead(), [0.10m], modelo: "fake-forte");

        Assert.Empty(Painel().AceitePorModelo);
    }

    // --- Filtro por vendedor ---

    [SkippableFact]
    public void O_filtro_por_vendedor_so_conta_a_carteira_dele()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: o painel agrega no banco");

        var ana = Guid.NewGuid();
        var bruno = Guid.NewGuid();

        SemearDeal(SemearLead(ana), [1m], ganhouPor: 1000m);
        SemearDeal(SemearLead(bruno), [5m], ganhouPor: 9000m);

        var painel = Painel(vendedor: ana);

        Assert.Equal(1m, painel.CustoIaEmReais);
        Assert.Equal(1000m, painel.ReceitaInfluenciada);
    }

    // --- O criterio de tempo ---

    /// <summary>
    /// O criterio da issue: menos de 1s com 10 mil invocacoes. O numero aqui e
    /// folgado de proposito — o que este teste protege e a ORDEM DE GRANDEZA, e
    /// reprovar por 50ms num runner ocupado trocaria um alerta util por um teste
    /// intermitente que alguem desliga.
    /// </summary>
    [SkippableFact]
    public void Dez_mil_invocacoes_respondem_em_menos_de_um_segundo()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: o painel agrega no banco");

        var leadId = SemearLead();

        using (var ctx = NovoContexto())
        {
            var deal = new Deal(Guid.NewGuid(), leadId, Comeco);

            for (var i = 0; i < 10_000; i++)
            {
                deal.RegistrarInvocacao(new AiInvocation(
                    Guid.NewGuid(), i % 2 == 0 ? Tarefa.Leitura : Tarefa.Plano,
                    new MedicaoDaChamada(
                        i % 3 == 0 ? "caro" : "barato", 100, 40, 12, 1, true, 0.0001m),
                    T0.AddDays(-1), deal.Id));
            }

            ctx.Deals.Add(deal);
            ctx.SaveChanges();
        }

        var relogio = Stopwatch.StartNew();
        var painel = Painel();
        relogio.Stop();

        Assert.Equal(10_000, painel.Invocacoes);
        Assert.True(relogio.ElapsedMilliseconds < 1000,
            $"o painel levou {relogio.ElapsedMilliseconds}ms com 10 mil invocacoes");
    }
}
