using System.ComponentModel;
using Copiloto.Api.Persistencia;
using ModelContextProtocol.Server;

namespace Copiloto.Api.Mcp;

/// <summary>
/// O CRM exposto como servidor MCP, so leitura (#56).
///
/// O CRM deixa de ser um destino e vira capacidade componivel: o gestor
/// pergunta "quais negocios travaram este mes?" de qualquer cliente MCP, sem
/// que exista tela para isso.
///
/// NADA aqui altera estado, e isso e limite desta issue e nao acaso. Escrita
/// por MCP significaria um agente movendo negocio de estagio a partir de uma
/// frase — e o produto inteiro existe para o contrario: quem decide e a pessoa.
///
/// As descricoes sao escritas PARA UM CLIENTE MCP escolher sozinho a ferramenta
/// certa. Descricao vaga aqui nao produz erro: produz o agente chamando a
/// ferramenta errada e respondendo com confianca.
/// </summary>
[McpServerToolType]
public static class FerramentasDoCrm
{
    [McpServerTool(Name = "buscar_lead")]
    [Description("Procura um lead por nome ou telefone e devolve o identificador dele. "
        + "Use PRIMEIRO quando a pergunta cita uma pessoa ou empresa pelo nome, porque as "
        + "outras ferramentas pedem o lead_id. Aceita telefone em qualquer formato "
        + "(com ou sem DDI, com ou sem nono digito). Devolve no maximo 50 resultados.")]
    public static async Task<IReadOnlyList<LeadEncontrado>> BuscarLead(
        CopilotoDbContext ctx,
        [Description("Nome (parcial) ou telefone do cliente.")] string termo,
        CancellationToken ct = default) =>
        await ConsultasDoCrm.BuscarLead(ctx, termo, ct);

    [McpServerTool(Name = "obter_ficha")]
    [Description("Traz o que a EMPRESA sabe sobre um lead: o que o vendedor anotou antes "
        + "de falar, o que ainda falta descobrir, o estagio do negocio aberto, ha quantos "
        + "dias ele nao anda e ha quantos dias o cliente nao responde. NAO e o dossie "
        + "gerado por IA e nao contem analise: sao os dados do CRM. Parte do que esta "
        + "anotado e impressao de quem atendeu, e nao fato apurado — trate como hipotese.")]
    public static async Task<FichaDoLead?> ObterFicha(
        CopilotoDbContext ctx,
        [Description("Identificador do lead, obtido em buscar_lead.")] Guid lead_id,
        CancellationToken ct = default) =>
        await ConsultasDoCrm.ObterFicha(ctx, lead_id, DateTimeOffset.UtcNow, ct);

    [McpServerTool(Name = "listar_negocios_parados")]
    [Description("Lista negocios ABERTOS que estao no mesmo estagio ha pelo menos N dias, "
        + "do mais parado para o menos, com o nome do cliente e ha quantos dias parou. "
        + "Use para perguntas como 'o que travou' ou 'o que precisa de atencao'. Conta o "
        + "tempo desde a ultima mudanca de estagio, e nao desde a abertura: negocio de "
        + "dois meses que andou ontem nao esta parado. Devolve no maximo 50.")]
    public static async Task<IReadOnlyList<NegocioParado>> ListarNegociosParados(
        CopilotoDbContext ctx,
        [Description("Minimo de dias sem mudar de estagio. Ex.: 10.")] int dias,
        CancellationToken ct = default) =>
        await ConsultasDoCrm.ListarNegociosParados(ctx, dias, DateTimeOffset.UtcNow, ct);

    [McpServerTool(Name = "historico_conversa")]
    [Description("Devolve as ultimas falas trocadas com o cliente, em ordem cronologica, "
        + "com quem falou e quando. Use quando a pergunta depende do que foi DITO — "
        + "objecao, combinado, preco citado. O limite e cortado em 50 mesmo que voce peca "
        + "mais: conversa inteira em contexto e token gasto antes de alguem precisar.")]
    public static async Task<IReadOnlyList<FalaDaConversa>> HistoricoConversa(
        CopilotoDbContext ctx,
        [Description("Identificador do lead, obtido em buscar_lead.")] Guid lead_id,
        [Description("Quantas falas trazer, das mais recentes. Padrao 20, teto 50.")] int limite = 20,
        CancellationToken ct = default) =>
        await ConsultasDoCrm.HistoricoDaConversa(ctx, lead_id, limite, ct);
}
