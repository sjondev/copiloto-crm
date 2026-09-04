using System.Text.Json;
using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Fichas;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Api.Titulares;

/// <summary>Uma linha da ficha, como ela sai para o titular.</summary>
public record AnotacaoExportada(string Campo, string Valor, string Natureza, string? Fonte);

/// <summary>Uma fala, como ela sai para o titular.</summary>
public record MensagemExportada(string Autor, string Texto, DateTimeOffset EnviadaEm);

/// <summary>Um negocio, e o que o sistema concluiu dentro dele.</summary>
public record NegocioExportado(Guid Id, string Estagio, DateTimeOffset AbertoEm);

/// <summary>
/// Tudo que o sistema tem sobre um titular, no formato que ele leva embora.
/// </summary>
public record DadosDoTitular(
    Guid LeadId,
    string Telefone,
    string? Nome,
    DateTimeOffset CriadoEm,
    bool AnaliseDeIaSuspensa,
    IReadOnlyList<AnotacaoExportada> FichaDoCliente,
    IReadOnlyList<MensagemExportada> Conversas,
    IReadOnlyList<NegocioExportado> Negocios,
    IReadOnlyList<string> CompartilhadoCom,
    IReadOnlyList<string> Observacoes);

/// <summary>
/// Confirmacao, acesso e portabilidade num lugar so (#81).
///
/// O criterio dificil da issue e o primeiro, e ele nao e tecnico: o titular tem
/// direito de saber que o sistema o classificou como "sensivel a preco" ou
/// "esfriando". Isso e dado pessoal SOBRE ELE, gerado por nos, e ele pode
/// contestar — protege-se o que entrou e esquece-se o que o sistema produziu.
///
/// Por isso a exportacao inclui a ficha com natureza e procedencia (#88): ver
/// "anotaram uma IMPRESSAO de que eu pareco desconfiado" e outra coisa de ver
/// uma lista de campos.
/// </summary>
public class ExportacaoDoTitular
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly CopilotoDbContext _ctx;
    private readonly IReadOnlyList<string> _suboperadores;

    /// <param name="suboperadores">
    /// Com quem o dado foi compartilhado (art. 18, VII). Vem de CONFIGURACAO e
    /// nao de constante: a lista muda quando alguem troca o provedor de modelo,
    /// e uma resposta ao titular que envelhece em silencio e pior que nenhuma.
    /// </param>
    public ExportacaoDoTitular(CopilotoDbContext ctx, IReadOnlyList<string> suboperadores)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        _ctx = ctx;
        _suboperadores = suboperadores;
    }

    /// <summary>Sim ou nao: existe tratamento de dado deste titular?</summary>
    public Task<bool> Confirmar(Guid leadId, CancellationToken ct) =>
        _ctx.Leads.AnyAsync(l => l.Id == leadId, ct);

    public async Task<DadosDoTitular?> Exportar(Guid leadId, CancellationToken ct)
    {
        var lead = await _ctx.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leadId, ct);
        if (lead is null) return null;

        var ficha = await _ctx.Fichas.AsNoTracking().FirstOrDefaultAsync(f => f.LeadId == leadId, ct);
        var conversas = await _ctx.Conversas.AsNoTracking()
            .Include(c => c.Mensagens)
            .Where(c => c.LeadId == leadId)
            .ToListAsync(ct);
        var negocios = await _ctx.Deals.AsNoTracking()
            .Where(d => d.LeadId == leadId)
            .ToListAsync(ct);

        var anotacoes = (ficha?.Preenchidos ?? new Dictionary<string, Anotacao>())
            .Select(p => new AnotacaoExportada(
                p.Key, p.Value.Valor,
                p.Value.EhFato ? "fato" : "impressão do vendedor",
                p.Value.Fonte))
            .ToList();

        var mensagens = conversas
            .SelectMany(c => c.Mensagens)
            .OrderBy(m => m.EnviadaEm)
            .Select(m => new MensagemExportada(m.Autor.ToString(), m.Texto, m.EnviadaEm))
            .ToList();

        return new DadosDoTitular(
            lead.Id, lead.Telefone, lead.Nome, lead.CriadoEm,
            lead.AnaliseDeIaSuspensa,
            anotacoes,
            mensagens,
            negocios.Select(d => new NegocioExportado(d.Id, d.Estagio.ToString(), d.AbertoEm)).ToList(),
            _suboperadores,
            Observacoes(anotacoes));
    }

    /// <summary>
    /// JSON identado — "formato estruturado e de uso comum" do art. 18, V.
    ///
    /// Identado de proposito: portabilidade que sai como uma linha de 40 mil
    /// caracteres atende a letra e nao serve ao titular, que e uma pessoa
    /// abrindo um arquivo.
    /// </summary>
    public async Task<string?> ExportarComoJson(Guid leadId, CancellationToken ct)
    {
        var dados = await Exportar(leadId, ct);
        return dados is null ? null : JsonSerializer.Serialize(dados, Json);
    }

    /// <summary>
    /// O que a exportacao precisa DIZER, alem do que ela mostra.
    ///
    /// Arquivo que so lista campos deixa o titular concluir que aquilo e tudo, e
    /// duas coisas aqui nao sao obvias: impressao nao e fato apurado, e o
    /// dossie nao esta no arquivo porque ele e recalculado, nao guardado.
    /// </summary>
    private static IReadOnlyList<string> Observacoes(IReadOnlyList<AnotacaoExportada> anotacoes)
    {
        var notas = new List<string>
        {
            "As inferências do sistema (estágio, objeção, \"esfriando\") são recalculadas "
            + "a partir das conversas acima e não ficam guardadas: o que as origina está "
            + "neste arquivo, e você pode contestar tanto o dado quanto a conclusão.",
        };

        if (anotacoes.Any(a => a.Natureza.StartsWith("impressão")))
        {
            notas.Add(
                "Algumas linhas da ficha são IMPRESSÕES de quem atendeu, e não fatos "
                + "apurados. Elas estão marcadas como tal, e você pode pedir correção "
                + "ou remoção delas.");
        }

        return notas;
    }
}
