using Copiloto.Api.Ingestao;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<FilaDeMensagens>();
builder.Services.AddHostedService<ProcessadorDeMensagens>();

var app = builder.Build();

app.MapGet("/saude", () => Results.Ok(new { ok = true }));

// O webhook responde na hora e nao processa nada (#40). O 202 e' deliberado: 200
// diria "processado", e o que aconteceu foi "recebido e enfileirado".
app.MapPost("/webhook/whatsapp", async (
    MensagemRecebida mensagem, FilaDeMensagens fila, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(mensagem.ProviderMessageId))
        return Results.BadRequest(new { erro = "sem ProviderMessageId: a reentrega nao teria como ser reconhecida" });

    var enfileirou = await fila.Publicar(mensagem, ct);

    // 503 e nao 500: o provedor deve REENTREGAR. Dizer 200 com a fila cheia
    // perderia a fala do cliente em silencio, que e' o pior desfecho possivel.
    return enfileirou
        ? Results.Accepted()
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

app.Run();

/// <summary>Torna a classe gerada visivel para o WebApplicationFactory da suite.</summary>
public partial class Program;
