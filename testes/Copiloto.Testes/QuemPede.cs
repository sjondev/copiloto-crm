using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Vendas;

namespace Copiloto.Testes;

/// <summary>
/// Um usuario no banco e o ClaimsPrincipal correspondente (#176).
///
/// As rotas de leitura passaram a exigir identidade: elas resolvem o usuario
/// pelo `sub` do token e aplicam o escopo da #49. Teste que chama a rota sem
/// isso recebe 404 — e o 404 e' a resposta certa, porque lead alheio e lead
/// inexistente respondem igual.
/// </summary>
public static class QuemPede
{
    public static Usuario Gestor(CopilotoDbContext ctx, string nome = "Gestor") =>
        Guardar(ctx, nome, PerfilDeAcesso.Gestor);

    public static Usuario Vendedor(CopilotoDbContext ctx, string nome = "Vendedor") =>
        Guardar(ctx, nome, PerfilDeAcesso.Vendedor);

    private static Usuario Guardar(CopilotoDbContext ctx, string nome, PerfilDeAcesso perfil)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var usuario = new Usuario(
            Guid.NewGuid(),
            nome,
            $"{nome.ToLowerInvariant()}-{Guid.NewGuid():N}@copiloto.local",
            new string('h', Usuario.TamanhoMinimoDoHash),
            perfil);

        ctx.Usuarios.Add(usuario);
        ctx.SaveChanges();

        return usuario;
    }

    /// <summary>O principal como o middleware do JWT o entrega: `sub` e papel.</summary>
    public static ClaimsPrincipal Principal(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(ClaimTypes.Role, usuario.Perfil.ToString()),
        ], "teste"));
    }
}
