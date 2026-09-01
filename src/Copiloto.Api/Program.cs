using Copiloto.Api.Ia;
using Copiloto.Api.Ingestao;
using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Ia;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Postgres em producao. A cadeia de configuracao segue a do compose, e a
// ausencia da senha derruba a subida de proposito — banco sem senha e o tipo de
// "funciona na minha maquina" que vira incidente.
builder.Services.AddDbContext<CopilotoDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
                ?? "Host=localhost;Database=copiloto;Username=copiloto"));

builder.Services.AddScoped<IRepositorioDeLeads, LeadsNoBanco>();

// O router e a tabela dele: a tabela vem do appsettings, nunca de codigo.
builder.Services.AddSingleton(_ => new RoteadorDeModelo(
    TabelaDeModelos.Carregar(builder.Configuration)));

builder.Services.AddSingleton<FilaDeMensagens>();

// O numero da empresa e o que decide quem falou em cada mensagem, entao ele e
// configuracao e nao constante: cada instalacao tem o seu.
builder.Services.AddSingleton(_ => new ResolvedorDeLead(
    builder.Configuration["WHATSAPP_NUMERO_EMPRESA"] ?? "+55 11 3333-4444"));
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
    if (string.IsNullOrWhiteSpace(mensagem.De) || string.IsNullOrWhiteSpace(mensagem.Para))
        return Results.BadRequest(new { erro = "sem De/Para: nao ha como dizer quem falou" });

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
