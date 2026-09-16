using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Rag;
using Copiloto.Dominio.Vendas;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Testes;

/// <summary>
/// Conversa de parceiro fora da base de precedentes de venda (#85).
///
/// O risco nao e so de dado pessoal: se conversa com fornecedor entra no mesmo
/// indice que conversa com cliente, o sistema pode recuperar uma negociacao de
/// fornecimento — com margem e custo — enquanto o vendedor atende um comprador.
/// </summary>
public class DadosDeParceiroTeste : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _conexao;
    private readonly DbContextOptions<CopilotoDbContext> _opcoes;

    public DadosDeParceiroTeste()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<CopilotoDbContext>().UseSqlite(_conexao).Options;
        using var ctx = new CopilotoDbContext(_opcoes);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _conexao.Dispose();

    private static Lead Cliente(string telefone = "+55 11 98888-1111") =>
        new(Guid.NewGuid(), telefone, T0, "Marina");

    private static Lead Parceiro(string telefone = "+55 35 98888-2222")
    {
        var lead = new Lead(Guid.NewGuid(), telefone, T0, "Fazenda Serra Alta");
        lead.MarcarComo(Relacao.Parceiro);
        return lead;
    }

    [Fact]
    public void Lead_nasce_cliente()
    {
        // Errar para esse lado e menos grave: cliente marcado como parceiro
        // perde recurso; parceiro marcado como cliente vaza margem.
        Assert.Equal(Relacao.Cliente, Cliente().Relacao);
    }

    [Fact]
    public void Conversa_de_parceiro_nao_entra_na_base_de_precedentes()
    {
        Assert.True(BaseDePrecedentes.PodeIndexar(Cliente()));
        Assert.False(BaseDePrecedentes.PodeIndexar(Parceiro()));
    }

    [Fact]
    public void Precedente_de_parceiro_nunca_sai_para_atendimento_de_cliente()
    {
        // O criterio concreto da issue: margem e custo do fornecedor perto de
        // um comprador.
        var comprador = Cliente();
        var candidatos = new[]
        {
            (Origem: Cliente("+55 11 97777-0001"), Texto: "fechou com 8% em 5kg"),
            (Origem: Parceiro(), Texto: "compramos o lote a R$ 32 o quilo"),
        };

        var permitidos = BaseDePrecedentes.Filtrar(candidatos, c => c.Origem, comprador);

        Assert.Single(permitidos);
        Assert.DoesNotContain(permitidos, p => p.Texto.Contains("R$ 32"));
    }

    [Fact]
    public void Atendimento_de_parceiro_nao_recebe_precedente_de_cliente()
    {
        // A direcao contraria do mesmo vazamento: o dossie de um fornecedor
        // recheado com conversas de clientes.
        var fornecedor = Parceiro();
        var candidatos = new[] { (Origem: Cliente(), Texto: "cliente fechou 3kg") };

        Assert.Empty(BaseDePrecedentes.Filtrar(candidatos, c => c.Origem, fornecedor));
    }

    [Fact]
    public void Nao_ha_flag_para_ligar_parceiro_no_indice_de_vendas()
    {
        // "Salvo decisao explicita" nao virou parametro: flag assim e ligada
        // uma vez e nunca mais revisada. Se houver base de precedentes de
        // COMPRA, ela e outra base — nao esta com um booleano invertido.
        var metodos = typeof(BaseDePrecedentes).GetMethods()
            .SelectMany(m => m.GetParameters())
            .Select(p => p.ParameterType);

        Assert.DoesNotContain(typeof(bool), metodos);
    }

    [Fact]
    public void A_relacao_sobrevive_ao_banco()
    {
        // Se a relacao se perdesse no restart, o isolamento valeria ate a
        // proxima subida — e o vazamento voltaria sem ninguem mexer em nada.
        var id = Guid.NewGuid();
        using (var ctx = new CopilotoDbContext(_opcoes))
        {
            var lead = new Lead(id, "+55 35 98888-2222", T0, "Fazenda Serra Alta");
            lead.MarcarComo(Relacao.Parceiro);
            ctx.Leads.Add(lead);
            ctx.Leads.Add(Cliente());
            ctx.SaveChanges();
        }

        using var leitura = new CopilotoDbContext(_opcoes);

        Assert.Equal(Relacao.Parceiro, leitura.Leads.Single(l => l.Id == id).Relacao);
        Assert.Single(leitura.Leads.Where(l => l.Relacao == Relacao.Cliente));
    }

    [Fact]
    public void Da_para_consultar_parceiros_separado_no_banco()
    {
        // Escopo de acesso separado comeca por conseguir SEPARAR na consulta:
        // sem isso, "so leads de cliente" viraria filtro em memoria, e filtro
        // em memoria e o que alguem esquece na proxima tela.
        using (var ctx = new CopilotoDbContext(_opcoes))
        {
            ctx.Leads.Add(Parceiro());
            ctx.Leads.Add(Parceiro("+55 35 98888-3333"));
            ctx.Leads.Add(Cliente());
            ctx.SaveChanges();
        }

        using var leitura = new CopilotoDbContext(_opcoes);

        Assert.Equal(2, leitura.Leads.Count(l => l.Relacao == Relacao.Parceiro));
    }
}
