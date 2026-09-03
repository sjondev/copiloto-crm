using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Fichas;
using Copiloto.Dominio.Planos;

namespace Copiloto.Testes;

/// <summary>
/// A abordagem fria (#87), que precisa de MAIS cuidado que a continuidade.
///
/// Na continuidade o vendedor descarta a sugestao ruim e a conversa segue.
/// Aqui o erro vai para o cliente, e nao ha segunda mensagem inicial.
/// </summary>
public class AbordagemInicialTeste
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    private static FichaCliente FichaVazia() => new(Guid.NewGuid(), Guid.NewGuid(), T0);

    private static Plano Montar(FichaCliente? ficha) =>
        AbordagemInicial.Montar(Guid.NewGuid(), Guid.NewGuid(), ficha, T0);

    [Fact]
    public void Sem_conversa_o_copiloto_muda_de_modo()
    {
        Assert.Equal(ModoDoCopiloto.AbordagemInicial, AbordagemInicial.ModoPara(null));

        var vazia = new Conversa(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(ModoDoCopiloto.AbordagemInicial, AbordagemInicial.ModoPara(vazia));
    }

    [Fact]
    public void Com_uma_mensagem_ja_e_continuidade()
    {
        var conversa = new Conversa(Guid.NewGuid(), Guid.NewGuid());
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, "bom dia", T0));

        Assert.Equal(ModoDoCopiloto.Continuidade, AbordagemInicial.ModoPara(conversa));
    }

    [Fact]
    public void Ficha_vazia_nao_produz_mensagem_pronta()
    {
        // O criterio inegociavel. Copiloto que gera saudacao generica com ficha
        // vazia e PIOR que copiloto nenhum: da ao vendedor a falsa sensacao de
        // que a mensagem foi pensada.
        var plano = Montar(FichaVazia());

        Assert.NotEmpty(plano.Blocos);
        Assert.All(plano.Blocos, b => Assert.True(b.EhPergunta));
    }

    [Fact]
    public void Sem_ficha_nenhuma_o_comportamento_e_o_mesmo()
    {
        var plano = Montar(null);

        Assert.All(plano.Blocos, b => Assert.True(b.EhPergunta));
    }

    [Fact]
    public void Ficha_vazia_pede_informacao_especifica_e_nao_generica()
    {
        // "Me manda mais dados" nao ajuda ninguem: a pergunta cita o campo.
        var plano = Montar(FichaVazia());

        Assert.Contains(plano.Perguntas, p => p.Texto.Contains("ramo"));
    }

    [Fact]
    public void So_com_impressao_ainda_nao_ha_abordagem()
    {
        // "Parece desconfiado" nao sustenta a primeira frase que o cliente vai
        // ler na vida dele sobre esta empresa (#88).
        var ficha = FichaVazia();
        ficha.Atualizar(T0, pessoa: new SobreAPessoa(
            EstiloObservado: Anotacao.Impressao("parece desconfiado")));

        var plano = Montar(ficha);

        Assert.All(plano.Blocos, b => Assert.True(b.EhPergunta));
        Assert.Contains(plano.Perguntas, p => p.Texto.Contains("confirmar isso como fato"));
    }

    [Fact]
    public void Com_dois_fatos_saem_dois_angulos_diferentes()
    {
        var ficha = FichaVazia();
        ficha.Atualizar(T0,
            empresa: new SobreAEmpresa(
                Ramo: Anotacao.Fato("cafeteria de bairro"),
                Momento: Anotacao.Fato("abriu a segunda loja em agosto", "Instagram")));

        var angulos = Montar(ficha).Blocos.Where(b => !b.EhPergunta && b.Texto.StartsWith("Ângulo")).ToList();

        Assert.Equal(2, angulos.Count);
        Assert.NotEqual(angulos[0].Ancora, angulos[1].Ancora);
    }

    [Fact]
    public void Cada_angulo_fica_preso_ao_fato_que_o_sustenta()
    {
        var ficha = FichaVazia();
        ficha.Atualizar(T0, empresa: new SobreAEmpresa(
            Momento: Anotacao.Fato("abriu a segunda loja em agosto", "Instagram")));

        var angulo = Montar(ficha).Blocos.First(b => b.Texto.StartsWith("Ângulo"));

        Assert.Contains("segunda loja", angulo.Texto);
        Assert.Contains("fato, Instagram", angulo.Ancora);
    }

    [Fact]
    public void Com_um_fato_so_sai_um_angulo_e_um_pedido_do_segundo()
    {
        // Duas versoes do mesmo texto sao a aparencia de escolha sem a escolha.
        var ficha = FichaVazia();
        ficha.Atualizar(T0, empresa: new SobreAEmpresa(Ramo: Anotacao.Fato("cafeteria")));

        var plano = Montar(ficha);

        Assert.Single(plano.Blocos, b => b.Texto.StartsWith("Ângulo"));
        Assert.Contains(plano.Perguntas, p => p.Texto.Contains("segundo"));
    }

    [Fact]
    public void O_porque_agora_sai_de_um_fato_que_justifique_o_momento()
    {
        var ficha = FichaVazia();
        ficha.Atualizar(T0, empresa: new SobreAEmpresa(
            Momento: Anotacao.Fato("abriu a segunda loja em agosto")));

        var plano = Montar(ficha);

        var porQue = plano.Blocos.First(b => b.Texto.StartsWith("Por que agora"));
        Assert.False(porQue.EhPergunta);
        Assert.Contains("segunda loja", porQue.Texto);
    }

    [Fact]
    public void Fato_que_nao_justifica_o_momento_nao_vira_gancho()
    {
        // "É cafeteria" diz quem o cliente e, nao o que mudou nele. Sem gancho,
        // o contato nao tem motivo — e isso so da para mandar uma vez.
        var ficha = FichaVazia();
        ficha.Atualizar(T0, empresa: new SobreAEmpresa(Ramo: Anotacao.Fato("cafeteria")));

        var plano = Montar(ficha);

        Assert.Contains(plano.Perguntas, p => p.Texto.Contains("HOJE"));
    }

    [Fact]
    public void O_canal_sai_de_por_onde_o_lead_chegou()
    {
        var ficha = FichaVazia();
        ficha.Atualizar(T0, empresa: new SobreAEmpresa(
            ComoChegou: Anotacao.Fato("respondeu um story no Instagram")));

        var canal = Montar(ficha).Blocos.First(b => b.Texto.StartsWith("Canal"));

        Assert.False(canal.EhPergunta);
        Assert.Contains("Instagram", canal.Texto);
    }

    [Fact]
    public void Sem_dado_de_canal_e_de_horario_os_dois_viram_pergunta()
    {
        var ficha = FichaVazia();
        ficha.Atualizar(T0, empresa: new SobreAEmpresa(Ramo: Anotacao.Fato("cafeteria")));

        var plano = Montar(ficha);

        Assert.Contains(plano.Perguntas, p => p.Texto.Contains("canal"));
        Assert.Contains(plano.Perguntas, p => p.Texto.Contains("horário"));
    }

    [Fact]
    public void Nenhum_bloco_e_mensagem_pronta_para_o_cliente()
    {
        // A tese do produto: o vendedor copia, edita e manda. O plano entrega
        // ANGULO ancorado, e nao texto assinado por um robo.
        var ficha = FichaVazia();
        ficha.Atualizar(T0, empresa: new SobreAEmpresa(
            Ramo: Anotacao.Fato("cafeteria"),
            Momento: Anotacao.Fato("abriu a segunda loja em agosto")));

        var plano = Montar(ficha);

        Assert.DoesNotContain(plano.Blocos, b => b.Texto.Contains("Olá"));
        Assert.DoesNotContain(plano.Blocos, b => b.Texto.Contains("espero que esteja bem"));
        Assert.All(plano.Blocos.Where(b => !b.EhPergunta), b => Assert.NotNull(b.Ancora));
    }
}
