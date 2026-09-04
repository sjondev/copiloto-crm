using System.Reflection;
using Copiloto.Dominio.Conversas;

namespace Copiloto.Testes;

/// <summary>
/// A conversa lida do BANCO tambem esta em ordem (#136).
///
/// O defeito que originou este arquivo nao levantava excecao: `UltimaDoCliente`
/// usava `LastOrDefault`, que so vale enquanto a lista foi montada por
/// `Registrar`. Materializada pelo ORM, a colecao vem na ordem do provedor — a
/// chave primaria e Guid — e "a ultima fala do cliente" podia ser a primeira.
/// O sintoma era um numero errado e plausivel na tela: 9 dias de silencio onde
/// eram 8.
/// </summary>
public class OrdemDasMensagensTeste
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Enche a colecao PELO CAMPO, sem passar por `Registrar`.
    ///
    /// E exatamente o que o EF Core faz ao materializar: escreve no backing
    /// field, na ordem em que a consulta devolveu. Por isso o teste usa
    /// reflexao — encenar a materializacao em memoria e o unico jeito
    /// deterministico de reproduzir a ordem embaralhada, que num banco de
    /// verdade varia de execucao para execucao.
    /// </summary>
    private static Conversa MaterializadaForaDeOrdem(params Mensagem[] mensagens)
    {
        var conversa = new Conversa(Guid.NewGuid(), Guid.NewGuid());

        var campo = typeof(Conversa)
            .GetField("_mensagens", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var lista = (List<Mensagem>)campo.GetValue(conversa)!;
        lista.AddRange(mensagens);

        return conversa;
    }

    [Fact]
    public void A_ultima_fala_do_cliente_sai_por_data_e_nao_por_posicao()
    {
        var recente = new Mensagem(Guid.NewGuid(), Autor.Cliente, "vou pensar", T0.AddDays(-8));
        var antiga = new Mensagem(Guid.NewGuid(), Autor.Cliente, "qual o valor do kg?", T0.AddDays(-9));

        // A antiga POR ULTIMO, como o banco pode devolver.
        var conversa = MaterializadaForaDeOrdem(recente, antiga);

        Assert.Equal("vou pensar", conversa.UltimaDoCliente!.Texto);
    }

    [Fact]
    public void O_silencio_conta_da_fala_certa_mesmo_com_a_lista_embaralhada()
    {
        // O numero que aparece na tela do vendedor e no alerta do Vigia (#53).
        var conversa = MaterializadaForaDeOrdem(
            new Mensagem(Guid.NewGuid(), Autor.Cliente, "vou pensar", T0.AddDays(-8)),
            new Mensagem(Guid.NewGuid(), Autor.Vendedor, "claro!", T0.AddDays(-7)),
            new Mensagem(Guid.NewGuid(), Autor.Cliente, "qual o valor?", T0.AddDays(-9)));

        Assert.Equal(TimeSpan.FromDays(8), conversa.SilencioDoCliente(T0));
    }

    [Fact]
    public void As_mensagens_saem_em_ordem_mesmo_vindo_embaralhadas()
    {
        var conversa = MaterializadaForaDeOrdem(
            new Mensagem(Guid.NewGuid(), Autor.Cliente, "vou pensar", T0.AddDays(-8)),
            new Mensagem(Guid.NewGuid(), Autor.Cliente, "qual o valor?", T0.AddDays(-9)),
            new Mensagem(Guid.NewGuid(), Autor.Vendedor, "R$ 68", T0.AddDays(-9).AddMinutes(4)));

        var textos = conversa.Mensagens.Select(m => m.Texto).ToList();

        Assert.Equal(["qual o valor?", "R$ 68", "vou pensar"], textos);
    }

    [Fact]
    public void Fala_do_vendedor_nao_vira_a_ultima_do_cliente()
    {
        // Quem parou de responder foi o cliente: a fala do vendedor depois dela
        // nao zera o silencio.
        var conversa = MaterializadaForaDeOrdem(
            new Mensagem(Guid.NewGuid(), Autor.Vendedor, "oi?", T0),
            new Mensagem(Guid.NewGuid(), Autor.Cliente, "vou pensar", T0.AddDays(-8)));

        Assert.Equal("vou pensar", conversa.UltimaDoCliente!.Texto);
    }

    [Fact]
    public void Conversa_sem_fala_do_cliente_nao_tem_silencio()
    {
        var conversa = MaterializadaForaDeOrdem(
            new Mensagem(Guid.NewGuid(), Autor.Vendedor, "bom dia!", T0));

        Assert.Null(conversa.UltimaDoCliente);
        Assert.Null(conversa.SilencioDoCliente(T0));
    }

    [Fact]
    public void O_caminho_do_webhook_continua_valendo()
    {
        // A ordenacao na leitura nao substitui a do Registrar: as duas origens
        // precisam dar o mesmo resultado.
        var conversa = new Conversa(Guid.NewGuid(), Guid.NewGuid());
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, "vou pensar", T0.AddDays(-8)));
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, "qual o valor?", T0.AddDays(-9)));

        Assert.Equal("qual o valor?", conversa.Mensagens[0].Texto);
        Assert.Equal("vou pensar", conversa.UltimaDoCliente!.Texto);
    }
}
