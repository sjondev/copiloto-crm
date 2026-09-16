using Copiloto.Api.Ingestao;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Vendas;

namespace Copiloto.Testes;

/// <summary>
/// Varios aparelhos da empresa conectados ao mesmo CRM (#175).
///
/// Ate aqui o resolvedor conhecia UM numero, e decidia quem falou por uma
/// comparacao so. Com dois vendedores conectados, a fala que o segundo mandou
/// pelo numero dele caia como Cliente — e o dossie leria a conversa ao
/// contrario, atribuindo ao cliente o que o vendedor disse. Este e o defeito
/// que os testes abaixo fecham.
/// </summary>
public class VariosNumerosTeste
{
    private const string NumeroDaAna = "+55 11 3333-4444";
    private const string NumeroDoBruno = "+55 11 3333-5555";
    private const string Cliente = "+55 11 98765-4321";

    private static readonly Guid Ana = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bruno = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Agora = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private static MensagemRecebida Fala(string de, string para) =>
        new("wamid.1", de, para, "qual o valor do kg?", Agora);

    private static Telefone Tel(string bruto) => Telefone.Normalizar(bruto)!;

    // --- Quem falou, com mais de um numero nosso ---

    [Fact]
    public void Os_dois_numeros_da_empresa_sao_vendedor()
    {
        var r = new ResolvedorDeLead(NumerosDaEmpresa.De(NumeroDaAna, NumeroDoBruno));

        Assert.Equal(Autor.Vendedor, r.QuemFalou(Tel(NumeroDaAna)));
        Assert.Equal(Autor.Vendedor, r.QuemFalou(Tel(NumeroDoBruno)));
        Assert.Equal(Autor.Cliente, r.QuemFalou(Tel(Cliente)));
    }

    [Fact]
    public void O_cliente_e_o_mesmo_venha_a_fala_por_qual_aparelho_for()
    {
        var r = new ResolvedorDeLead(NumerosDaEmpresa.De(NumeroDaAna, NumeroDoBruno));

        Assert.Equal(Tel(Cliente), r.TelefoneDoCliente(Fala(Cliente, NumeroDaAna)));
        Assert.Equal(Tel(Cliente), r.TelefoneDoCliente(Fala(NumeroDoBruno, Cliente)));
    }

    /// <summary>
    /// O caso que motivou a issue: sem isto, a resposta do Bruno entraria na
    /// conversa como fala do CLIENTE, e a leitura sairia invertida.
    /// </summary>
    [Fact]
    public void A_resposta_do_segundo_vendedor_nao_vira_fala_do_cliente()
    {
        var r = new ResolvedorDeLead(NumerosDaEmpresa.De(NumeroDaAna, NumeroDoBruno));

        Assert.Equal(Autor.Vendedor, r.QuemFalou(Tel(NumeroDoBruno)));
    }

    // --- Numero que nao e nosso ---

    /// <summary>
    /// Antes, qualquer troca em que nenhuma ponta fosse a empresa criava Lead
    /// do remetente: `QuemFalou` devolvia Cliente por ELIMINACAO. Duas pessoas
    /// de fora conversando viravam cliente nosso.
    /// </summary>
    [Fact]
    public void Troca_sem_nenhum_numero_nosso_nao_vira_lead()
    {
        var r = new ResolvedorDeLead(NumerosDaEmpresa.De(NumeroDaAna));

        Assert.Null(r.TelefoneDoCliente(Fala(Cliente, "+55 11 97777-6666")));
    }

    // --- O dono do numero vira dono do lead ---

    [Fact]
    public void O_lead_nasce_do_vendedor_que_atende_naquele_aparelho()
    {
        var r = new ResolvedorDeLead(NumerosDaEmpresa.ComDono(
            (NumeroDaAna, Ana), (NumeroDoBruno, Bruno)));

        var fala = Fala(Cliente, NumeroDoBruno);
        var lead = r.Resolver(r.TelefoneDoCliente(fala)!, Agora, r.VendedorDaTroca(fala));

        Assert.Equal(Bruno, lead.VendedorId);
    }

    [Fact]
    public void Numero_sem_dono_declarado_deixa_o_lead_com_a_equipe()
    {
        var r = new ResolvedorDeLead(NumerosDaEmpresa.De(NumeroDaAna));

        var fala = Fala(Cliente, NumeroDaAna);
        var lead = r.Resolver(r.TelefoneDoCliente(fala)!, Agora, r.VendedorDaTroca(fala));

        // Nulo NAO e "de ninguem": e "da equipe" (#49). O lead aparece para todo
        // mundo justamente porque ninguem o assumiu ainda.
        Assert.Null(lead.VendedorId);
    }

    /// <summary>
    /// O aparelho nao rouba. Se a Ana ja assumiu o lead e o cliente escreve para
    /// o numero do Bruno, o dono continua sendo a Ana — quem libera e ela (#49).
    /// </summary>
    [Fact]
    public void Falar_com_outro_aparelho_nao_transfere_o_lead()
    {
        var r = new ResolvedorDeLead(NumerosDaEmpresa.ComDono(
            (NumeroDaAna, Ana), (NumeroDoBruno, Bruno)));

        var primeira = Fala(Cliente, NumeroDaAna);
        r.Resolver(r.TelefoneDoCliente(primeira)!, Agora, r.VendedorDaTroca(primeira));

        var segunda = Fala(Cliente, NumeroDoBruno);
        var lead = r.Resolver(r.TelefoneDoCliente(segunda)!, Agora, r.VendedorDaTroca(segunda));

        Assert.Equal(Ana, lead.VendedorId);
    }

    // --- O que ja rodava continua rodando ---

    [Fact]
    public void Um_numero_so_continua_funcionando_como_antes()
    {
        var r = new ResolvedorDeLead(NumeroDaAna);

        Assert.Equal(Autor.Vendedor, r.QuemFalou(Tel(NumeroDaAna)));
        Assert.Equal(Autor.Cliente, r.QuemFalou(Tel(Cliente)));
        Assert.Equal(Tel(Cliente), r.TelefoneDoCliente(Fala(Cliente, NumeroDaAna)));
    }

    // --- O conjunto em si ---

    [Fact]
    public void Numero_invalido_na_lista_e_recusado_na_construcao()
    {
        // Numero torto aqui nao e detalhe: ele nunca casaria com fala nenhuma, e
        // o sintoma seria toda mensagem daquele aparelho virando fala do cliente.
        Assert.Throws<ArgumentException>(() => NumerosDaEmpresa.De("nao e telefone"));
    }

    [Fact]
    public void Conjunto_vazio_e_recusado()
    {
        Assert.Throws<ArgumentException>(() => NumerosDaEmpresa.De());
    }

    [Fact]
    public void O_mesmo_numero_escrito_de_dois_jeitos_e_um_so()
    {
        var numeros = NumerosDaEmpresa.De("+55 11 3333-4444", "551133334444");

        Assert.Equal(1, numeros.Quantidade);
    }
}
