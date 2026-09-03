using Copiloto.Api.Ia;
using Copiloto.Api.Infra;
using Copiloto.Dominio.Ia;

namespace Copiloto.Testes;

/// <summary>
/// O circuito valendo para todas as instancias (#68).
///
/// Com estado em memoria e tres replicas existem tres circuitos independentes:
/// um provedor fora do ar leva 3N requisicoes em vez de N, e cada uma e tempo
/// de espera na frente do vendedor.
/// </summary>
public class CircuitoCompartilhadoTeste
{
    private const string Provedor = "openai";
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    private static async Task Derrubar(CircuitoDoProvedor circuito)
    {
        for (var i = 0; i < CircuitoDoProvedor.FalhasQueAbrem; i++)
            await circuito.RegistrarFalha(Provedor, default);
    }

    [Fact]
    public async Task Circuito_comeca_fechado()
    {
        var circuito = new CircuitoDoProvedor(new InMemoryState());

        Assert.Equal(EstadoDoCircuito.Fechado, await circuito.Estado(Provedor, default));
        Assert.True(await circuito.PodeChamar(Provedor, default));
    }

    [Fact]
    public async Task Falhas_abaixo_do_limiar_nao_abrem()
    {
        var circuito = new CircuitoDoProvedor(new InMemoryState());

        await circuito.RegistrarFalha(Provedor, default);
        await circuito.RegistrarFalha(Provedor, default);

        Assert.True(await circuito.PodeChamar(Provedor, default));
    }

    [Fact]
    public async Task A_instancia_que_abre_protege_as_demais_na_hora()
    {
        // O criterio da issue: sem estado compartilhado, a instancia B ainda
        // teria de falhar tres vezes por conta propria.
        var compartilhado = new InMemoryState();
        var instanciaA = new CircuitoDoProvedor(compartilhado);
        var instanciaB = new CircuitoDoProvedor(compartilhado);

        await Derrubar(instanciaA);

        Assert.Equal(EstadoDoCircuito.Aberto, await instanciaB.Estado(Provedor, default));
        Assert.False(await instanciaB.PodeChamar(Provedor, default));
    }

    [Fact]
    public async Task Com_estado_por_processo_cada_instancia_apanha_sozinha()
    {
        // Documenta o bug que a issue fecha: se alguem trocar o estado
        // compartilhado por um local, este teste passa a ser a realidade.
        var instanciaA = new CircuitoDoProvedor(new InMemoryState());
        var instanciaB = new CircuitoDoProvedor(new InMemoryState());

        await Derrubar(instanciaA);

        Assert.True(await instanciaB.PodeChamar(Provedor, default));
    }

    [Fact]
    public async Task Um_provedor_caido_nao_derruba_o_outro()
    {
        var circuito = new CircuitoDoProvedor(new InMemoryState());

        await Derrubar(circuito);

        Assert.True(await circuito.PodeChamar("anthropic", default));
    }

    [Fact]
    public async Task Passada_a_espera_o_circuito_fica_meio_aberto()
    {
        var agora = T0;
        var circuito = new CircuitoDoProvedor(new InMemoryState(() => agora), () => agora);
        await Derrubar(circuito);

        agora += CircuitoDoProvedor.EsperaAntesDeTestar + TimeSpan.FromSeconds(1);

        Assert.Equal(EstadoDoCircuito.MeioAberto, await circuito.Estado(Provedor, default));
    }

    [Fact]
    public async Task No_meio_aberto_so_UMA_instancia_faz_a_requisicao_de_teste()
    {
        // O detalhe que separa quem implementou de quem leu sobre: se todas
        // testarem juntas, o teste vira a avalanche que o breaker evita.
        var agora = T0;
        var compartilhado = new InMemoryState(() => agora);
        var instancias = Enumerable.Range(0, 10)
            .Select(_ => new CircuitoDoProvedor(compartilhado, () => agora)).ToList();

        await Derrubar(instancias[0]);
        agora += CircuitoDoProvedor.EsperaAntesDeTestar + TimeSpan.FromSeconds(1);

        var podem = await Task.WhenAll(instancias.Select(i => i.PodeChamar(Provedor, default)));

        Assert.Single(podem, pode => pode);
    }

    [Fact]
    public async Task Sonda_que_da_certo_libera_todo_mundo()
    {
        var agora = T0;
        var compartilhado = new InMemoryState(() => agora);
        var sonda = new CircuitoDoProvedor(compartilhado, () => agora);
        var outra = new CircuitoDoProvedor(compartilhado, () => agora);
        await Derrubar(sonda);
        agora += CircuitoDoProvedor.EsperaAntesDeTestar + TimeSpan.FromSeconds(1);

        Assert.True(await sonda.PodeChamar(Provedor, default));
        await sonda.RegistrarSucesso(Provedor, default);

        Assert.Equal(EstadoDoCircuito.Fechado, await outra.Estado(Provedor, default));
        Assert.True(await outra.PodeChamar(Provedor, default));
    }

    [Fact]
    public async Task Sonda_que_falha_fecha_a_porta_de_novo()
    {
        var agora = T0;
        var compartilhado = new InMemoryState(() => agora);
        var sonda = new CircuitoDoProvedor(compartilhado, () => agora);
        var outra = new CircuitoDoProvedor(compartilhado, () => agora);
        await Derrubar(sonda);
        agora += CircuitoDoProvedor.EsperaAntesDeTestar + TimeSpan.FromSeconds(1);

        await sonda.PodeChamar(Provedor, default);
        await sonda.RegistrarFalha(Provedor, default);

        Assert.Equal(EstadoDoCircuito.Aberto, await outra.Estado(Provedor, default));
    }

    [Fact]
    public async Task Sucesso_zera_a_contagem_de_falhas()
    {
        // Sem zerar, tres falhas espalhadas com sucessos no meio abririam o
        // circuito de um provedor saudavel — e o sintoma seria "as vezes o
        // sistema escolhe o modelo caro".
        var circuito = new CircuitoDoProvedor(new InMemoryState());

        await circuito.RegistrarFalha(Provedor, default);
        await circuito.RegistrarFalha(Provedor, default);
        await circuito.RegistrarSucesso(Provedor, default);
        await circuito.RegistrarFalha(Provedor, default);

        Assert.True(await circuito.PodeChamar(Provedor, default));
    }

    [Fact]
    public async Task Falha_velha_nao_conta_para_abrir_hoje()
    {
        var agora = T0;
        var circuito = new CircuitoDoProvedor(new InMemoryState(() => agora), () => agora);

        await circuito.RegistrarFalha(Provedor, default);
        await circuito.RegistrarFalha(Provedor, default);

        agora += CircuitoDoProvedor.JanelaDeContagem + TimeSpan.FromSeconds(1);
        await circuito.RegistrarFalha(Provedor, default);

        Assert.True(await circuito.PodeChamar(Provedor, default));
    }

    [Fact]
    public async Task Provedor_vazio_e_erro_e_nao_circuito_comum()
    {
        var circuito = new CircuitoDoProvedor(new InMemoryState());

        await Assert.ThrowsAsync<ArgumentException>(() => circuito.Estado("  ", default));
    }

    // --- O encontro com o router (#29) ---

    [Fact]
    public async Task O_router_descarta_o_provedor_com_circuito_aberto()
    {
        // O router recebe o retrato pronto: ele decide, nao consulta
        // infraestrutura.
        var compartilhado = new InMemoryState();
        var circuito = new CircuitoDoProvedor(compartilhado);
        await Derrubar(circuito);

        var tabela = new[]
        {
            new ModeloDisponivel("barato", Provedor, 0.5m, 400, [Tarefa.Triagem]),
            new ModeloDisponivel("caro", "anthropic", 4m, 900, [Tarefa.Triagem]),
        };

        var fora = await circuito.Indisponiveis(tabela.Select(m => m.Provedor), default);
        var decisao = new RoteadorDeModelo(tabela, p => fora.Contains(p)).Escolher(Tarefa.Triagem);

        Assert.NotNull(decisao);
        Assert.Equal("caro", decisao!.Modelo);
        Assert.Contains(decisao.Descartados, d => d.Contains("circuito aberto"));
    }

    [Fact]
    public async Task Com_todos_de_pe_o_router_nao_muda_de_ideia()
    {
        var circuito = new CircuitoDoProvedor(new InMemoryState());
        var tabela = new[]
        {
            new ModeloDisponivel("barato", Provedor, 0.5m, 400, [Tarefa.Triagem]),
            new ModeloDisponivel("caro", "anthropic", 4m, 900, [Tarefa.Triagem]),
        };

        var fora = await circuito.Indisponiveis(tabela.Select(m => m.Provedor), default);

        Assert.Empty(fora);
        Assert.Equal("barato",
            new RoteadorDeModelo(tabela, p => fora.Contains(p)).Escolher(Tarefa.Triagem)!.Modelo);
    }
}
