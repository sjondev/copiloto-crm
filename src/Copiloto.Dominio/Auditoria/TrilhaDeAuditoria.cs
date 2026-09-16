namespace Copiloto.Dominio.Auditoria;

/// <summary>
/// As perguntas que a trilha precisa responder num incidente (#84).
///
/// Nao e "guardar log": e conseguir dizer, no dia seguinte a uma credencial
/// vazar, QUAIS titulares aquele acesso alcancou. A diferenca entre comunicar
/// 40 pessoas e comunicar a base inteira e esta consulta.
/// </summary>
public static class TrilhaDeAuditoria
{
    /// <summary>
    /// Os titulares alcancados por um usuario dentro de uma janela — a
    /// pergunta do vazamento de credencial.
    /// </summary>
    public static IReadOnlyList<Guid> TitularesAlcancadosPor(
        IEnumerable<AcessoRegistrado> acessos, Guid usuarioId,
        DateTimeOffset de, DateTimeOffset ate) =>
        acessos
            .Where(a => a.UsuarioId == usuarioId && a.Quando >= de && a.Quando <= ate)
            .Select(a => a.LeadId)
            .Distinct()
            .ToList();

    /// <summary>
    /// Quem tocou neste titular — a pergunta que o proprio titular faz, e que
    /// a LGPD lhe da o direito de fazer.
    /// </summary>
    public static IReadOnlyList<AcessoRegistrado> QuemAcessou(
        IEnumerable<AcessoRegistrado> acessos, Guid leadId) =>
        acessos.Where(a => a.LeadId == leadId).OrderBy(a => a.Quando).ToList();

    /// <summary>
    /// Acesso por MCP acima do normal na janela.
    ///
    /// Esta superficie e diferente das outras e por isso tem consulta propria:
    /// o servidor MCP existe para um agente consumir em VOLUME, entao o padrao
    /// que denuncia abuso ali nao e "acessou algo estranho", e "acessou muita
    /// gente rapido" (#58). Numa tela, trinta titulares numa hora e um dia
    /// cheio; por MCP, pode ser uma extracao.
    /// </summary>
    public static bool VolumeAnormalPorMcp(
        IEnumerable<AcessoRegistrado> acessos, DateTimeOffset agora,
        TimeSpan janela, int limite)
    {
        var recentes = acessos
            .Where(a => a.Origem == OrigemDoAcesso.Mcp && a.Quando >= agora - janela)
            .Select(a => a.LeadId)
            .Distinct()
            .Count();

        return recentes > limite;
    }

    /// <summary>
    /// O que saiu da nossa rede na janela. E a primeira lista pedida quando uma
    /// chave de provedor vaza.
    /// </summary>
    public static IReadOnlyList<Guid> TitularesEnviadosParaModelo(
        IEnumerable<AcessoRegistrado> acessos, DateTimeOffset de, DateTimeOffset ate) =>
        acessos
            .Where(a => a.Operacao == OperacaoAuditada.EnviouParaModelo
                        && a.Quando >= de && a.Quando <= ate)
            .Select(a => a.LeadId)
            .Distinct()
            .ToList();
}
