using Copiloto.Api.Persistencia;
using Copiloto.Api.Rag;
using Copiloto.Dominio.Rag;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Xunit;

namespace Copiloto.Testes;

/// <summary>
/// Embedding e busca por semelhanca no Postgres que ja existe (#60).
///
/// Estes testes exigem Postgres COM a extensao vector — SQLite nao serve, e
/// fingir que serve seria testar outra coisa. Sem banco, eles PULAM em vez de
/// passar. Para rodar:
///
///   docker run -d --rm -p 5443:5432 -e POSTGRES_PASSWORD=teste \
///     -e POSTGRES_USER=copiloto -e POSTGRES_DB=copiloto pgvector/pgvector:pg16
///   POSTGRES_URL="Host=localhost;Port=5443;Database=copiloto;Username=copiloto;Password=teste" dotnet test
/// </summary>
public class PgvectorTeste : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    private static string? Url => Environment.GetEnvironmentVariable("POSTGRES_URL");

    private readonly FakeEmbeddingProvider _embedder = new();
    private CopilotoDbContext? _ctx;

    public async Task InitializeAsync()
    {
        if (Url is null) return;

        _ctx = new CopilotoDbContext(new DbContextOptionsBuilder<CopilotoDbContext>()
            .UseNpgsql(Url, npg => npg.UseVector()).Options);

        // Recria do zero: teste que herda linha de execucao anterior mede o que
        // sobrou, e nao o que ele escreveu.
        await _ctx.Database.EnsureDeletedAsync();
        await _ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_ctx is not null) await _ctx.DisposeAsync();
    }

    private BuscaComPgvector Busca() => new(_ctx!);

    private async Task<Precedente> Guardar(string trecho, Guid? lead = null)
    {
        var precedente = new Precedente(
            Guid.NewGuid(), lead ?? Guid.NewGuid(), trecho,
            await _embedder.Vetorizar(trecho, default), T0);

        await Busca().Guardar(precedente, default);
        return precedente;
    }

    // --- O provedor fake ---

    [Fact]
    public async Task O_mesmo_texto_gera_sempre_o_mesmo_vetor()
    {
        // Deterministico em qualquer maquina e execucao: um Random sem semente
        // fixa daria um teste que passa uma vez e nunca mais.
        var a = await _embedder.Vetorizar("cliente travou no preco", default);
        var b = await _embedder.Vetorizar("cliente travou no preco", default);

        Assert.Equal(a, b);
    }

    [Fact]
    public async Task Textos_diferentes_geram_vetores_diferentes()
    {
        var a = await _embedder.Vetorizar("cliente travou no preco", default);
        var b = await _embedder.Vetorizar("cliente pediu prazo", default);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task O_vetor_tem_a_largura_da_coluna()
    {
        // Dimensao errada nao e detalhe: a busca compararia coisas de espacos
        // diferentes, e o resultado pareceria plausivel.
        var vetor = await _embedder.Vetorizar("qualquer coisa", default);

        Assert.Equal(Embedding.Dimensoes, vetor.Length);
    }

    [Fact]
    public void Vetor_de_dimensao_errada_e_recusado_no_dominio()
    {
        Assert.Throws<ArgumentException>(() => new Precedente(
            Guid.NewGuid(), Guid.NewGuid(), "trecho", new float[8], T0));
    }

    [Fact]
    public void Precedente_sem_titular_nao_existe()
    {
        // Vetor que sobrevive ao Lead e dado pessoal vivo depois do pedido de
        // exclusao (#46, #62).
        Assert.Throws<ArgumentException>(() => new Precedente(
            Guid.NewGuid(), Guid.Empty, "trecho", new float[Embedding.Dimensoes], T0));
    }

    // --- O banco ---

    [SkippableFact]
    public async Task O_precedente_atravessa_o_banco_com_o_vetor_inteiro()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: banco com pgvector nao disponivel");

        var guardado = await Guardar("o cliente travou no preco e fechou com 8%");

        var lido = await _ctx!.Precedentes.AsNoTracking()
            .FirstAsync(p => p.Id == guardado.Id);

        Assert.Equal(guardado.Trecho, lido.Trecho);
        Assert.Equal(Embedding.Dimensoes, lido.Vetor.Length);
        Assert.Equal(guardado.Vetor[0], lido.Vetor[0], 5);
    }

    [SkippableFact]
    public async Task A_busca_devolve_o_mais_parecido_primeiro()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: banco com pgvector nao disponivel");

        await Guardar("o cliente travou no preco e fechou com 8%");
        await Guardar("prazo de entrega para a zona sul");
        await Guardar("pediu amostra antes de decidir");

        var consulta = await _embedder.Vetorizar("o cliente travou no preco e fechou com 8%", default);
        var achados = await Busca().MaisParecidos(consulta, quantos: 2, default);

        Assert.Equal(2, achados.Count);
        Assert.Contains("travou no preco", achados[0].Precedente.Trecho);

        // A distancia sobe junto: sem ela, quem consome nao distingue "achei um
        // parecido" de "achei o menos ruim de uma base que nao tinha nada a ver".
        Assert.True(achados[0].Distancia < achados[1].Distancia);
        Assert.InRange(achados[0].Distancia, 0, 0.0001);
    }

    [SkippableFact]
    public async Task A_busca_respeita_o_limite_pedido()
    {
        Skip.If(Url is null, "sem POSTGRES_URL: banco com pgvector nao disponivel");

        for (var i = 0; i < 5; i++) await Guardar($"conversa numero {i}");

        var consulta = await _embedder.Vetorizar("conversa numero 1", default);

        Assert.Equal(2, (await Busca().MaisParecidos(consulta, quantos: 2, default)).Count);
    }

    [SkippableFact]
    public async Task Esquecer_o_titular_apaga_os_vetores_dele_e_so_os_dele()
    {
        // O expurgo em cascata do art. 18: apagar o Lead sem apagar o vetor
        // deixa o dado vivo depois de o titular pedir exclusao (#46, #62).
        Skip.If(Url is null, "sem POSTGRES_URL: banco com pgvector nao disponivel");

        var marina = Guid.NewGuid();
        var outro = Guid.NewGuid();
        await Guardar("marina travou no preco", marina);
        await Guardar("marina pediu prazo", marina);
        await Guardar("outro cliente qualquer", outro);

        var apagados = await Busca().EsquecerTitular(marina, default);

        Assert.Equal(2, apagados);
        Assert.Equal(0, await _ctx!.Precedentes.CountAsync(p => p.LeadId == marina));
        Assert.Equal(1, await _ctx.Precedentes.CountAsync(p => p.LeadId == outro));
    }

    [SkippableFact]
    public async Task O_indice_de_similaridade_e_usado_pela_consulta()
    {
        // Criterio de aceite: verificar o PLANO. Um indice criado com operador
        // diferente do da consulta simplesmente nao e usado, e o sintoma e
        // lentidao silenciosa — nao erro.
        Skip.If(Url is null, "sem POSTGRES_URL: banco com pgvector nao disponivel");

        for (var i = 0; i < 20; i++) await Guardar($"conversa {i}");

        var alvo = new Pgvector.Vector(await _embedder.Vetorizar("conversa 7", default));

        var plano = new List<string>();
        var conexao = _ctx!.Database.GetDbConnection();
        await conexao.OpenAsync();
        try
        {
            // `enable_seqscan = off` na MESMA conexao do EXPLAIN: o ajuste vale
            // por sessao, e mandar em outra conexao nao chega aqui — foi o que
            // fez a primeira versao deste teste medir o plano sem o ajuste.
            //
            // Ele e necessario porque com vinte linhas a varredura sequencial e
            // MESMO mais rapida, e o planejador esta certo em escolher ela. O
            // que se prova aqui e que o indice E UTILIZAVEL por esta consulta —
            // e e isso que deixa de valer quando o operador do indice nao bate
            // com o da consulta, sem erro nenhum aparecer.
            await using (var ajuste = conexao.CreateCommand())
            {
                ajuste.CommandText = "SET enable_seqscan = off";
                await ajuste.ExecuteNonQueryAsync();
            }

            await using var cmd = conexao.CreateCommand();
            cmd.CommandText =
                // Nomes entre aspas: as colunas foram criadas com maiuscula, e sem
                // as aspas o Postgres procura por `id` minusculo e nao acha.
                "EXPLAIN SELECT \"Id\" FROM precedentes ORDER BY \"Vetor\" <=> $1 LIMIT 3";
            var p = cmd.CreateParameter();
            p.Value = alvo;
            cmd.Parameters.Add(p);

            await using var leitura = await cmd.ExecuteReaderAsync();
            while (await leitura.ReadAsync()) plano.Add(leitura.GetString(0));
        }
        finally
        {
            await conexao.CloseAsync();
        }

        var texto = string.Join("\n", plano);
        Assert.Contains("ix_precedentes_vetor", texto);
    }
}
