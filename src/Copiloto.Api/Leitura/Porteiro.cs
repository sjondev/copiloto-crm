using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Acesso;
using Copiloto.Dominio.Auditoria;
using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Api.Leitura;

/// <summary>
/// Quem pode abrir a conversa de quem, e o que fica registrado quando abre
/// (#176).
///
/// A regra ja existia desde a #49, no `EscopoDeLeitura` do dominio, e nao era
/// chamada de lugar nenhum — o mesmo destino do `RequireAuthorization` que a
/// #182 encontrou. Aqui ela passa a valer.
/// </summary>
public static class Porteiro
{
    /// <summary>
    /// Por que o acesso foi negado, ou <c>null</c> quando pode passar.
    ///
    /// Devolve NOT FOUND, e nao "proibido", para lead de outro vendedor. A
    /// diferenca importa: "voce nao pode ver o lead 123" confirma que o 123
    /// existe, e quem varre ids aprende a carteira do colega sem ler uma
    /// conversa. Lead inexistente e lead alheio respondem igual.
    /// </summary>
    public static async Task<IResult?> Barrar(
        Guid leadId, Usuario? usuario, CopilotoDbContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var naoAchou = Results.NotFound(new { erro = "lead nao encontrado" });

        // Sem usuario com o token valido: o token e de alguem que nao existe
        // mais, ou foi removido no meio da sessao.
        if (usuario is null) return naoAchou;

        var lead = await ctx.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leadId, ct);
        if (lead is null) return naoAchou;

        if (!EscopoDeLeitura.PodeVer(usuario, lead)) return naoAchou;

        await RegistrarSeForAlheio(lead, usuario, ctx, ct);

        return null;
    }

    /// <summary>
    /// A trilha (#84) so registra o acesso ALHEIO.
    ///
    /// Gravar o vendedor abrindo o proprio lead encheria a tabela com a
    /// operacao normal do dia e afogaria o que a trilha existe para achar: o
    /// gestor que leu a conversa de um cliente que nao e da carteira dele.
    /// Trilha que registra tudo e trilha que ninguem consulta.
    /// </summary>
    private static async Task RegistrarSeForAlheio(
        Lead lead, Usuario usuario, CopilotoDbContext ctx, CancellationToken ct)
    {
        if (lead.VendedorId == usuario.Id) return;

        // Lead sem dono tambem nao entra: ele esta na fila comum de proposito
        // (#49), e abrir a fila de entrada e o trabalho de todo mundo.
        if (lead.VendedorId is null) return;

        ctx.Acessos.Add(new AcessoRegistrado(
            Guid.NewGuid(),
            usuario.Id,
            lead.Id,
            OperacaoAuditada.Leu,
            OrigemDoAcesso.Tela,
            DateTimeOffset.UtcNow,
            detalhe: usuario.EhGestor
                ? "gestor leu conversa de lead de outro vendedor"
                : "leitura de lead de outro vendedor"));

        await ctx.SaveChangesAsync(ct);
    }
}
