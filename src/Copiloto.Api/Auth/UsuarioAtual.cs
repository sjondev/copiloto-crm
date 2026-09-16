using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Api.Auth;

/// <summary>
/// Quem esta pedindo (#176).
///
/// O token carrega id e perfil, e daria para decidir escopo so com isso. Le do
/// BANCO mesmo assim: perfil rebaixado ou usuario removido valeriam apenas no
/// proximo login, e "tirei o acesso dele" precisa valer agora — token de oito
/// horas e oito horas de acesso que alguem achou que tinha cortado.
///
/// O custo e uma consulta por requisicao, pela chave primaria. Se um dia
/// aparecer no p99 (#54), o lugar de resolver e um cache curto com invalidacao
/// no logout, e nao confiar no claim.
/// </summary>
public static class UsuarioAtual
{
    public static async Task<Usuario?> De(
        ClaimsPrincipal? quem, CopilotoDbContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var sub = quem?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? quem?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(sub, out var id)) return null;

        return await ctx.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
    }
}
