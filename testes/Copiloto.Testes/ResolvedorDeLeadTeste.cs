using Copiloto.Api.Ingestao;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Vendas;

namespace Copiloto.Testes;

/// <summary>
/// Resolucao de Lead por telefone e identificacao do falante (#22).
/// </summary>
public class ResolvedorDeLeadTeste
{
    private const string Empresa = "+55 11 3333-4444";
    private const string Cliente = "+55 11 98765-4321";
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static ResolvedorDeLead Novo() => new(Empresa);

    private static MensagemRecebida Fala(string de, string para) =>
        new("wamid.1", de, para, "qual o valor?", Agora);

    [Fact]
    public void Mensagem_que_chega_e_do_cliente()
    {
        var r = Novo();
        Assert.Equal(Autor.Cliente, r.QuemFalou(Telefone.Normalizar(Cliente)!));
    }

    [Fact]
    public void Mensagem_que_sai_e_do_vendedor()
    {
        // O outro sentido, e ele importa: sem isto a conversa que o vendedor
        // iniciou entraria no historico como se o cliente tivesse falado primeiro.
        var r = Novo();
        Assert.Equal(Autor.Vendedor, r.QuemFalou(Telefone.Normalizar(Empresa)!));
    }

    [Fact]
    public void O_cliente_e_o_mesmo_nos_dois_sentidos()
    {
        var r = Novo();

        var entrando = r.TelefoneDoCliente(Fala(Cliente, Empresa));
        var saindo = r.TelefoneDoCliente(Fala(Empresa, Cliente));

        Assert.Equal(entrando, saindo);
        Assert.Equal("+5511987654321", entrando!.E164);
    }

    [Fact]
    public void Telefone_desconhecido_cria_o_Lead()
    {
        // No WhatsApp nao existe cadastro previo: um Lead que so passa a existir
        // depois de alguem preencher formulario e um Lead que nunca existe.
        var r = Novo();

        var lead = r.Resolver(Telefone.Normalizar(Cliente)!, Agora);

        Assert.Equal(1, r.LeadsConhecidos);
        Assert.Equal("+5511987654321", lead.Telefone);
    }

    [Fact]
    public void O_mesmo_numero_em_formatos_diferentes_e_UM_lead_so()
    {
        // O bug que a issue existe para evitar: o historico partido no meio.
        var r = Novo();

        var a = r.Resolver(Telefone.Normalizar("(11) 98765-4321")!, Agora);
        var b = r.Resolver(Telefone.Normalizar("5511987654321")!, Agora);
        var c = r.Resolver(Telefone.Normalizar("11 8765-4321")!, Agora);   // sem o nono digito

        Assert.Equal(1, r.LeadsConhecidos);
        Assert.Equal(a.Id, b.Id);
        Assert.Equal(a.Id, c.Id);
    }

    [Fact]
    public void Numero_irreconhecivel_devolve_null_em_vez_de_criar_lead_torto()
    {
        var r = Novo();

        Assert.Null(r.TelefoneDoCliente(Fala("123", Empresa)));
        Assert.Equal(0, r.LeadsConhecidos);
    }

    [Fact]
    public void Empresa_com_numero_invalido_nao_sobe()
    {
        // Falhar na construcao e melhor que classificar todo mundo como cliente:
        // o erro apareceria como "o vendedor nunca fala", tres camadas adiante.
        Assert.Throws<ArgumentException>(() => new ResolvedorDeLead("nao e telefone"));
    }
}
