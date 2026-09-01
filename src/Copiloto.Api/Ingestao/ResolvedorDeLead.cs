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
    private readonly Telefone _numeroDaEmpresa;
    private readonly Dictionary<string, Lead> _porTelefone = new();

    public ResolvedorDeLead(string numeroDaEmpresa)
    {
        _numeroDaEmpresa = Telefone.Normalizar(numeroDaEmpresa)
            ?? throw new ArgumentException(
                "O numero da empresa precisa ser um telefone brasileiro valido: e "
                + "ele que decide quem falou em cada mensagem.", nameof(numeroDaEmpresa));
    }

    /// <summary>
    /// Quem falou. Sai da comparacao com o numero da empresa, e nao de um campo
    /// do provedor — a mesma rotina serve para mensagem que entra e que sai.
    /// </summary>
    public Autor QuemFalou(Telefone remetente) =>
        remetente.Equals(_numeroDaEmpresa) ? Autor.Vendedor : Autor.Cliente;

    /// <summary>
    /// O telefone do CLIENTE na troca, seja ele quem enviou ou quem recebeu.
    /// E' ele que identifica a conversa nos dois sentidos.
    /// </summary>
    public Telefone? TelefoneDoCliente(MensagemRecebida bruta)
    {
        var de = Telefone.Normalizar(bruta.De);
        var para = Telefone.Normalizar(bruta.Para);
        if (de is null || para is null) return null;

        return QuemFalou(de) == Autor.Cliente ? de : para;
    }

    /// <summary>
    /// O Lead daquele telefone, criando na hora se for a primeira vez.
    ///
    /// Telefone desconhecido cria Lead: no WhatsApp nao existe cadastro previo,
    /// e um Lead que so passa a existir depois de alguem preencher formulario e
    /// um Lead que nunca existe.
    /// </summary>
    public Lead Resolver(Telefone telefone, DateTimeOffset quando)
    {
        if (_porTelefone.TryGetValue(telefone.E164, out var existente))
            return existente;

        var novo = new Lead(Guid.NewGuid(), telefone.E164, quando);
        _porTelefone[telefone.E164] = novo;
        return novo;
    }

    public int LeadsConhecidos => _porTelefone.Count;
}
