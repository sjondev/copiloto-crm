namespace Copiloto.Dominio.Auditoria;

/// <summary>O que foi feito com o dado de um titular.</summary>
public enum OperacaoAuditada
{
    Leu = 0,
    Exportou = 1,
    Editou = 2,
    Excluiu = 3,

    /// <summary>Mandou para analise de modelo — saiu da nossa rede.</summary>
    EnviouParaModelo = 4,
}

/// <summary>De onde partiu o acesso. Muda o que se investiga.</summary>
public enum OrigemDoAcesso
{
    Tela = 0,
    Api = 1,

    /// <summary>Servidor MCP: agente consumindo em volume (#56, #58).</summary>
    Mcp = 2,

    /// <summary>Rotina do proprio sistema, sem gente pedindo.</summary>
    Job = 3,
}

/// <summary>
/// Uma linha da trilha de auditoria: quem, qual titular, o que, quando, por
/// onde (#84).
///
/// E o registro que decide o resultado de um incidente real. Sem ele, a
/// empresa nao consegue dizer QUAIS titulares foram afetados — e ai precisa
/// comunicar todos, transformando um incidente pequeno num evento de reputacao
/// grande.
///
/// Imutavel, porque e o registro do que ja aconteceu: trilha que pode ser
/// editada nao serve para provar nada, e e a primeira coisa que um invasor
/// esperto mexeria.
/// </summary>
public class AcessoRegistrado
{
    /// <param name="usuarioId">
    /// Quem acessou. Nulo APENAS quando ninguem pediu — rotina do sistema. O
    /// tipo e anulavel de proposito: passar Guid.Empty por "nao sei" faria a
    /// trilha apontar para um usuario que nao existe, e ninguem descobriria
    /// isso ate precisar dela.
    /// </param>
    public AcessoRegistrado(
        Guid id, Guid? usuarioId, Guid leadId, OperacaoAuditada operacao,
        OrigemDoAcesso origem, DateTimeOffset quando, string? detalhe = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Acesso sem id.", nameof(id));
        if (leadId == Guid.Empty)
            throw new ArgumentException(
                "Acesso sem titular nao responde a pergunta que a trilha existe para "
                + "responder: QUEM foi afetado.", nameof(leadId));

        // Usuario vazio e permitido so quando ninguem pediu: rotina do sistema.
        // Em qualquer outra origem, acesso sem autor e trilha que nao aponta
        // para pessoa nenhuma — e ai ela nao serve nem para conter o incidente
        // nem para responder a ANPD.
        if ((usuarioId is null || usuarioId == Guid.Empty) && origem != OrigemDoAcesso.Job)
            throw new ArgumentException(
                $"Acesso de origem {origem} sem usuario: a trilha precisa dizer quem "
                + "acessou. Se foi rotina do sistema, use OrigemDoAcesso.Job.",
                nameof(usuarioId));

        Id = id;
        UsuarioId = usuarioId == Guid.Empty ? null : usuarioId;
        LeadId = leadId;
        Operacao = operacao;
        Origem = origem;
        Quando = quando;
        Detalhe = string.IsNullOrWhiteSpace(detalhe) ? null : detalhe.Trim();
    }

    public Guid Id { get; }

    /// <summary>Nulo apenas para rotina do sistema.</summary>
    public Guid? UsuarioId { get; }

    public Guid LeadId { get; }
    public OperacaoAuditada Operacao { get; }
    public OrigemDoAcesso Origem { get; }
    public DateTimeOffset Quando { get; }

    /// <summary>
    /// O que ajuda a reconstruir depois — a ferramenta MCP chamada, o campo
    /// editado, o provedor para onde foi.
    ///
    /// NAO carrega conteudo de mensagem nem valor de campo: a trilha existe
    /// para dizer o que foi tocado, e uma trilha que copia o dado pessoal vira
    /// a segunda copia a proteger, e a primeira a vazar junto.
    /// </summary>
    public string? Detalhe { get; }
}
