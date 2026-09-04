using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Fichas;
using Copiloto.Dominio.Ia;
using Copiloto.Dominio.Rag;
using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Api.Persistencia;

/// <summary>
/// O contexto, e ele mora na Api (#103).
///
/// O `Copiloto.Dominio` segue sem PackageReference nenhum: sem pacote ele nao
/// consegue compilar um `[Table]` nem um `DbContext`, e e essa impossibilidade
/// que sustenta o dominio POCO (#48) — regra que depende de disciplina apodrece,
/// regra que nao compila, nao.
///
/// Por isso o mapeamento e por `IEntityTypeConfiguration`, e nao por atributo:
/// atributo obrigaria a anotar a entidade, que e exatamente o que o dominio nao
/// pode fazer.
/// </summary>
public class CopilotoDbContext : DbContext
{
    public CopilotoDbContext(DbContextOptions<CopilotoDbContext> opcoes) : base(opcoes) { }

    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<AiInvocation> Invocacoes => Set<AiInvocation>();
    public DbSet<Conversa> Conversas => Set<Conversa>();
    public DbSet<Mensagem> Mensagens => Set<Mensagem>();
    public DbSet<FichaCliente> Fichas => Set<FichaCliente>();

    /// <summary>Trechos de conversa vetorizados, para recuperar por semelhanca (#60).</summary>
    public DbSet<Precedente> Precedentes => Set<Precedente>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(CopilotoDbContext).Assembly);

        // `vector` e tipo de coluna que so o Postgres tem, e o modelo inteiro e
        // conferido em SQLite na memoria (#103) para a suite rodar offline. As
        // duas coisas so convivem se o Precedente sair do modelo fora do
        // Postgres — senao a validacao do EF derruba TODO teste de mapeamento,
        // inclusive os que nao tem nada a ver com RAG.
        //
        // Quem confere o Precedente e o teste contra Postgres real (#60), e a
        // ausencia aqui e afirmada por teste: exclusao que ninguem verifica
        // vira cobertura que sumiu sem aviso.
        if (!Database.IsNpgsql())
        {
            b.Ignore<Precedente>();
            return;
        }

        // A extensao entra pela MIGRATION, e nao por um comando solto: banco
        // novo sem `vector` habilitado quebra na primeira consulta, e o erro
        // aparece longe de quem esqueceu de rodar o comando (#60).
        b.HasPostgresExtension("vector");
    }
}
