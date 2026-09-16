using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Testes;

/// <summary>
/// A busca de Lead contra Postgres de verdade (#177).
///
/// Existe porque os outros numeros da pessoa sao uma COLECAO, e os dois
/// provedores a guardam de forma diferente: `text[]` no Postgres, JSON no
/// SQLite. A traducao da consulta nao e a mesma, entao o teste em SQLite passar
/// NAO prova que a producao funciona — e producao aqui e Postgres.
///
/// Esta distancia ja custou caro duas vezes neste repositorio no mesmo dia: uma
/// migration que so quebra com linha existente (#172) e outra que so quebra com
/// coluna NOT NULL sem default. As duas passaram pela suite inteira em verde,
/// porque a suite monta o schema com `EnsureCreated()` a partir do modelo e
/// nunca executa migration.
///
/// Sem banco, PULA em vez de passar. Para rodar:
///
///   POSTGRES_URL="Host=localhost;Port=5442;Database=copiloto;Username=copiloto;Password=..." dotnet test
/// </summary>
[Collection(BancoPostgres.Nome)]
public class LeadNoPostgresTeste : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private static string? Url => Environment.GetEnvironmentVariable("POSTGRES_URL");

    private CopilotoDbContext? _ctx;

    public async Task InitializeAsync()
    {
        if (Url is null) return;

        _ctx = new CopilotoDbContext(new DbContextOptionsBuilder<CopilotoDbContext>()
            .UseNpgsql(Url, npg => npg.UseVector()).Options);

        // Recria do zero e aplica as MIGRATIONS, e nao EnsureCreated: e' o
        // caminho que a producao percorre, e o unico que reprova quando uma
        // migration esta errada.
        await _ctx.Database.EnsureDeletedAsync();
        await _ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_ctx is not null) await _ctx.DisposeAsync();
    }

    [SkippableFact]
    public void O_cliente_que_trocou_de_chip_e_achado_pelo_numero_novo()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: Postgres nao disponivel");

        var repo = new LeadsNoBanco(_ctx!);
        var lead = new Lead(Guid.NewGuid(), "+5511987654321", T0);
        lead.TambemFalaPor("+55 11 97777-1111");
        repo.Adicionar(lead);
        _ctx!.SaveChanges();

        _ctx.ChangeTracker.Clear();

        var achado = repo.PorTelefone("+5511977771111");

        Assert.NotNull(achado);
        Assert.Equal(lead.Id, achado.Id);
    }

    [SkippableFact]
    public void Numero_de_outra_pessoa_nao_acha_lead_nenhum()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: Postgres nao disponivel");

        var repo = new LeadsNoBanco(_ctx!);
        var lead = new Lead(Guid.NewGuid(), "+5511987654321", T0);
        lead.TambemFalaPor("+55 11 97777-1111");
        repo.Adicionar(lead);
        _ctx!.SaveChanges();

        _ctx.ChangeTracker.Clear();

        Assert.Null(repo.PorTelefone("+5511966660000"));
    }

    /// <summary>
    /// A coluna nasce com lista VAZIA, e nao nula. E o que faz a migration
    /// atravessar uma base que ja tem linhas — sem o default, o Postgres recusa
    /// a alteracao inteira com `23502: contains null values`.
    /// </summary>
    [SkippableFact]
    public void Lead_sem_numero_adicional_tem_lista_vazia_e_nao_nula()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: Postgres nao disponivel");

        var repo = new LeadsNoBanco(_ctx!);
        var lead = new Lead(Guid.NewGuid(), "+5511987654321", T0);
        repo.Adicionar(lead);
        _ctx!.SaveChanges();

        _ctx.ChangeTracker.Clear();

        var achado = repo.PorTelefone("+5511987654321");

        Assert.NotNull(achado);
        Assert.Equal(["+5511987654321"], achado.Numeros);
    }
}
