using System.Reflection;
using Copiloto.Api.Ia;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Ia;
using Microsoft.Extensions.Logging.Abstractions;

namespace Copiloto.Testes;

/// <summary>
/// O triador A0 (#37).
///
/// Boa parte do trafego de WhatsApp e ruido social, e filtrar antes do modelo
/// caro e economia de multiplo direto. Mas o filtro erra caro: fala descartada
/// nao entra no dossie, e o vendedor nunca fica sabendo que ela existiu.
///
/// A metade mais importante destes testes nao prova que ele economiza — prova
/// que ele NAO descarta o que importa.
/// </summary>
public class TriadorTeste
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static Mensagem Do(Autor autor, string texto, int minuto = 0, Midia? midia = null) =>
        new(Guid.NewGuid(), autor, texto, Agora.AddMinutes(minuto), midia);

    private static Triagem? Heuristica(Mensagem fala, Mensagem? anterior = null) =>
        TriagemPorHeuristica.Avaliar(fala, anterior);

    [Theory]
    [InlineData("ok")]
    [InlineData("Obrigado!")]
    [InlineData("obrigada")]
    [InlineData("vlw")]
    [InlineData("bom dia")]
    [InlineData("Boa noite.")]
    [InlineData("blz")]
    [InlineData("perfeito")]
    [InlineData("  ótimo  ")]
    public void Cortesia_solta_do_cliente_nao_acorda_modelo_nenhum(string texto)
    {
        var triagem = Heuristica(Do(Autor.Cliente, texto));

        Assert.NotNull(triagem);
        Assert.False(triagem!.Analisa);
        Assert.True(triagem.PorHeuristica);
    }

    [Fact]
    public void Acento_e_pontuacao_nao_escapam_do_filtro()
    {
        // "Obrigado!!!" e "obrigado" sao a mesma coisa, e o cliente escreve das
        // duas formas no mesmo dia.
        Assert.False(Heuristica(Do(Autor.Cliente, "ObRiGaDo!!!"))!.Analisa);
        Assert.False(Heuristica(Do(Autor.Cliente, "ótimo."))!.Analisa);
    }

    [Fact]
    public void So_emoji_e_ruido()
    {
        Assert.False(Heuristica(Do(Autor.Cliente, "👍"))!.Analisa);
        Assert.False(Heuristica(Do(Autor.Cliente, "😂😂😂"))!.Analisa);
    }

    [Fact]
    public void Resposta_curta_LOGO_DEPOIS_do_vendedor_nao_e_descartada()
    {
        // O caso que torna o triador seguro. "ok" depois de uma proposta e a
        // OBJECAO VELADA que a #13 existe para captar — resposta monossilabica
        // depois de mensagem longa e sinal de fuga. Descartar isso seria
        // economizar centavos destruindo o produto.
        var proposta = Do(Autor.Vendedor, "fecho 2kg do bourbon a 78 com frete incluso, topa?", 0);
        var resposta = Do(Autor.Cliente, "ok", 3);

        var triagem = Heuristica(resposta, proposta);

        Assert.NotNull(triagem);
        Assert.True(triagem!.Analisa);
        Assert.Contains("objecao velada", triagem.Motivo);
    }

    [Fact]
    public void Cortesia_muito_depois_da_fala_do_vendedor_volta_a_ser_ruido()
    {
        // Passada a janela, "bom dia" nao responde mais a proposta de ontem.
        var proposta = Do(Autor.Vendedor, "fecho 2kg a 78?", 0);
        var cumprimento = Do(Autor.Cliente, "bom dia", (int)TriagemPorHeuristica.JanelaDeResposta.TotalMinutes + 60);

        Assert.False(Heuristica(cumprimento, proposta)!.Analisa);
    }

    [Fact]
    public void Cortesia_do_vendedor_e_sempre_ruido()
    {
        // "bom dia" dito por quem vende nao conta nada sobre o cliente, que e o
        // unico assunto do dossie.
        var doCliente = Do(Autor.Cliente, "qual o valor do kg?", 0);

        Assert.False(Heuristica(Do(Autor.Vendedor, "bom dia!", 1), doCliente)!.Analisa);
    }

    [Fact]
    public void Midia_nunca_e_descartada()
    {
        // Sem regerar o dossie, a lacuna do audio (#23) nunca seria declarada.
        var audio = Do(Autor.Cliente, "", 1, new Midia(TipoDeMidia.Audio, TimeSpan.FromSeconds(30)));

        var triagem = Heuristica(audio, Do(Autor.Vendedor, "manda ai", 0));

        Assert.True(triagem!.Analisa);
        Assert.Contains("lacuna", triagem.Motivo);
    }

    [Theory]
    [InlineData("qual o valor do kg?")]
    [InlineData("vou pensar melhor e te falo")]
    [InlineData("tem desconto pra 5kg?")]
    [InlineData("ok, mas o prazo me preocupa")]
    public void Fala_com_conteudo_nao_e_decidida_pela_heuristica(string texto)
    {
        // Devolver null e a resposta honesta: forcar veredito aqui faria a
        // heuristica opinar sobre a fala que ela nao entende, que e exatamente
        // onde o modelo barato entra.
        Assert.Null(Heuristica(Do(Autor.Cliente, texto)));
    }

    // --- o triador completo, com o modelo barato atras ---

    private static Triador Montar(IModelProvider provedor, ContadorDeTriagem? contador = null) =>
        new(new CascataDeModelos(
                new RoteadorDeModelo([new ModeloDisponivel("fake-mini", "fake", 0.5m, 10, [Tarefa.Triagem])]),
                provedor,
                NullLogger<CascataDeModelos>.Instance),
            contador ?? new ContadorDeTriagem(0.5m),
            NullLogger<Triador>.Instance);

    private static FakeProvider ProvedorDoSeed()
    {
        var raiz = typeof(TriadorTeste).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RaizDoRepositorio").Value!;

        return FakeProvider.DaPasta(Path.Combine(raiz, "seed", "respostas"));
    }

    [Fact]
    public async Task Pergunta_de_preco_dispara_a_analise()
    {
        // Criterio literal da issue. O seed do A0 responde vale_analisar: true.
        var triagem = await Montar(ProvedorDoSeed())
            .Triar(Do(Autor.Cliente, "qual o valor do kg?"), null, CancellationToken.None);

        Assert.True(triagem.Analisa);
        Assert.False(triagem.PorHeuristica);
    }

    [Fact]
    public async Task Ok_isolado_nao_dispara_analise()
    {
        // Criterio literal da issue, e resolvido sem gastar modelo nenhum.
        var triagem = await Montar(ProvedorDoSeed())
            .Triar(Do(Autor.Cliente, "ok"), null, CancellationToken.None);

        Assert.False(triagem.Analisa);
        Assert.True(triagem.PorHeuristica);
    }

    [Fact]
    public async Task Triador_fora_do_ar_manda_analisar()
    {
        // Degradar para "descartar tudo" faria o dossie parar de atualizar em
        // silencio justamente quando a infraestrutura ja esta ruim.
        var provedor = ProvedorDoSeed();
        provedor.CombinarFalha(FalhaSimulada.ProvedorForaDoAr);

        var triagem = await Montar(provedor)
            .Triar(Do(Autor.Cliente, "tem desconto pra 5kg?"), null, CancellationToken.None);

        Assert.True(triagem.Analisa);
        Assert.Contains("na duvida", triagem.Motivo);
    }

    [Fact]
    public async Task Triador_respondendo_ilegivel_manda_analisar()
    {
        var provedor = ProvedorDoSeed();
        provedor.CombinarFalha(FalhaSimulada.JsonInvalido);

        var triagem = await Montar(provedor)
            .Triar(Do(Autor.Cliente, "tem desconto pra 5kg?"), null, CancellationToken.None);

        Assert.True(triagem.Analisa);
    }

    [Fact]
    public async Task A_economia_sai_em_numero()
    {
        // "Percentual de descarte visivel" e "economia estimada": os dois saem
        // daqui para o painel da #3.
        var contador = new ContadorDeTriagem(custoPorMilTokens: 8m, tokensPorLeitura: 1000);
        var triador = Montar(ProvedorDoSeed(), contador);

        await triador.Triar(Do(Autor.Cliente, "ok"), null, CancellationToken.None);
        await triador.Triar(Do(Autor.Cliente, "obrigado"), null, CancellationToken.None);
        await triador.Triar(Do(Autor.Cliente, "vlw"), null, CancellationToken.None);
        await triador.Triar(Do(Autor.Cliente, "qual o valor do kg?"), null, CancellationToken.None);

        var economia = contador.Agora();

        Assert.Equal(4, economia.Total);
        Assert.Equal(3, economia.DescartadasPorHeuristica);
        Assert.Equal(1, economia.Analisadas);
        Assert.Equal(0.75, economia.PercentualDeDescarte, 2);
        Assert.Equal(24m, economia.EconomiaEstimada);
    }

    [Fact]
    public void Contador_vazio_nao_divide_por_zero()
    {
        var economia = new ContadorDeTriagem().Agora();

        Assert.Equal(0, economia.Total);
        Assert.Equal(0, economia.PercentualDeDescarte);
        Assert.Equal(0m, economia.EconomiaEstimada);
    }
}
