using System.Reflection;
using System.Text.Json;
using Copiloto.Api.Ia;
using Copiloto.Dominio.Ia;
using Microsoft.Extensions.Configuration;

namespace Copiloto.Testes;

/// <summary>
/// O provedor falso (#27), que e a primeira coisa construida na camada de IA.
///
/// O que se prova aqui nao e que ele responde — e que ele MENTE sob demanda.
/// A orquestracao inteira e feita de tratamento de caso ruim, e caso ruim so se
/// testa se der para provoca-lo na hora exata; contra API real ninguem consegue
/// um 429 quando quer, e o codigo que trata 429 fica sendo o unico que nunca
/// rodou antes de producao.
/// </summary>
public class FakeProviderTeste
{
    private static string PastaDasRespostas()
    {
        var raiz = typeof(FakeProviderTeste).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RaizDoRepositorio").Value!;
        return Path.Combine(raiz, "seed", "respostas");
    }

    private static FakeProvider Provedor() => FakeProvider.DaPasta(PastaDasRespostas());

    private static PedidoAoModelo Pedido(Tarefa tarefa = Tarefa.Leitura) =>
        new(tarefa, "fake-mini", "leia esta conversa");

    private static IConfiguration Config(string? provedor)
    {
        var valores = new Dictionary<string, string?> { [ProvedorDeModelo.ChaveDaPasta] = PastaDasRespostas() };
        if (provedor is not null) valores[ProvedorDeModelo.Chave] = provedor;

        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }

    [Fact]
    public void As_tres_tarefas_tem_resposta_gravada()
    {
        var gravadas = Provedor().TarefasGravadas;

        Assert.Contains(Tarefa.Triagem, gravadas);
        Assert.Contains(Tarefa.Leitura, gravadas);
        Assert.Contains(Tarefa.Conselho, gravadas);
    }

    [Fact]
    public async Task Responde_de_arquivo_com_os_tokens_separados()
    {
        // Entrada e saida tem precos diferentes em todo provedor real, e um
        // total unico impediria a conta do ledger (#1).
        //
        // As asserções são sobre a FORMA e não sobre os números do seed: o
        // conteúdo do arquivo muda quando o prompt evolui, e um teste preso ao
        // valor exato quebraria a cada ajuste sem nada de errado ter
        // acontecido — e teste que quebra à toa é teste que alguém desliga.
        var resposta = await Provedor().Responder(Pedido(), CancellationToken.None);

        Assert.True(resposta.TokensEntrada > 0);
        Assert.True(resposta.TokensSaida > 0);
        Assert.NotEqual(resposta.TokensEntrada, resposta.TokensSaida);
        Assert.Equal(resposta.TokensEntrada + resposta.TokensSaida, resposta.TokensTotais);
    }

    [Fact]
    public async Task A_resposta_gravada_e_json_valido()
    {
        var resposta = await Provedor().Responder(Pedido(), CancellationToken.None);

        var lido = JsonDocument.Parse(resposta.Conteudo);
        Assert.Equal("consideracao", lido.RootElement.GetProperty("estagio").GetString());
    }

    [Fact]
    public async Task O_conselho_gravado_respeita_a_regra_de_ancoragem()
    {
        // O arquivo de seed e o exemplo de referencia da regra (#15, #16): sem
        // dado de estoque no CRM, escassez NAO vira fala pronta — vira pergunta
        // ao vendedor. Se alguem "melhorar" o seed enchendo esse bloco, isto
        // falha, e e para falhar mesmo.
        var resposta = await Provedor().Responder(Pedido(Tarefa.Conselho), CancellationToken.None);

        var escassez = JsonDocument.Parse(resposta.Conteudo).RootElement
            .GetProperty("blocos").EnumerateArray()
            .Single(b => b.GetProperty("tecnica").GetString() == "Escassez");

        Assert.Equal(JsonValueKind.Null, escassez.GetProperty("conselho").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(
            escassez.GetProperty("pergunta_ao_vendedor").GetString()));
    }

    [Fact]
    public async Task Tarefa_sem_resposta_gravada_nao_e_improvisada()
    {
        // Improvisar passaria no teste e esconderia que o agente daquela tarefa
        // nunca foi exercitado.
        var vazio = new FakeProvider();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => vazio.Responder(Pedido(), CancellationToken.None));

        Assert.Contains("seed/respostas", erro.Message);
    }

    [Fact]
    public async Task Timeout_sob_demanda()
    {
        var provedor = Provedor();
        provedor.CombinarFalha(FalhaSimulada.Timeout);

        await Assert.ThrowsAsync<TimeoutException>(
            () => provedor.Responder(Pedido(), CancellationToken.None));
    }

    [Fact]
    public async Task Limite_de_taxa_sob_demanda_traz_o_tempo_de_espera()
    {
        // A cascata (#30) espera e tenta de novo no 429, mas TROCA de provedor
        // quando ele caiu — por isso as duas falhas nao compartilham excecao.
        var provedor = Provedor();
        provedor.CombinarFalha(FalhaSimulada.LimiteExcedido);

        var erro = await Assert.ThrowsAsync<LimiteDeTaxaExcedido>(
            () => provedor.Responder(Pedido(), CancellationToken.None));

        Assert.Equal(TimeSpan.FromSeconds(2), erro.TentarDepoisDe);
        Assert.Equal("fake", erro.Provedor);
    }

    [Fact]
    public async Task Provedor_fora_do_ar_sob_demanda()
    {
        var provedor = Provedor();
        provedor.CombinarFalha(FalhaSimulada.ProvedorForaDoAr);

        var erro = await Assert.ThrowsAsync<ProvedorForaDoAr>(
            () => provedor.Responder(Pedido(), CancellationToken.None));

        Assert.Equal("fake", erro.Provedor);
    }

    [Fact]
    public async Task Json_invalido_volta_como_sucesso_e_nao_como_excecao()
    {
        // E assim que o caso acontece de verdade: HTTP 200, conteudo truncado.
        // Quem trata e o Contract Validator (#32), e ele so pode ser testado se
        // o fake souber mentir desse jeito exato.
        var provedor = Provedor();
        provedor.CombinarFalha(FalhaSimulada.JsonInvalido);

        var resposta = await provedor.Responder(Pedido(), CancellationToken.None);

        Assert.ThrowsAny<JsonException>(() => JsonDocument.Parse(resposta.Conteudo));
    }

    [Fact]
    public async Task Falha_combinada_vale_o_numero_de_vezes_e_depois_passa()
    {
        // O caso que abre e fecha o circuito (#38): falha tres vezes e ai
        // responde. Com um estado unico, isto viraria liga-desliga manual no
        // meio do cenario.
        var provedor = Provedor();
        provedor.CombinarFalha(FalhaSimulada.ProvedorForaDoAr, vezes: 3);

        for (var tentativa = 1; tentativa <= 3; tentativa++)
        {
            await Assert.ThrowsAsync<ProvedorForaDoAr>(
                () => provedor.Responder(Pedido(), CancellationToken.None));
        }

        var resposta = await provedor.Responder(Pedido(), CancellationToken.None);
        Assert.NotEmpty(resposta.Conteudo);
    }

    [Fact]
    public async Task Latencia_artificial_e_configuravel_e_zero_por_padrao()
    {
        var provedor = Provedor();
        Assert.Equal(TimeSpan.Zero, provedor.Latencia);

        provedor.Latencia = TimeSpan.FromMilliseconds(40);
        var relogio = System.Diagnostics.Stopwatch.StartNew();
        await provedor.Responder(Pedido(), CancellationToken.None);
        relogio.Stop();

        Assert.True(relogio.ElapsedMilliseconds >= 30, $"esperou so {relogio.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Sem_configuracao_o_provedor_e_o_fake()
    {
        // Ninguem sobe o projeto pela primeira vez gastando dinheiro sem pedir.
        Assert.IsType<FakeProvider>(ProvedorDeModelo.Escolher(Config(null), ""));
    }

    [Fact]
    public void Provedor_desconhecido_derruba_a_subida()
    {
        // Cair no fake aqui e pior que na ingestao: a API devolveria conselho
        // gravado em arquivo com cara de leitura da conversa do cliente.
        var erro = Assert.Throws<ArgumentException>(
            () => ProvedorDeModelo.Escolher(Config("openai"), ""));

        Assert.Contains("openai", erro.Message);
    }

    [Fact]
    public void Pasta_de_respostas_ausente_diz_o_que_falta()
    {
        var erro = Assert.Throws<DirectoryNotFoundException>(
            () => FakeProvider.DaPasta("/nao/existe/respostas"));

        Assert.Contains("primeiro clone", erro.Message);
    }
}
