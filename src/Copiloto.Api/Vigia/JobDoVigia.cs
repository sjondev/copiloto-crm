using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Vigia;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Api.Vigia;

/// <summary>
/// O Vigia rodando sozinho, fora do ciclo request/response (#53).
///
/// Nenhum vendedor pede este trabalho: negocio esquecido nao gera evento, nao
/// abre tela e nao chega por webhook — ele so fica parado. Por isso o gatilho e
/// o relogio, e nao uma requisicao.
/// </summary>
public class JobDoVigia : BackgroundService
{
    /// <summary>
    /// De hora em hora. A varredura e de datas, entao o custo e de banco e nao
    /// de modelo — mas rodar de minuto em minuto encheria o log com o mesmo
    /// alerta que ja foi dado, e a dedupe nao existe para justificar polling
    /// desnecessario.
    /// </summary>
    public static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    /// <summary>
    /// O que ja foi avisado, nesta instancia.
    ///
    /// Em memoria, e isso e LIMITACAO DECLARADA: com duas replicas o vendedor
    /// recebe o mesmo alerta duas vezes, e um restart faz a lista inteira ser
    /// reavisada. O lugar certo e o estado compartilhado (#66/#67); enquanto ele
    /// nao existe, o conjunto fica aqui e o comentario fica junto — premissa
    /// nao declarada e o que quebra no primeiro deploy com replica.
    /// </summary>
    private readonly HashSet<string> _jaAvisados = new();

    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<JobDoVigia> _log;

    public JobDoVigia(IServiceScopeFactory escopos, ILogger<JobDoVigia> log)
    {
        _escopos = escopos;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var relogio = new PeriodicTimer(Intervalo);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var escopo = _escopos.CreateScope();
                var ctx = escopo.ServiceProvider.GetRequiredService<CopilotoDbContext>();

                foreach (var alerta in await Varrer(ctx, DateTimeOffset.UtcNow, ct))
                {
                    // O alerta vai para o log ate a tela existir (#50): o valor
                    // ja e verificavel, e prender a varredura ao SignalR faria
                    // uma issue depender da outra sem motivo.
                    _log.LogInformation(
                        "Vigia · {Motivo} no deal {Deal}: {Texto}",
                        alerta.Motivo, alerta.DealId, alerta.Texto);
                }
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                // Job que morre no primeiro erro para de vigiar em silencio, que
                // e o mesmo desfecho de nao existir.
                _log.LogError(erro, "Vigia falhou nesta passagem; tenta de novo em {Intervalo}", Intervalo);
            }

            try
            {
                await relogio.WaitForNextTickAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Uma passagem da varredura. Publico e sem timer para o teste conseguir
    /// chamar sem subir a aplicacao nem esperar uma hora.
    /// </summary>
    public async Task<IReadOnlyList<Alerta>> Varrer(
        CopilotoDbContext ctx, DateTimeOffset agora, CancellationToken ct)
    {
        // Deals fechados nao entram na consulta, e nao so no filtro do dominio:
        // e a diferenca entre varrer o funil ativo e varrer o historico inteiro
        // da empresa toda hora.
        var abertos = await ctx.Deals
            .AsNoTracking()
            .Where(d => d.FechadoEm == null)
            .ToListAsync(ct);

        if (abertos.Count == 0) return [];

        var leads = abertos.Select(d => d.LeadId).ToList();
        var conversas = await ctx.Conversas
            .AsNoTracking()
            .Include(c => c.Mensagens)
            .Where(c => leads.Contains(c.LeadId))
            .ToListAsync(ct);

        var achados = abertos.SelectMany(deal => Dominio.Vigia.Vigia.Varrer(
            deal, conversas.FirstOrDefault(c => c.LeadId == deal.LeadId), agora));

        return Dominio.Vigia.Vigia.Novos(achados, _jaAvisados).ToList();
    }
}
