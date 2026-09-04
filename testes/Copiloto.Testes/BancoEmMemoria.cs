using Copiloto.Api.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Testes;

/// <summary>
/// Um banco SQLite em memoria por classe de teste (#111).
///
/// SQLite e nao Postgres porque a suite roda offline por decisao do CLAUDE.md:
/// teste que precisa de container para passar e teste que quebra no primeiro
/// clone. O que se prova com ele e o MAPEAMENTO — que o modelo fecha, que
/// chaves e indices existem e que o dominio sobrevive a ida e volta.
///
/// A conexao fica ABERTA pela vida da classe: fechada, o banco em memoria some
/// junto, e o teste passaria a criar um banco novo a cada contexto sem que
/// ninguem percebesse.
///
/// Existe porque o mesmo construtor de quatro linhas ja estava copiado em tres
/// classes — a terceira aparicao e o gatilho de DRY do CLAUDE.md. Aqui a
/// duplicacao NAO era acidental: se o setup mudar (outro provider, outra
/// versao, um `EnsureDeleted`), muda para todas ao mesmo tempo.
/// </summary>
public abstract class BancoEmMemoria : IDisposable
{
    private readonly SqliteConnection _conexao;

    protected BancoEmMemoria()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        Opcoes = new DbContextOptionsBuilder<CopilotoDbContext>().UseSqlite(_conexao).Options;

        using var ctx = new CopilotoDbContext(Opcoes);
        ctx.Database.EnsureCreated();
    }

    protected DbContextOptions<CopilotoDbContext> Opcoes { get; }

    /// <summary>
    /// Um contexto novo sobre o MESMO banco.
    ///
    /// Contexto novo e o que aproxima o teste de um processo novo: o
    /// ChangeTracker nasce vazio, entao o que voltar veio do banco, e nao da
    /// memoria do contexto que gravou.
    /// </summary>
    protected CopilotoDbContext NovoContexto() => new(Opcoes);

    public void Dispose()
    {
        _conexao.Dispose();
        GC.SuppressFinalize(this);
    }
}
