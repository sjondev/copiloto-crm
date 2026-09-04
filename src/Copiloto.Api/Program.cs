using System.Text.Json;
using Copiloto.Api.Auth;
using Copiloto.Api.Ia;
using Copiloto.Api.Infra;
using Copiloto.Api.Ingestao;
using Copiloto.Api.Leitura;
using Copiloto.Api.TempoReal;
using Copiloto.Api.Mcp;
using Copiloto.Api.Persistencia;
using Copiloto.Api.Vigia;
using Copiloto.Dominio.Ia;
using Copiloto.Dominio.Vendas;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

builder.Services.AddSignalR();

// Estado e fila vem da variavel de ambiente, com `inmemory` como padrao (#66).
// Sem .env, sem Redis e sem RabbitMQ, a aplicacao sobe inteira.
builder.Services.AddSingleton<IQueue<MensagemRecebida>>(
    _ => Backends.Fila<MensagemRecebida>(builder.Configuration));
// O estado distribuido entra embrulhado na degradacao (#72): Redis fora nao
// pode derrubar o atendimento, mas cair para memoria em silencio esconderia que
// idempotencia, rate limit e circuito passaram a valer so nesta instancia.
builder.Services.AddSingleton<IDistributedState>(sp => new EstadoComDegradacao(
    Backends.Estado(builder.Configuration),
    sp.GetRequiredService<ILogger<EstadoComDegradacao>>()));

// A fonte de conversa e escolha de configuracao, nao de codigo (#17): o
// nucleo daqui para dentro so conhece MensagemRecebida.
builder.Services.AddSingleton(_ => FonteDeConversa.Escolher(builder.Configuration));

// O provedor de modelo segue a mesma regra (#27), e o padrao e o fake: a suite
// e a demo rodam offline e de graca, e o primeiro clone nao gasta dinheiro.
builder.Services.AddSingleton(_ => ProvedorDeModelo.Escolher(
    builder.Configuration, builder.Environment.ContentRootPath));

// O orcamento de contexto (#31) tambem vem de fora: o teto muda quando o
// modelo muda, e quem opera aperta o gasto sem esperar deploy.
builder.Services.AddSingleton(_ => new MontadorDeContexto(
    OrcamentoDeContextoConfig.Carregar(builder.Configuration)));

// A cascata amarra router e provedor (#30). Ela nao levanta excecao quando se
// esgota: erro na tela no meio de uma venda e pior que dado desatualizado.
builder.Services.AddSingleton<CascataDeModelos>();

// O triador A0 (#37). O custo vem da tabela: e o modelo de LEITURA que ele
// evita acordar, entao e o preco dele que entra na conta da economia.
builder.Services.AddSingleton(sp =>
{
    var escolha = sp.GetRequiredService<RoteadorDeModelo>().Escolher(Tarefa.Leitura);
    var tabela = TabelaDeModelos.Carregar(builder.Configuration);
    var custo = tabela.FirstOrDefault(m => m.Nome == escolha?.Modelo)?.CustoPorMilTokens ?? 0m;

    return new ContadorDeTriagem(custo);
});

builder.Services.AddSingleton<Triador>();

// O agente A1 (#13). A camada C0 vem de ARQUIVO e nao de string em codigo:
// ajustar o que o agente sabe e a operacao mais frequente depois que o produto
// esta no ar, e em codigo cada ajuste vira deploy.
builder.Services.AddSingleton(sp => new AgenteDeLeitura(
    sp.GetRequiredService<CascataDeModelos>(),
    sp.GetRequiredService<MontadorDeContexto>(),
    File.ReadAllText(Path.Combine(
        builder.Configuration["PROMPTS_DIR"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", "prompts"),
        "a1-leitura.md")),
    sp.GetRequiredService<ILogger<AgenteDeLeitura>>()));
// O circuito por provedor tambem vive no estado compartilhado (#68): com estado
// local, tres replicas sao tres circuitos e o provedor caido leva 3N chamadas.
builder.Services.AddSingleton(sp => new CircuitoDoProvedor(
    sp.GetRequiredService<IDistributedState>()));

// Rate limit e cache de analise dividem o mesmo estado compartilhado (#71): com
// contador local, o limite viraria limite vezes o numero de replicas.
builder.Services.AddSingleton(sp => new LimitadorDeTaxa(
    sp.GetRequiredService<IDistributedState>(),
    int.TryParse(builder.Configuration["RATE_LIMIT_POR_USUARIO"], out var teto) ? teto : 60,
    TimeSpan.FromMinutes(1)));

builder.Services.AddSingleton(sp => new CacheDeAnalise(
    sp.GetRequiredService<IDistributedState>(),
    double.TryParse(builder.Configuration["CACHE_ANALISE_HORAS"], out var validade)
        ? TimeSpan.FromHours(validade)
        : null));

// A janela de dedupe e configuravel porque o prazo real de reentrega do webhook
// nao foi verificado na fonte (#67): conferir muda a variavel, nao o codigo.
builder.Services.AddSingleton(sp => new GuardaDeReentrega(
    sp.GetRequiredService<IDistributedState>(),
    double.TryParse(builder.Configuration["IDEMPOTENCIA_JANELA_HORAS"], out var horas)
        ? TimeSpan.FromHours(horas)
        : GuardaDeReentrega.JanelaPadrao));

// O numero da empresa e o que decide quem falou em cada mensagem, entao ele e
// configuracao e nao constante: cada instalacao tem o seu.
//
// Scoped e nao Singleton (#158): o resolvedor grava Lead, e gravar exige o
// DbContext do escopo. Como singleton ele caia no LeadsEmMemoria do construtor,
// e todo Lead criado sumia no restart sem erro nenhum aparecer.
builder.Services.AddScoped(sp => new ResolvedorDeLead(
    builder.Configuration["WHATSAPP_NUMERO_EMPRESA"] ?? "+55 11 3333-4444",
    sp.GetRequiredService<IRepositorioDeLeads>()));
builder.Services.AddHostedService<ProcessadorDeMensagens>();

builder.Services.AddScoped<Saude>();
// O Vigia roda pelo relogio, e nao por requisicao: negocio esquecido nao gera
// evento nenhum — ele so fica parado (#53).
builder.Services.AddHostedService<JobDoVigia>();

// O CRM como servidor MCP (#56), DESLIGADO por padrao.
//
// Escopo e autenticacao sao a #58, e ate la o servidor e superficie de leitura
// de dado de cliente sem porteiro. Ligar por padrao seria abrir essa porta para
// quem so fez `docker compose up` — a demo nao precisa dela de pe, e quem for
// experimentar liga de proposito.
var mcpLigado = builder.Configuration.GetValue("MCP_HABILITADO", false);
if (mcpLigado)
{
    builder.Services.AddMcpServer()
        .WithHttpTransport(o => o.Stateless = true)
        .WithToolsFromAssembly();
}

var app = builder.Build();

// Cada dependencia separada, e nao um "ok" agregado: health check que responde
// so verde ou vermelho manda o plantonista procurar do zero (#72).
app.MapGet("/saude", async (Saude saude, CancellationToken ct) =>
{
    var relatorio = await saude.Agora(ct);

    // 503 so quando o ESSENCIAL cai. Estado compartilhado fora degrada, e tirar
    // do ar um sistema que ainda atende seria transformar perda de garantia em
    // perda de atendimento.
    return relatorio.Apta
        ? Results.Ok(new { ok = true, degradada = relatorio.Degradada, relatorio.Dependencias })
        : Results.Json(
            new { ok = false, degradada = true, relatorio.Dependencias },
            statusCode: StatusCodes.Status503ServiceUnavailable);
});

// As rotas que a tela chama (#161).
app.MapearLeitura();

// O canal que empurra a leitura pronta (#50). O polling do front continua
// existindo como degradacao: quando isto cai, a tela fica desatualizada, nao
// vazia.
app.MapHub<DossieHub>(DossieHub.Rota);

// Quanto a triagem poupou (#37). O painel de ROI que vai consumir isto e a #3;
// aqui fica so o numero, sem tela.
app.MapGet("/triagem/economia", (ContadorDeTriagem contador) => Results.Ok(contador.Agora()));
if (mcpLigado) app.MapMcp();
// Auth (#49). O segredo vem SO de variavel de ambiente: sem ela, a aplicacao
// nao sobe. Cair para um segredo embutido seria pior que nao ter auth — daria a
// impressao de proteger enquanto qualquer um forja um token de gestor.
var tokens = new Tokens(builder.Configuration["JWT_SEGREDO"] ?? "");
builder.Services.AddSingleton(tokens);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = tokens.Validacao());

builder.Services.AddAuthorization(o =>
    o.AddPolicy("gestor", p => p.RequireRole(nameof(PerfilDeAcesso.Gestor))));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// O login e o unico caminho que aceita senha, e ele responde a mesma coisa para
// email inexistente e senha errada: dizer "usuario nao encontrado" entrega ao
// atacante metade do trabalho — quais emails existem.
app.MapPost("/auth/login", async (
    Credenciais entrada, CopilotoDbContext ctx, Tokens tokens, CancellationToken ct) =>
{
    var email = (entrada.Email ?? "").Trim().ToLowerInvariant();
    var usuario = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);

    if (usuario is null || !Senhas.Confere(entrada.Senha ?? "", usuario.SenhaHash))
        return Results.Unauthorized();

    return Results.Ok(new
    {
        token = tokens.Emitir(usuario, DateTimeOffset.UtcNow),
        expiraEm = DateTimeOffset.UtcNow + Tokens.Validade,
        perfil = usuario.Perfil.ToString(),
    });
});

app.MapGet("/saude", () => Results.Ok(new { ok = true }));

// O webhook responde na hora e nao processa nada (#40). O 202 e' deliberado: 200
// diria "processado", e o que aconteceu foi "recebido e enfileirado".
//
// O corpo chega CRU e quem o entende e a fonte (#17). O handler nao sabe se
// atras dele esta a Cloud API, o WAHA ou o seed — e e' isso que permite trocar
// de provedor mudando uma variavel de ambiente.
app.MapPost("/webhook/whatsapp", async (
    HttpRequest requisicao, IConversationSource fonte, IQueue<MensagemRecebida> fila,
    CancellationToken ct) =>
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

/// <summary>O que chega no login. A senha nao passa disto para dentro.</summary>
public record Credenciais(string? Email, string? Senha);
