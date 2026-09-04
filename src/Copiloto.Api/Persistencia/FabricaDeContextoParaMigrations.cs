using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Copiloto.Api.Persistencia;

/// <summary>
/// O contexto que o `dotnet ef` usa para gerar migration.
///
/// Sem isto, a ferramenta sobe o `Program` inteiro so para descobrir o
/// DbContext, e passa a depender de tudo que a aplicacao exige para subir.
/// </summary>
public class FabricaDeContextoParaMigrations : IDesignTimeDbContextFactory<CopilotoDbContext>
{
    public CopilotoDbContext CreateDbContext(string[] args)
    {
        var cadeia = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                     ?? "Host=localhost;Database=copiloto;Username=copiloto";

        return new CopilotoDbContext(
            new DbContextOptionsBuilder<CopilotoDbContext>()
                .UseNpgsql(cadeia, o => o.UseVector())
                .Options);
    }
}
