using System.Reflection;
using Copiloto.Api.Ingestao;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Dossies;
using Copiloto.Dominio.Vendas;

namespace Copiloto.Testes;

/// <summary>
/// A objecao que ninguem diz (#9).
///
/// "Vou pensar" e a objecao mais comum do varejo e a que mais vendedor novato
/// interpreta como interesse. Mas o sinal mais forte nem sempre esta no texto:
/// esta na resposta que encurtou, no intervalo que cresceu, no cliente que
/// sumiu depois do numero.
///
/// Nada aqui usa modelo. E aritmetica sobre a conversa — roda de graca e da o
/// mesmo resultado toda vez, o que faz desta a parte do dossie que nao alucina.
/// </summary>
public class ObjecaoVeladaTeste
{
    private static readonly DateTimeOffset Inicio = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private static Conversa Com(params (Autor Autor, string Texto, int Minuto)[] falas)
    {
        var conversa = new Conversa(Guid.NewGuid(), Guid.NewGuid());
        foreach (var (autor, texto, minuto) in falas)
            conversa.Registrar(new Mensagem(Guid.NewGuid(), autor, texto, Inicio.AddMinutes(minuto)));

        return conversa;
    }

    /// <summary>A conversa 3 do seed, montada pelo caminho real do FakeSource.</summary>
    private static Conversa EsfriaESome()
    {
        var raiz = typeof(ObjecaoVeladaTeste).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RaizDoRepositorio").Value!;

        var fonte = FakeSource.DaPasta(Path.Combine(raiz, "seed", "conversas"));
        var conversa = new Conversa(Guid.NewGuid(), Guid.NewGuid());

        // Quem falou sai do ResolvedorDeLead, e nao de comparar string de
        // telefone. O cliente do seed e +55 11 96666-3333 e a empresa e
        // +55 11 3333-4444: procurar "3333" no texto cru classifica o cliente
        // como vendedor e inverte a conversa inteira. E o motivo de a #22
        // existir.
        var resolvedor = new ResolvedorDeLead("+55 11 3333-4444");

        foreach (var bruta in fonte.Reproduzir("esfria-e-some", Inicio))
        {
            var autor = resolvedor.QuemFalou(Telefone.Normalizar(bruta.De)!);
            conversa.Registrar(new Mensagem(
                IdDaMensagem.De(bruta.ProviderMessageId), autor, bruta.Texto, bruta.EnviadaEm, bruta.Midia));
        }

        return conversa;
    }

    [Fact]
    public void A_conversa_do_seed_que_esfria_e_lida_como_esfriando()
    {
        // O teste que a issue pede por nome. Se o detector nao acusar NESTA
        // conversa, ele nao serve para nada — ela foi escrita para ser o caso
        // dificil do produto.
        var achadas = PadraoDeConversa.Detectar(EsfriaESome(), Inicio.AddDays(5));

        Assert.NotEmpty(achadas);
        Assert.Contains(achadas, o => o.Tipo == TipoDeObjecao.Timing);
        Assert.Contains(achadas, o => o.PorComportamento);
    }

    [Fact]
    public void Toda_objecao_cita_uma_fala_que_existe_na_conversa()
    {
        var conversa = EsfriaESome();

        var achadas = PadraoDeConversa.Detectar(conversa, Inicio.AddDays(5));

        Assert.All(achadas, o =>
        {
            var origem = Assert.Single(conversa.Mensagens, m => m.Id == o.MensagemId);
            Assert.Equal(origem.Texto, o.TrechoCitado);
        });
    }

    [Fact]
    public void Vou_pensar_e_lido_como_adiamento_e_nao_como_interesse()
    {
        var conversa = Com(
            (Autor.Cliente, "quanto sai o plano mensal?", 0),
            (Autor.Vendedor, "mando a proposta agora", 5),
            (Autor.Cliente, "vou pensar e te falo", 10));

        var adiamento = Assert.Single(
            PadraoDeConversa.Detectar(conversa, Inicio.AddMinutes(20)),
            o => o.Tipo == TipoDeObjecao.Timing);

        Assert.Equal("vou pensar e te falo", adiamento.TrechoCitado);
        Assert.False(adiamento.PorComportamento);
    }

    [Theory]
    [InlineData("vou ver com meu socio")]
    [InlineData("preciso consultar a diretoria")]
    [InlineData("me manda por escrito que eu avalio")]
    [InlineData("depois eu te retorno")]
    public void As_formas_de_adiar_sao_reconhecidas(string adiamento)
    {
        var conversa = Com(
            (Autor.Cliente, "quanto custa?", 0),
            (Autor.Vendedor, "mando a proposta", 5),
            (Autor.Cliente, adiamento, 10));

        Assert.Contains(
            PadraoDeConversa.Detectar(conversa, Inicio.AddMinutes(20)),
            o => o.Tipo == TipoDeObjecao.Timing);
    }

    [Fact]
    public void Respostas_que_encurtam_sao_objecao_mesmo_sem_palavra_nenhuma()
    {
        // O sinal que o vendedor NAO ve sozinho: ninguem percebe que as
        // respostas do cliente cairam pela metade ao longo de tres dias.
        var conversa = Com(
            (Autor.Cliente, "boa tarde, voces atendem empresa de medio porte tambem?", 0),
            (Autor.Cliente, "seria pra um escritorio com uns trinta litros por semana", 2),
            (Autor.Vendedor, "atendemos sim, monto um plano mensal", 10),
            (Autor.Cliente, "ta", 30),
            (Autor.Cliente, "sei", 60));

        var encurtou = Assert.Single(
            PadraoDeConversa.Detectar(conversa, Inicio.AddMinutes(70)),
            o => o.PorComportamento && o.Descricao.StartsWith("as respostas encurtaram"));

        Assert.Equal("sei", encurtou.TrechoCitado);
    }

    [Fact]
    public void Conversa_que_mantem_o_engajamento_nao_vira_objecao()
    {
        // Falso positivo aqui e caro: o vendedor perde tempo tratando objecao
        // que nao existe, e para de confiar no dossie.
        var conversa = Com(
            (Autor.Cliente, "boa tarde, voces atendem empresa?", 0),
            (Autor.Cliente, "seria pra um escritorio, uns trinta litros", 2),
            (Autor.Vendedor, "atendemos sim", 10),
            (Autor.Cliente, "otimo, e qual seria o valor mensal disso?", 15),
            (Autor.Cliente, "e o prazo de entrega costuma ser de quantos dias?", 17));

        Assert.Empty(PadraoDeConversa.Detectar(conversa, Inicio.AddMinutes(20)));
    }

    [Fact]
    public void Conversa_curta_nao_gera_tendencia()
    {
        // Duas falas nao sao tendencia, sao duas falas.
        var conversa = Com(
            (Autor.Cliente, "boa tarde, tudo bem com voces por ai hoje?", 0),
            (Autor.Cliente, "ok", 5));

        Assert.DoesNotContain(
            PadraoDeConversa.Detectar(conversa, Inicio.AddMinutes(10)),
            o => o.Descricao.StartsWith("as respostas encurtaram"));
    }

    [Fact]
    public void Sumico_vira_objecao_e_cita_a_ultima_fala_do_cliente()
    {
        // A citacao e a frase DEPOIS da qual o silencio comecou — e o que o
        // vendedor precisa reler para entender o que aconteceu.
        var conversa = Com(
            (Autor.Cliente, "quanto sai o plano?", 0),
            (Autor.Vendedor, "mando agora", 5),
            (Autor.Cliente, "recebi, obrigado", 10),
            (Autor.Vendedor, "conseguiu ver?", 120));

        var sumico = Assert.Single(
            PadraoDeConversa.Detectar(conversa, Inicio.AddDays(4)),
            o => o.Descricao.StartsWith("sem responder ha"));

        Assert.Equal("recebi, obrigado", sumico.TrechoCitado);
        Assert.True(sumico.PorComportamento);
    }

    [Fact]
    public void Silencio_curto_nao_e_sumico()
    {
        // Cliente ocupado por algumas horas nao e cliente perdido, e acusar
        // isso encheria a tela de alarme falso.
        var conversa = Com(
            (Autor.Cliente, "quanto sai o plano?", 0),
            (Autor.Vendedor, "mando agora", 5));

        Assert.DoesNotContain(
            PadraoDeConversa.Detectar(conversa, Inicio.AddHours(6)),
            o => o.Descricao.StartsWith("sem responder ha"));
    }

    [Fact]
    public void O_marcador_de_audio_nao_conta_como_fala_curta_do_cliente()
    {
        // "[audio nao transcrito, 14s]" e texto NOSSO. Medir o marcador seria
        // medir a nossa escrita como se fosse a do cliente — e um audio longo
        // apareceria como resposta curta, invertendo a leitura.
        var conversa = new Conversa(Guid.NewGuid(), Guid.NewGuid());
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente,
            "boa tarde, voces atendem empresa de medio porte tambem?", Inicio));
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente,
            "seria pra um escritorio com uns trinta litros por semana", Inicio.AddMinutes(2)));
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, "",
            Inicio.AddMinutes(5), new Midia(TipoDeMidia.Audio, TimeSpan.FromMinutes(3))));
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente,
            "e ai, o que acha de fecharmos ainda essa semana mesmo?", Inicio.AddMinutes(8)));

        Assert.DoesNotContain(
            PadraoDeConversa.Detectar(conversa, Inicio.AddMinutes(10)),
            o => o.Descricao.StartsWith("as respostas encurtaram"));
    }

    [Fact]
    public void Objecao_sem_citacao_nao_existe()
    {
        Assert.Throws<ArgumentException>(() => new Objecao(
            TipoDeObjecao.Preco, "achou caro", "  ", Guid.NewGuid(), false));

        Assert.Throws<ArgumentException>(() => new Objecao(
            TipoDeObjecao.Preco, "achou caro", "ta caro", Guid.Empty, false));
    }

    [Fact]
    public void Tipo_que_nao_da_para_dizer_fica_NaoClassificada()
    {
        // Chutar "preco" porque e o mais comum mandaria o vendedor defender
        // valor quando o problema era que ele nem falava com quem decide.
        var conversa = Com(
            (Autor.Cliente, "quanto sai o plano mensal pra trinta litros por semana?", 0),
            (Autor.Cliente, "seria pra um escritorio no centro, com doze pessoas", 2),
            (Autor.Vendedor, "mando a proposta", 5),
            (Autor.Cliente, "ta", 30),
            (Autor.Cliente, "sei", 60));

        Assert.Contains(
            PadraoDeConversa.Detectar(conversa, Inicio.AddMinutes(70)),
            o => o.Tipo == TipoDeObjecao.NaoClassificada);
    }
}
