using Copiloto.Dominio.Vendas;

namespace Copiloto.Api.Persistencia;

/// <summary>
/// Onde os leads ficam.
///
/// A interface existe com duas implementacoes de verdade — a de banco e a de
/// memoria, que serve a suite e ao FakeSource —, e nao "para o caso de". O
/// gatilho do CLAUDE.md e claro: interface com uma implementacao so entra com
/// teste que a justifique.
/// </summary>
public interface IRepositorioDeLeads
{
    Lead? PorTelefone(string telefoneNormalizado);
    void Adicionar(Lead lead);
    int Quantos { get; }
}

/// <summary>Em memoria. E o padrao da suite e da demo offline.</summary>
public class LeadsEmMemoria : IRepositorioDeLeads
{
    private readonly Dictionary<string, Lead> _porTelefone = new();

    public Lead? PorTelefone(string telefoneNormalizado) =>
        _porTelefone.GetValueOrDefault(telefoneNormalizado);

    public void Adicionar(Lead lead) => _porTelefone[lead.Telefone] = lead;

    public int Quantos => _porTelefone.Count;
}

/// <summary>
/// Em banco. A unicidade real e o indice `ux_leads_telefone`, e nao este
/// codigo: duas instancias chegam aqui ao mesmo tempo, as duas leem "nao
/// existe", e so o banco recusa a segunda.
/// </summary>
public class LeadsNoBanco : IRepositorioDeLeads
{
    private readonly CopilotoDbContext _ctx;

    public LeadsNoBanco(CopilotoDbContext ctx) => _ctx = ctx;

    public Lead? PorTelefone(string telefoneNormalizado) =>
        _ctx.Leads.FirstOrDefault(l => l.Telefone == telefoneNormalizado);

    public void Adicionar(Lead lead)
    {
        _ctx.Leads.Add(lead);
        _ctx.SaveChanges();
    }

    public int Quantos => _ctx.Leads.Count();
}
