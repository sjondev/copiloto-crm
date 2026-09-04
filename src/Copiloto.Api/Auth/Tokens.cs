using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Copiloto.Dominio.Vendas;
using Microsoft.IdentityModel.Tokens;

namespace Copiloto.Api.Auth;

/// <summary>
/// Emite e valida o token de sessao (#49).
///
/// O segredo vem de variavel de ambiente, sempre. Segredo em appsettings e
/// segredo no repositorio, e a varredura da esteira (#47) existe justamente
/// para impedir que ele chegue la — mas a defesa de verdade e nao ter um
/// caminho em que isso funcione.
/// </summary>
public class Tokens
{
    /// <summary>
    /// Tamanho minimo do segredo. HMAC-SHA256 usa chave de 256 bits, e um
    /// segredo curto e adivinhavel torna a assinatura decorativa: qualquer um
    /// forja um token de gestor.
    /// </summary>
    public const int TamanhoMinimoDoSegredo = 32;

    /// <summary>
    /// Oito horas: um turno. Token que nao expira e credencial permanente
    /// entregue ao navegador; token de quinze minutos, sem renovacao, e um
    /// vendedor relogando no meio do atendimento — e ai alguem "resolve" isso
    /// aumentando para um ano.
    /// </summary>
    public static readonly TimeSpan Validade = TimeSpan.FromHours(8);

    public const string Emissor = "copiloto";

    private readonly SymmetricSecurityKey _chave;

    public Tokens(string segredo)
    {
        if (string.IsNullOrWhiteSpace(segredo) || segredo.Length < TamanhoMinimoDoSegredo)
            throw new ArgumentException(
                $"JWT_SEGREDO ausente ou com menos de {TamanhoMinimoDoSegredo} caracteres. "
                + "Sem segredo forte a assinatura nao protege nada — qualquer um forja um "
                + "token de gestor. Defina a variavel de ambiente antes de subir.",
                nameof(segredo));

        _chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(segredo));
    }

    public string Emitir(Usuario usuario, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        // O token leva id, perfil e nome — e NAO leva o email nem o hash: ele
        // trafega em header e fica no navegador, entao carrega o minimo que o
        // servidor precisa para decidir.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(ClaimTypes.Role, usuario.Perfil.ToString()),
            new(ClaimTypes.Name, usuario.Nome),
        };

        var token = new JwtSecurityToken(
            issuer: Emissor,
            audience: Emissor,
            claims: claims,
            notBefore: agora.UtcDateTime,
            expires: (agora + Validade).UtcDateTime,
            signingCredentials: new SigningCredentials(_chave, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Os parametros que a Api usa para validar o que chega.</summary>
    public TokenValidationParameters Validacao() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Emissor,
        ValidateAudience = true,
        ValidAudience = Emissor,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _chave,
        ValidateLifetime = true,

        // Sem tolerancia de relogio: o padrao da biblioteca e cinco minutos, e
        // "expira em oito horas" com cinco minutos de bonus silencioso e uma
        // regra que ninguem escreveu. Se um dia houver deriva de relogio entre
        // maquinas, isso vira decisao explicita, com numero.
        ClockSkew = TimeSpan.Zero,
    };

    /// <summary>Quem esta no token, ou null se ele nao vale.</summary>
    public (Guid UsuarioId, PerfilDeAcesso Perfil)? Ler(string token)
    {
        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, Validacao(), out _);

            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var papel = principal.FindFirst(ClaimTypes.Role)?.Value;

            if (!Guid.TryParse(sub, out var id)) return null;
            if (!Enum.TryParse<PerfilDeAcesso>(papel, out var perfil)) return null;

            return (id, perfil);
        }
        catch (SecurityTokenException)
        {
            // Expirado, assinatura errada, emissor errado: para quem chama, e
            // tudo a mesma coisa — nao autenticado.
            return null;
        }
    }
}
