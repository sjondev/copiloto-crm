using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Auditoria;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Testes;

/// <summary>
/// A trilha que decide o tamanho de um incidente (#84).
///
/// Sem ela, a empresa nao consegue dizer QUAIS titulares foram afetados — e ai
/// precisa comunicar todos, transformando um incidente pequeno num evento de
/// reputacao grande.
/// </summary>
public class AuditoriaTeste : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _conexao;
    private readonly DbContextOptions<CopilotoDbContext> _opcoes;

    public AuditoriaTeste()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<CopilotoDbContext>().UseSqlite(_conexao).Options;
        using var ctx = new CopilotoDbContext(_opcoes);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _conexao.Dispose();

    private static AcessoRegistrado Acesso(
        Guid usuario, Guid lead, DateTimeOffset quando,
        OperacaoAuditada operacao = OperacaoAuditada.Leu,
        OrigemDoAcesso origem = OrigemDoAcesso.Tela) =>
        new(Guid.NewGuid(), usuario, lead, operacao, origem, quando);

    [Fact]
    public void Acesso_sem_titular_nao_existe()
    {
        // A trilha existe para responder QUEM foi afetado.
        Assert.Throws<ArgumentException>(() => new AcessoRegistrado(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
            OperacaoAuditada.Leu, OrigemDoAcesso.Tela, T0));
    }

    [Fact]
    public void Acesso_de_gente_sem_autor_nao_existe()
    {
        // Trilha que nao aponta para pessoa nenhuma nao serve nem para conter o
        // incidente nem para responder a ANPD.
        Assert.Throws<ArgumentException>(() => new AcessoRegistrado(
            Guid.NewGuid(), null, Guid.NewGuid(),
            OperacaoAuditada.Leu, OrigemDoAcesso.Mcp, T0));
    }

    [Fact]
    public void Rotina_do_sistema_pode_nao_ter_autor()
    {
        // O Vigia varre sem ninguem pedir, e forcar um usuario ali seria
        // inventar um responsavel que nao existe.
        var acesso = new AcessoRegistrado(
            Guid.NewGuid(), null, Guid.NewGuid(),
            OperacaoAuditada.Leu, OrigemDoAcesso.Job, T0);

        Assert.Null(acesso.UsuarioId);
    }

    [Fact]
    public void A_trilha_diz_quais_titulares_uma_credencial_alcancou()
    {
        // A pergunta do dia seguinte ao vazamento — e a diferenca entre
        // comunicar tres pessoas e comunicar a base inteira.
        var vazado = Guid.NewGuid();
        var outro = Guid.NewGuid();
        var marina = Guid.NewGuid();
        var lucas = Guid.NewGuid();

        var acessos = new[]
        {
            Acesso(vazado, marina, T0),
            Acesso(vazado, lucas, T0.AddMinutes(2)),
            Acesso(vazado, marina, T0.AddMinutes(5)),          // repetido: conta uma vez
            Acesso(outro, Guid.NewGuid(), T0.AddMinutes(3)),   // outro usuario: fora
            Acesso(vazado, Guid.NewGuid(), T0.AddDays(-2)),    // antes da janela: fora
        };

        var alcancados = TrilhaDeAuditoria.TitularesAlcancadosPor(
            acessos, vazado, T0.AddMinutes(-10), T0.AddHours(1));

        Assert.Equal(2, alcancados.Count);
        Assert.Contains(marina, alcancados);
        Assert.Contains(lucas, alcancados);
    }

    [Fact]
    public void A_trilha_diz_quem_tocou_num_titular_em_ordem()
    {
        // A pergunta que o proprio titular tem direito de fazer.
        var marina = Guid.NewGuid();
        var acessos = new[]
        {
            Acesso(Guid.NewGuid(), marina, T0.AddHours(3)),
            Acesso(Guid.NewGuid(), marina, T0),
            Acesso(Guid.NewGuid(), Guid.NewGuid(), T0.AddHours(1)),
        };

        var historico = TrilhaDeAuditoria.QuemAcessou(acessos, marina);

        Assert.Equal(2, historico.Count);
        Assert.Equal(T0, historico[0].Quando);
    }

    [Fact]
    public void Volume_anormal_por_MCP_e_medido_a_parte()
    {
        // O servidor MCP existe para agente consumir em VOLUME: o que denuncia
        // abuso ali nao e "acessou algo estranho", e "acessou muita gente
        // rapido". Numa tela, trinta numa hora e um dia cheio.
        var agente = Guid.NewGuid();
        var muitos = Enumerable.Range(0, 40)
            .Select(i => Acesso(agente, Guid.NewGuid(), T0.AddSeconds(i), origem: OrigemDoAcesso.Mcp))
            .ToList();

        Assert.True(TrilhaDeAuditoria.VolumeAnormalPorMcp(
            muitos, T0.AddMinutes(1), TimeSpan.FromHours(1), limite: 30));
    }

    [Fact]
    public void Movimento_normal_de_tela_nao_vira_alarme_de_MCP()
    {
        var vendedor = Guid.NewGuid();
        var dia = Enumerable.Range(0, 40)
            .Select(i => Acesso(vendedor, Guid.NewGuid(), T0.AddMinutes(i)))
            .ToList();

        Assert.False(TrilhaDeAuditoria.VolumeAnormalPorMcp(
            dia, T0.AddHours(1), TimeSpan.FromHours(1), limite: 30));
    }

    [Fact]
    public void A_trilha_lista_o_que_saiu_da_nossa_rede()
    {
        // Primeira lista pedida quando uma chave de provedor vaza.
        var vendedor = Guid.NewGuid();
        var marina = Guid.NewGuid();
        var acessos = new[]
        {
            Acesso(vendedor, marina, T0, OperacaoAuditada.EnviouParaModelo),
            Acesso(vendedor, Guid.NewGuid(), T0.AddMinutes(1)),
        };

        var enviados = TrilhaDeAuditoria.TitularesEnviadosParaModelo(
            acessos, T0.AddMinutes(-1), T0.AddMinutes(5));

        Assert.Single(enviados);
        Assert.Equal(marina, enviados[0]);
    }

    [Fact]
    public void A_trilha_nao_guarda_conteudo_de_mensagem()
    {
        // Trilha que copia o dado pessoal vira a segunda copia a proteger, e a
        // primeira a vazar junto.
        var acesso = new AcessoRegistrado(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            OperacaoAuditada.Exportou, OrigemDoAcesso.Api, T0,
            detalhe: "exportacao de acesso, art. 18");

        Assert.Equal("exportacao de acesso, art. 18", acesso.Detalhe);
        Assert.DoesNotContain("R$", acesso.Detalhe);
    }

    [Fact]
    public void A_trilha_sobrevive_ao_banco_e_e_consultavel_por_usuario()
    {
        // A consulta que decide o tamanho da comunicacao roda no dia em que
        // ninguem tem tempo — por isso ela e indexada, e por isso ela e testada
        // contra o banco e nao so em memoria.
        var usuario = Guid.NewGuid();
        var marina = Guid.NewGuid();

        using (var ctx = new CopilotoDbContext(_opcoes))
        {
            ctx.Acessos.Add(Acesso(usuario, marina, T0, OperacaoAuditada.Exportou, OrigemDoAcesso.Mcp));
            ctx.Acessos.Add(Acesso(usuario, Guid.NewGuid(), T0.AddMinutes(1)));
            ctx.Acessos.Add(Acesso(Guid.NewGuid(), Guid.NewGuid(), T0.AddMinutes(2)));
            ctx.SaveChanges();
        }

        using var leitura = new CopilotoDbContext(_opcoes);
        var doUsuario = leitura.Acessos.Where(a => a.UsuarioId == usuario).ToList();

        Assert.Equal(2, doUsuario.Count);
        Assert.Contains(doUsuario, a => a.Operacao == OperacaoAuditada.Exportou
                                        && a.Origem == OrigemDoAcesso.Mcp
                                        && a.LeadId == marina);
    }
}
