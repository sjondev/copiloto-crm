using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Copiloto.Api.Persistencia;

/// <summary>
/// O contexto que o `dotnet ef` usa para gerar migration (#49).
///
/// Sem isto, a ferramenta sobe o `Program` inteiro so para descobrir o
/// DbContext — e passa a depender de tudo que a aplicacao exige para subir. Foi
/// o que aconteceu quando a auth entrou: `JWT_SEGREDO` ausente derrubava
/// `dotnet ef migrations add`, e a saida barata teria sido enfraquecer a
/// validacao do segredo para a ferramenta voltar a funcionar. Enfraquecer
/// seguranca para satisfazer uma ferramenta de build e como isso costuma
/// comecar.
///
/// Aqui a migration precisa apenas do provider e de uma cadeia de conexao, que
/// nem precisa apontar para um banco de pe: o `dotnet ef` gera SQL, nao executa.
/// </summary>
public class FabricaDeContextoParaMigrations : IDesignTimeDbContextFactory<CopilotoDbContext>
{
    public CopilotoDbContext CreateDbContext(string[] args)
    {
        var cadeia = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                     ?? "Host=localhost;Database=copiloto;Username=copiloto";

        return new CopilotoDbContext(
            new DbContextOptionsBuilder<CopilotoDbContext>().UseNpgsql(cadeia).Options);
    }
}
