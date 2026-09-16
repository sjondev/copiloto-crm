using Microsoft.AspNetCore.SignalR;

namespace Copiloto.Api.TempoReal;

/// <summary>
/// O canal que empurra a leitura para a tela (#50).
///
/// Sugestao que exige F5 nao e usada: o vendedor esta lendo a conversa, nao
/// vigiando um botao de atualizar. E numa demo, o dossie aparecendo sozinho
/// enquanto a conversa avanca e o momento que prende a atencao.
///
/// Por GRUPO e nao por broadcast. Cada vendedor acompanha os leads que esta
/// atendendo; mandar todo dossie para todo mundo vazaria conversa de um cliente
/// para quem nao a atende — e sob a LGPD isso nao e detalhe de performance.
/// </summary>
public class DossieHub : Hub
{
    public const string Rota = "/tempo-real/dossie";

    /// <summary>Evento de leitura pronta. O payload e o mesmo DTO do GET.</summary>
    public const string DossieAtualizado = "dossieAtualizado";

    /// <summary>
    /// Avisa que a releitura comecou.
    ///
    /// Existe porque o intervalo entre a fala chegar e o dossie ficar pronto e
    /// visivel a olho nu, e tela parada nesse intervalo parece tela quebrada —
    /// o vendedor recarrega, nao acontece nada, e ele conclui que a ferramenta
    /// nao funciona.
    /// </summary>
    public const string Analisando = "analisando";

    public static string Grupo(Guid leadId) => $"lead:{leadId}";

    public Task Acompanhar(Guid leadId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, Grupo(leadId));

    public Task Largar(Guid leadId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupo(leadId));
}
