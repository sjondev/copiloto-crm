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
    private readonly ILogger<ProcessadorDeMensagens> _log;

    public ProcessadorDeMensagens(FilaDeMensagens fila, ILogger<ProcessadorDeMensagens> log)
    {
        _fila = fila;
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

    private Task Processar(MensagemRecebida mensagem)
    {
        // O passo de IA entra aqui (#41 em diante). Por ora, o registro prova
        // que a mensagem atravessou a fila sem passar pelo handler do webhook.
        _log.LogInformation(
            "Mensagem {Id} de {Telefone} processada fora do webhook",
            mensagem.ProviderMessageId, mensagem.Telefone);
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
