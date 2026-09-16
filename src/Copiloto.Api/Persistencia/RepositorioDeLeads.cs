using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;

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
        _porTelefone.GetValueOrDefault(telefoneNormalizado)
        ?? _porTelefone.Values.FirstOrDefault(l => l.FalaPor(telefoneNormalizado));

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

    /// <summary>
    /// Procura no numero principal e nos OUTROS numeros da mesma pessoa (#177).
    ///
    /// As duas consultas sao separadas de proposito: a primeira usa o indice
    /// unico e responde quase toda chamada; a segunda le a coluna JSON e so roda
    /// quando a primeira nao achou. Um `OR` unico jogaria fora o indice em todo
    /// acesso para atender o caso raro.
    /// </summary>
    public Lead? PorTelefone(string telefoneNormalizado) =>
        _ctx.Leads.FirstOrDefault(l => l.Telefone == telefoneNormalizado)
        ?? _ctx.Leads.FirstOrDefault(l => EF.Property<List<string>>(l, "_outrosNumeros")
                                            .Contains(telefoneNormalizado));

    public void Adicionar(Lead lead)
    {
        _ctx.Leads.Add(lead);
        _ctx.SaveChanges();
    }

    public int Quantos => _ctx.Leads.Count();
}
