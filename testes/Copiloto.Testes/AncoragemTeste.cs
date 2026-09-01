using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Dossies;
using Copiloto.Dominio.Planos;

namespace Copiloto.Testes;

/// <summary>
/// As duas regras que o CLAUDE.md diz que nao se negociam, e o teste que as
/// torna verificaveis.
/// </summary>
public class AncoragemTeste
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Sinal_sem_mensagem_de_origem_nao_existe()
    {
        // "Todo sinal do dossie cita a fala que o originou." Como parametro
        // obrigatorio, a violacao nao chega a virar linha na tela.
        Assert.Throws<ArgumentException>(
            () => new Sinal("preco citado 3x", Guid.Empty, "puxado hein"));
    }

    [Fact]
    public void Sinal_sem_o_trecho_citado_nao_existe()
    {
        // O id sozinho nao serve: o vendedor le a frase na tela, sem abrir a
        // conversa inteira para procurar do que a IA estava falando.
        Assert.Throws<ArgumentException>(
            () => new Sinal("objecao velada", Guid.NewGuid(), "   "));
    }

    [Fact]
    public void Tatica_que_exige_dado_nao_vira_sugestao_sem_ancora()
    {
        // Sugerir "restam 2 unidades" quando existem 200 e publicidade enganosa.
        var erro = Assert.Throws<ArgumentException>(
            () => BlocoSugerido.Ancorado(Tatica.Escassez, "restam 2 unidades", ""));

        Assert.Contains("Perguntar", erro.Message);
    }

    [Fact]
    public void Sem_dado_a_tatica_vira_pergunta_ao_vendedor()
    {
        var bloco = BlocoSugerido.Perguntar(
            Tatica.Escassez, "Temos estoque baixo desse cafe? Se sim, quanto?");

        Assert.True(bloco.EhPergunta);
        Assert.Null(bloco.Ancora);
    }

    [Fact]
    public void Com_dado_do_CRM_a_sugestao_sai_ancorada()
    {
        var bloco = BlocoSugerido.Ancorado(
            Tatica.Escassez, "restam 2 unidades", "estoque=2 em 01/09");

        Assert.False(bloco.EhPergunta);
        Assert.Equal("estoque=2 em 01/09", bloco.Ancora);
    }

    [Fact]
    public void Tatica_livre_nao_precisa_de_ancora()
    {
        var bloco = BlocoSugerido.Ancorado(Tatica.Livre, "perguntar sobre o prazo", "");

        Assert.False(bloco.EhPergunta);
    }

    [Fact]
    public void Playbook_vazio_autoriza_tudo()
    {
        // Empresa que ainda nao configurou nada nao pode receber produto mudo.
        var playbook = new Playbook(Guid.NewGuid(), "padrao");

        Assert.True(playbook.Autoriza(Tatica.Desconto));
    }

    [Fact]
    public void Playbook_configurado_restringe_ao_que_a_empresa_permitiu()
    {
        var playbook = new Playbook(Guid.NewGuid(), "casa");
        playbook.Permitir(Tatica.ProvaSocial);

        Assert.True(playbook.Autoriza(Tatica.ProvaSocial));
        Assert.False(playbook.Autoriza(Tatica.Desconto));
    }

    [Fact]
    public void Conversa_ordena_por_envio_e_nao_por_chegada()
    {
        // Celular sem sinal entrega fora de ordem, e o dossie que le "vou pensar"
        // antes de "qual o valor?" entende a conversa ao contrario.
        var conversa = new Conversa(Guid.NewGuid(), Guid.NewGuid());
        var depois = new Mensagem(Guid.NewGuid(), Autor.Cliente, "vou pensar", Agora.AddMinutes(5));
        var antes = new Mensagem(Guid.NewGuid(), Autor.Cliente, "qual o valor?", Agora);

        conversa.Registrar(depois);
        conversa.Registrar(antes);

        Assert.Equal("qual o valor?", conversa.Mensagens[0].Texto);
        Assert.Equal("vou pensar", conversa.Mensagens[1].Texto);
    }

    [Fact]
    public void Reentrega_do_webhook_nao_duplica_mensagem()
    {
        var conversa = new Conversa(Guid.NewGuid(), Guid.NewGuid());
        var m = new Mensagem(Guid.NewGuid(), Autor.Cliente, "bom dia", Agora);

        conversa.Registrar(m);
        conversa.Registrar(m);

        Assert.Single(conversa.Mensagens);
    }

    [Fact]
    public void Silencio_do_cliente_conta_da_ultima_fala_dele()
    {
        // O "sumiu ha 4 dias" da tela. Fala do vendedor nao zera o silencio:
        // quem parou de responder foi o cliente.
        var conversa = new Conversa(Guid.NewGuid(), Guid.NewGuid());
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, "vou pensar", Agora));
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Vendedor, "claro!", Agora.AddDays(1)));

        Assert.Equal(TimeSpan.FromDays(4), conversa.SilencioDoCliente(Agora.AddDays(4)));
    }
}
