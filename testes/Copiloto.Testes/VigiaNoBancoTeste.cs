using Copiloto.Api.Persistencia;
using Copiloto.Api.Vigia;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Vendas;
using Copiloto.Dominio.Vigia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Copiloto.Testes;

/// <summary>
/// A varredura do Vigia contra o banco (#53).
///
/// O metodo e publico e sem timer justamente para isto: testar o job esperando
/// uma hora seria testar o `PeriodicTimer`, nao a varredura.
/// </summary>
public class VigiaNoBancoTeste : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _conexao;
    private readonly DbContextOptions<CopilotoDbContext> _opcoes;

    public VigiaNoBancoTeste()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<CopilotoDbContext>().UseSqlite(_conexao).Options;
        using var ctx = new CopilotoDbContext(_opcoes);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _conexao.Dispose();

    private static JobDoVigia Job() => new(new EscopoFalso(), NullLogger<JobDoVigia>.Instance);

    /// <summary>O job so usa a fabrica no laco do timer; a varredura recebe o contexto.</summary>
    private class EscopoFalso : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotSupportedException(
            "A varredura do teste recebe o DbContext direto, sem passar pelo laco.");
    }

    private Guid GravarDealComFala(string texto, DateTimeOffset falaEm, DateTimeOffset abertoEm)
    {
        using var ctx = new CopilotoDbContext(_opcoes);
        var leadId = Guid.NewGuid();
        var lead = new Lead(leadId, "+55 11 98888-1111", abertoEm, "Marina");
        var deal = new Deal(Guid.NewGuid(), leadId, abertoEm);
        var conversa = new Conversa(Guid.NewGuid(), leadId);
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, texto, falaEm));

        ctx.Leads.Add(lead);
        ctx.Deals.Add(deal);
        ctx.Conversas.Add(conversa);
        ctx.SaveChanges();

        return deal.Id;
    }

    [Fact]
    public async Task A_varredura_acha_o_cliente_calado_no_banco()
    {
        var dealId = GravarDealComFala("vou pensar", T0, T0);
        using var ctx = new CopilotoDbContext(_opcoes);

        var alertas = await Job().Varrer(ctx, T0.AddDays(5), default);

        var alerta = Assert.Single(alertas);
        Assert.Equal(dealId, alerta.DealId);
        Assert.Equal(MotivoDeAlerta.ClienteEmSilencio, alerta.Motivo);
        Assert.Contains("vou pensar", alerta.Texto);
    }

    [Fact]
    public async Task A_segunda_passagem_nao_repete_o_que_ja_avisou()
    {
        // O job roda de hora em hora sobre os mesmos dados: repetir treinaria o
        // vendedor a fechar a lista sem ler.
        GravarDealComFala("vou pensar", T0, T0);
        using var ctx = new CopilotoDbContext(_opcoes);
        var job = Job();

        Assert.Single(await job.Varrer(ctx, T0.AddDays(5), default));
        Assert.Empty(await job.Varrer(ctx, T0.AddDays(6), default));
    }

    [Fact]
    public async Task Deal_fechado_nao_entra_nem_na_consulta()
    {
        var abertoEm = T0;
        using (var ctx = new CopilotoDbContext(_opcoes))
        {
            var leadId = Guid.NewGuid();
            var deal = new Deal(Guid.NewGuid(), leadId, abertoEm);
            deal.MoverPara(Estagio.Ganho, abertoEm);

            ctx.Leads.Add(new Lead(leadId, "+55 11 98888-2222", abertoEm, "Marina"));
            ctx.Deals.Add(deal);
            ctx.SaveChanges();
        }

        using var leitura = new CopilotoDbContext(_opcoes);

        Assert.Empty(await Job().Varrer(leitura, T0.AddDays(60), default));
    }

    [Fact]
    public async Task Banco_vazio_nao_quebra_a_passagem()
    {
        using var ctx = new CopilotoDbContext(_opcoes);

        Assert.Empty(await Job().Varrer(ctx, T0, default));
    }

    [Fact]
    public async Task O_estagio_desde_sobrevive_ao_banco()
    {
        // Sem esta coluna, "parado ha 12 dias" nao teria como ser dito depois
        // de um restart.
        var id = Guid.NewGuid();
        using (var ctx = new CopilotoDbContext(_opcoes))
        {
            var leadId = Guid.NewGuid();
            var deal = new Deal(id, leadId, T0);
            deal.MoverPara(Estagio.Qualificacao, T0.AddDays(2));

            ctx.Leads.Add(new Lead(leadId, "+55 11 98888-3333", T0, "Marina"));
            ctx.Deals.Add(deal);
            ctx.SaveChanges();
        }

        using var leitura = new CopilotoDbContext(_opcoes);

        Assert.Equal(T0.AddDays(2), leitura.Deals.Single(d => d.Id == id).EstagioDesde);
    }
}
