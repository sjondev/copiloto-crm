using System.Text.Json;
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

// A fonte de conversa e escolha de configuracao, nao de codigo (#17): o
// nucleo daqui para dentro so conhece MensagemRecebida.
builder.Services.AddSingleton(_ => FonteDeConversa.Escolher(builder.Configuration));

// O provedor de modelo segue a mesma regra (#27), e o padrao e o fake: a suite
// e a demo rodam offline e de graca, e o primeiro clone nao gasta dinheiro.
builder.Services.AddSingleton(_ => ProvedorDeModelo.Escolher(
    builder.Configuration, builder.Environment.ContentRootPath));

// A cascata amarra router e provedor (#30). Ela nao levanta excecao quando se
// esgota: erro na tela no meio de uma venda e pior que dado desatualizado.
builder.Services.AddSingleton<CascataDeModelos>();

// O numero da empresa e o que decide quem falou em cada mensagem, entao ele e
// configuracao e nao constante: cada instalacao tem o seu.
builder.Services.AddSingleton(_ => new ResolvedorDeLead(
    builder.Configuration["WHATSAPP_NUMERO_EMPRESA"] ?? "+55 11 3333-4444"));
builder.Services.AddHostedService<ProcessadorDeMensagens>();

var app = builder.Build();

app.MapGet("/saude", () => Results.Ok(new { ok = true }));

// O webhook responde na hora e nao processa nada (#40). O 202 e' deliberado: 200
// diria "processado", e o que aconteceu foi "recebido e enfileirado".
//
// O corpo chega CRU e quem o entende e a fonte (#17). O handler nao sabe se
// atras dele esta a Cloud API, o WAHA ou o seed — e e' isso que permite trocar
// de provedor mudando uma variavel de ambiente.
app.MapPost("/webhook/whatsapp", async (
    HttpRequest requisicao, IConversationSource fonte, FilaDeMensagens fila, CancellationToken ct) =>
{
    using var leitor = new StreamReader(requisicao.Body);
    var corpo = await leitor.ReadToEndAsync(ct);

    IReadOnlyList<MensagemRecebida> falas;
    try
    {
        falas = fonte.Traduzir(corpo);
    }
    catch (JsonException e)
    {
        return Results.BadRequest(new { erro = $"a fonte '{fonte.Nome}' nao entendeu o payload: {e.Message}" });
    }

    if (falas.FirstOrDefault(f => f.PorQueNaoEntra() is not null) is { } incompleta)
        return Results.BadRequest(new { erro = incompleta.PorQueNaoEntra() });

    // Lote vazio responde 202 e nao 400: confirmacao de leitura e mudanca de
    // status sao a maior parte do trafego real, e nao ha nada de errado nelas.
    foreach (var fala in falas)
    {
        // 503 e nao 500: o provedor deve REENTREGAR. Dizer 200 com a fila cheia
        // perderia a fala do cliente em silencio, que e' o pior desfecho possivel.
        if (!await fila.Publicar(fala, ct))
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Accepted();
});

app.Run();

/// <summary>Torna a classe gerada visivel para o WebApplicationFactory da suite.</summary>
public partial class Program;
