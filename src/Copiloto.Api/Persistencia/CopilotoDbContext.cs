using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Fichas;
using Copiloto.Dominio.Ia;
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

    /// <summary>Quem entra no CRM, com perfil e hash de senha (#49).</summary>
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder b) =>
        b.ApplyConfigurationsFromAssembly(typeof(CopilotoDbContext).Assembly);
}
