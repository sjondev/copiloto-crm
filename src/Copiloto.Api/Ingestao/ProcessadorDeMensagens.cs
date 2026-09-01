using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Vendas;

namespace Copiloto.Api.Ingestao;

/// <summary>
/// O worker que consome a fila fora do webhook (#40).
///
/// Processar IA dentro do handler e o erro classico: provedor lento vira
/// timeout na origem, timeout vira reentrega, reentrega vira custo duplicado —
/// e o custo duplicado e' de dinheiro, nao de CPU.
///
/// `MODEL_PROVIDER=fake` e' o padrao, entao isto roda offline e de graca ate' o
/// provedor de verdade entrar.
/// </summary>
public class ProcessadorDeMensagens : BackgroundService
{
    private readonly FilaDeMensagens _fila;
    private readonly ResolvedorDeLead _resolvedor;
    private readonly ILogger<ProcessadorDeMensagens> _log;

    public ProcessadorDeMensagens(
        FilaDeMensagens fila, ResolvedorDeLead resolvedor, ILogger<ProcessadorDeMensagens> log)
    {
        _fila = fila;
        _resolvedor = resolvedor;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // O token NAO e' repassado ao Ler: no desligamento a fila para de
        // aceitar e o laco termina sozinho ao esvaziar. Passar o token aqui
        // abortaria no meio e descartaria o que ja estava dentro, que e' o
        // oposto de drenar.
        try
        {
            await foreach (var mensagem in _fila.Ler(CancellationToken.None))
            {
                await Processar(mensagem);
            }
        }
        catch (OperationCanceledException)
        {
            // Desligamento estourou o prazo do host. O que sobrou se perde, e a
            // durabilidade e' assunto da #69.
        }
    }

    private Task Processar(MensagemRecebida bruta)
    {
        var doCliente = _resolvedor.TelefoneDoCliente(bruta);
        if (doCliente is null)
        {
            // Numero irreconhecivel nao derruba o worker nem some: fica no log
            // com o id do provedor, que e por onde alguem consegue ir atras.
            _log.LogWarning(
                "Mensagem {Id} descartada: nem De ({De}) nem Para ({Para}) e telefone valido",
                bruta.ProviderMessageId, bruta.De, bruta.Para);
            return Task.CompletedTask;
        }

        var lead = _resolvedor.Resolver(doCliente, bruta.EnviadaEm);
        var remetente = Telefone.Normalizar(bruta.De)!;
        var autor = _resolvedor.QuemFalou(remetente);

        // O passo de IA entra aqui (#41 em diante). Ate la, o registro prova que
        // a mensagem atravessou a fila sem passar pelo handler do webhook.
        _log.LogInformation(
            "Mensagem {Id} de {Autor} no lead {Lead} processada fora do webhook",
            bruta.ProviderMessageId, autor, lead.Id);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Ordem importa: fechar para escrita ANTES de esperar. Ao contrario, o
        // laco ficaria esperando trabalho que nunca chega e o desligamento
        // dependeria do timeout do host.
        _fila.PararDeAceitar();
        await base.StopAsync(cancellationToken);
    }
}
