using Copiloto.Dominio.Conversas;

namespace Copiloto.Testes;

/// <summary>
/// Baloes seguidos sao UMA fala (#19).
///
/// Ninguem escreve paragrafo no WhatsApp. Tratar cada balao como fala separada
/// gera N analises, N custos e um dossie que muda de opiniao a cada segundo.
/// </summary>
public class AgrupadorDeFalasTeste
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static Mensagem Balao(Autor autor, string texto, int segundos) =>
        new(Guid.NewGuid(), autor, texto, T0.AddSeconds(segundos));

    [Fact]
    public void Seis_baloes_em_quatro_segundos_viram_uma_fala()
    {
        // O criterio de aceite, com o cenario do café da issue.
        var baloes = new[]
        {
            Balao(Autor.Cliente, "bom dia", 0),
            Balao(Autor.Cliente, "vi o cafe de voces", 1),
            Balao(Autor.Cliente, "o bourbon", 2),
            Balao(Autor.Cliente, "qual o valor do kg?", 3),
            Balao(Autor.Cliente, "e o frete", 3),
            Balao(Autor.Cliente, "pra SP", 4),
        };

        var falas = AgrupadorDeFalas.Agrupar(baloes);

        Assert.Single(falas);
        Assert.Equal(6, falas[0].Baloes.Count);
    }

    [Fact]
    public void O_texto_da_fala_junta_os_baloes_na_ordem()
    {
        // "o bourbon" sozinho nao diz nada; junto, diz tudo.
        var falas = AgrupadorDeFalas.Agrupar(new[]
        {
            Balao(Autor.Cliente, "vi o cafe de voces", 0),
            Balao(Autor.Cliente, "o bourbon", 1),
        });

        Assert.Equal("vi o cafe de voces\no bourbon", falas[0].Texto);
    }

    [Fact]
    public void O_instante_da_fala_e_o_do_ultimo_balao()
    {
        // A fala so esta completa quando o silencio confirma que acabou. Usar a
        // primeira faria o "sumiu ha 4 dias" contar a partir do "bom dia".
        var falas = AgrupadorDeFalas.Agrupar(new[]
        {
            Balao(Autor.Cliente, "bom dia", 0),
            Balao(Autor.Cliente, "vou pensar", 5),
        });

        Assert.Equal(T0.AddSeconds(5), falas[0].Quando);
    }

    [Fact]
    public void Silencio_maior_que_a_janela_abre_outra_fala()
    {
        var falas = AgrupadorDeFalas.Agrupar(new[]
        {
            Balao(Autor.Cliente, "qual o valor?", 0),
            Balao(Autor.Cliente, "vou pensar", 60),
        });

        Assert.Equal(2, falas.Count);
    }

    [Fact]
    public void Troca_de_falante_quebra_mesmo_dentro_da_janela()
    {
        // O vendedor respondendo no meio encerra a fala do cliente, ainda que a
        // resposta venha em um segundo.
        var falas = AgrupadorDeFalas.Agrupar(new[]
        {
            Balao(Autor.Cliente, "qual o valor?", 0),
            Balao(Autor.Vendedor, "bom dia! o Bourbon sai a", 1),
            Balao(Autor.Cliente, "puxado hein", 2),
        });

        Assert.Equal(3, falas.Count);
        Assert.Equal(Autor.Cliente, falas[0].Autor);
        Assert.Equal(Autor.Vendedor, falas[1].Autor);
        Assert.Equal(Autor.Cliente, falas[2].Autor);
    }

    [Fact]
    public void A_janela_e_configuravel()
    {
        // Conversa de suporte tem outro ritmo que conversa de venda.
        var baloes = new[]
        {
            Balao(Autor.Cliente, "a", 0),
            Balao(Autor.Cliente, "b", 30),
        };

        Assert.Equal(2, AgrupadorDeFalas.Agrupar(baloes).Count);
        Assert.Single(AgrupadorDeFalas.Agrupar(baloes, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Baloes_fora_de_ordem_sao_ordenados_antes_de_agrupar()
    {
        // Celular sem sinal entrega fora de ordem, e agrupar na ordem errada
        // juntaria baloes que estao a minutos de distancia.
        var falas = AgrupadorDeFalas.Agrupar(new[]
        {
            Balao(Autor.Cliente, "segundo", 1),
            Balao(Autor.Cliente, "primeiro", 0),
        });

        Assert.Single(falas);
        Assert.Equal("primeiro\nsegundo", falas[0].Texto);
    }

    [Fact]
    public void Conversa_vazia_nao_produz_fala()
    {
        Assert.Empty(AgrupadorDeFalas.Agrupar(Array.Empty<Mensagem>()));
    }

    [Fact]
    public void A_fala_guarda_os_ids_para_o_sinal_poder_citar_o_balao_exato()
    {
        // O dossie cita a FALA que originou o sinal, e a fala tem varios baloes:
        // sem os ids, a citacao apontaria para o bloco inteiro em vez da frase.
        var baloes = new[] { Balao(Autor.Cliente, "a", 0), Balao(Autor.Cliente, "b", 1) };

        var fala = AgrupadorDeFalas.Agrupar(baloes)[0];

        Assert.Equal(baloes.Select(b => b.Id), fala.IdsDosBaloes);
    }
}
