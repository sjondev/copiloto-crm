using Copiloto.Api.Ia;
using Copiloto.Api.Infra;

namespace Copiloto.Testes;

/// <summary>
/// Rate limit e cache de analise valendo entre instancias (#71).
///
/// Os dois assumiam processo unico, e os dois quebram calados: o limite vira
/// limite vezes replicas, e o cache perde acerto na mesma proporcao. O terceiro
/// criterio da issue e o unico que quebra ALTO — cache mal chaveado serve o
/// dossie de um cliente para outro.
/// </summary>
public class LimiteECacheTeste
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Minuto = TimeSpan.FromMinutes(1);

    /// <summary>
    /// As implementacoes de estado cobradas por estes testes. RedisState (#70)
    /// entra com uma linha, e o teste de isolamento do cache passa a rodar nos
    /// dois backends, como o criterio pede.
    /// </summary>
    public static TheoryData<string, Func<IDistributedState>> Backends => new()
    {
        { "inmemory", () => new InMemoryState() },
    };

    // --- Rate limit ---

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task O_limite_vale_para_as_duas_instancias_juntas(
        string nome, Func<IDistributedState> criar)
    {
        // O ponto da issue: limite que se multiplica pelo numero de replicas
        // nao e limite, e sugestao.
        var compartilhado = criar();
        var usuario = Guid.NewGuid();
        var instanciaA = new LimitadorDeTaxa(compartilhado, limite: 3, Minuto);
        var instanciaB = new LimitadorDeTaxa(compartilhado, limite: 3, Minuto);

        Assert.True(await instanciaA.Permite(usuario, default));
        Assert.True(await instanciaB.Permite(usuario, default));
        Assert.True(await instanciaA.Permite(usuario, default));
        Assert.False(await instanciaB.Permite(usuario, default));
        Assert.NotNull(nome);
    }

    [Fact]
    public async Task Cada_usuario_tem_o_proprio_balde()
    {
        // Sem a chave por usuario, o primeiro vendedor movimentado bloquearia
        // a empresa inteira.
        var limitador = new LimitadorDeTaxa(new InMemoryState(), limite: 1, Minuto);

        Assert.True(await limitador.Permite(Guid.NewGuid(), default));
        Assert.True(await limitador.Permite(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task A_janela_vira_e_o_usuario_volta_a_poder()
    {
        var agora = T0;
        var limitador = new LimitadorDeTaxa(new InMemoryState(() => agora), limite: 1, Minuto);
        var usuario = Guid.NewGuid();

        Assert.True(await limitador.Permite(usuario, default));
        Assert.False(await limitador.Permite(usuario, default));

        agora = agora.AddMinutes(2);

        Assert.True(await limitador.Permite(usuario, default));
    }

    [Fact]
    public async Task Quem_insiste_alem_do_teto_continua_sendo_contado()
    {
        // Contador que para de subir apaga o unico sinal de que houve
        // insistencia — e insistencia e o que se quer enxergar.
        var estado = new InMemoryState();
        var limitador = new LimitadorDeTaxa(estado, limite: 1, Minuto);
        var usuario = Guid.NewGuid();

        for (var i = 0; i < 5; i++) await limitador.Permite(usuario, default);

        Assert.Equal(6, await estado.Incrementar($"rate:usuario:{usuario}", Minuto, default));
    }

    [Fact]
    public void Limite_zero_e_recusado_na_construcao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LimitadorDeTaxa(new InMemoryState(), limite: 0, Minuto));
    }

    [Fact]
    public async Task Rate_limit_sem_usuario_e_erro_e_nao_balde_comum()
    {
        var limitador = new LimitadorDeTaxa(new InMemoryState(), limite: 1, Minuto);

        await Assert.ThrowsAsync<ArgumentException>(() => limitador.Permite(Guid.Empty, default));
    }

    // --- Cache de analise ---

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task O_cache_nunca_serve_dossie_de_outro_lead(
        string nome, Func<IDistributedState> criar)
    {
        // O criterio que quebra alto: sem a conferencia do dono, uma chave
        // colidida mostraria um texto plausivel sobre a pessoa errada — sem
        // erro, sem log, com a tela parecendo certa.
        var compartilhado = criar();
        var cache = new CacheDeAnalise(compartilhado);
        var marina = Guid.NewGuid();
        var lucas = Guid.NewGuid();
        var chave = CacheDeAnalise.Chave(marina, Guid.NewGuid(), "v1");

        await cache.Guardar(marina, chave, "Marina está pronta para fechar", default);

        Assert.Null(await cache.Ler(lucas, chave, default));
        Assert.Equal("Marina está pronta para fechar", await cache.Ler(marina, chave, default));
        Assert.NotNull(nome);
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task A_segunda_instancia_aproveita_o_que_a_primeira_pagou(
        string nome, Func<IDistributedState> criar)
    {
        var compartilhado = criar();
        var lead = Guid.NewGuid();
        var chave = CacheDeAnalise.Chave(lead, Guid.NewGuid(), "v1");

        await new CacheDeAnalise(compartilhado).Guardar(lead, chave, "dossiê", default);

        Assert.Equal("dossiê", await new CacheDeAnalise(compartilhado).Ler(lead, chave, default));
        Assert.NotNull(nome);
    }

    [Fact]
    public void A_chave_muda_quando_a_conversa_anda()
    {
        // Servir analise velha depois de o cliente falar de novo e o erro pior
        // do cache: a resposta PARECE certa.
        var lead = Guid.NewGuid();

        var antes = CacheDeAnalise.Chave(lead, Guid.NewGuid(), "v1");
        var depois = CacheDeAnalise.Chave(lead, Guid.NewGuid(), "v1");

        Assert.NotEqual(antes, depois);
    }

    [Fact]
    public void A_chave_muda_quando_o_prompt_muda_de_versao()
    {
        // Prompt novo com resposta velha esconde justamente o efeito da
        // mudanca que alguem acabou de fazer.
        var lead = Guid.NewGuid();
        var mensagem = Guid.NewGuid();

        Assert.NotEqual(
            CacheDeAnalise.Chave(lead, mensagem, "v1"),
            CacheDeAnalise.Chave(lead, mensagem, "v2"));
    }

    [Fact]
    public void A_chave_leva_o_lead_no_prefixo_para_expurgo_por_titular()
    {
        // O titular pede exclusao e alguem precisa achar o que e dele no Redis
        // sem varrer tudo (#46).
        var lead = Guid.NewGuid();

        Assert.StartsWith($"analise:{lead}:", CacheDeAnalise.Chave(lead, Guid.NewGuid(), "v1"));
    }

    [Fact]
    public async Task A_metrica_de_acerto_conta_o_que_pagou_e_o_que_nao_pagou()
    {
        var cache = new CacheDeAnalise(new InMemoryState());
        var lead = Guid.NewGuid();
        var chave = CacheDeAnalise.Chave(lead, Guid.NewGuid(), "v1");

        Assert.Null(await cache.Ler(lead, chave, default));
        await cache.Guardar(lead, chave, "dossiê", default);
        await cache.Ler(lead, chave, default);

        Assert.Equal(1, cache.Acertos);
        Assert.Equal(1, cache.Erros);
        Assert.Equal(0.5, cache.TaxaDeAcerto);
    }

    [Fact]
    public async Task Analise_vencida_nao_e_servida()
    {
        var agora = T0;
        var cache = new CacheDeAnalise(new InMemoryState(() => agora), TimeSpan.FromHours(6));
        var lead = Guid.NewGuid();
        var chave = CacheDeAnalise.Chave(lead, Guid.NewGuid(), "v1");
        await cache.Guardar(lead, chave, "dossiê", default);

        agora = agora.AddHours(7);

        Assert.Null(await cache.Ler(lead, chave, default));
    }

    [Fact]
    public void Sem_leitura_nenhuma_a_taxa_nao_inventa_numero()
    {
        // Zero leitura com taxa "100%" viraria um painel que mente para cima.
        Assert.Equal(0, new CacheDeAnalise(new InMemoryState()).TaxaDeAcerto);
    }
}
