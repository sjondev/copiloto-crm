using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Vendas;

namespace Copiloto.Api.Ingestao;

/// <summary>
/// Transforma o payload bruto em Lead + falante (#22).
///
/// A resolucao e por telefone NORMALIZADO, nunca pela string que chegou: o
/// mesmo cliente manda mensagem do celular novo e do numero antigo de oito
/// digitos, e comparar texto cru faz virar dois leads com o historico partido
/// no meio.
/// </summary>
public class ResolvedorDeLead
{
    private readonly NumerosDaEmpresa _numeros;
    private readonly IRepositorioDeLeads _leads;

    public ResolvedorDeLead(NumerosDaEmpresa numeros, IRepositorioDeLeads? leads = null)
    {
        ArgumentNullException.ThrowIfNull(numeros);

        // Em memoria por padrao: e o que mantem a suite e a demo rodando sem
        // Postgres, como o CLAUDE.md manda.
        _leads = leads ?? new LeadsEmMemoria();
        _numeros = numeros;
    }

    /// <summary>
    /// Um aparelho so. Continua existindo porque e o caso de quem ja rodava
    /// antes da #175, e porque a demo sobe com um numero.
    /// </summary>
    public ResolvedorDeLead(string numeroDaEmpresa, IRepositorioDeLeads? leads = null)
        : this(NumerosDaEmpresa.De(numeroDaEmpresa), leads)
    {
    }

    /// <summary>
    /// Quem falou. Sai de o remetente estar ou nao entre os numeros da empresa,
    /// e nao de um campo do provedor — a mesma rotina serve para mensagem que
    /// entra e que sai, em qualquer um dos aparelhos conectados.
    /// </summary>
    public Autor QuemFalou(Telefone remetente) =>
        _numeros.Contem(remetente) ? Autor.Vendedor : Autor.Cliente;

    /// <summary>
    /// O telefone do CLIENTE na troca, seja ele quem enviou ou quem recebeu.
    /// E' ele que identifica a conversa nos dois sentidos.
    /// </summary>
    public Telefone? TelefoneDoCliente(MensagemRecebida bruta)
    {
        ArgumentNullException.ThrowIfNull(bruta);

        var de = Telefone.Normalizar(bruta.De);
        var para = Telefone.Normalizar(bruta.Para);
        if (de is null || para is null) return null;

        // Uma das pontas precisa ser NOSSA. Antes o cliente saia por eliminacao
        // — quem nao era a empresa era o cliente —, e uma troca em que nenhuma
        // ponta fosse nossa criava lead do remetente: duas pessoas de fora
        // conversando viravam cliente da empresa.
        if (_numeros.Contem(de)) return para;
        if (_numeros.Contem(para)) return de;

        return null;
    }

    /// <summary>
    /// O vendedor que atende no aparelho que participou desta troca, ou
    /// <c>null</c> quando o numero nao tem dono declarado.
    /// </summary>
    public Guid? VendedorDaTroca(MensagemRecebida bruta)
    {
        ArgumentNullException.ThrowIfNull(bruta);

        var de = Telefone.Normalizar(bruta.De);
        var para = Telefone.Normalizar(bruta.Para);

        return (de is not null && _numeros.Contem(de)) ? _numeros.DonoDe(de)
            : (para is not null && _numeros.Contem(para)) ? _numeros.DonoDe(para)
            : null;
    }

    /// <summary>
    /// O Lead daquele telefone, criando na hora se for a primeira vez.
    ///
    /// Telefone desconhecido cria Lead: no WhatsApp nao existe cadastro previo,
    /// e um Lead que so passa a existir depois de alguem preencher formulario e
    /// um Lead que nunca existe.
    /// </summary>
    public Lead Resolver(Telefone telefone, DateTimeOffset quando, Guid? vendedorId = null)
    {
        ArgumentNullException.ThrowIfNull(telefone);

        var existente = _leads.PorTelefone(telefone.E164);
        if (existente is not null)
        {
            // NAO reatribui. O cliente que escreve para o aparelho do Bruno
            // depois de a Ana ter assumido continua sendo da Ana: `Assumir`
            // recusa e devolve o motivo, e quem libera e ela (#49). Aparelho
            // nao rouba lead.
            return existente;
        }

        var novo = new Lead(Guid.NewGuid(), telefone.E164, quando);
        if (vendedorId is { } dono) novo.Assumir(dono);

        _leads.Adicionar(novo);
        return novo;
    }

    public int LeadsConhecidos => _leads.Quantos;
}
