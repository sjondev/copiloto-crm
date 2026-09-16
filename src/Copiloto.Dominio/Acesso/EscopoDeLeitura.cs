using Copiloto.Dominio.Vendas;

namespace Copiloto.Dominio.Acesso;

/// <summary>
/// Quem pode ver o dado de quem (#49).
///
/// A regra mora no dominio, e nao no controller, pelo mesmo motivo das
/// transicoes do Deal: regra em controller so e alcancada por teste que sobe a
/// aplicacao, e a que decide se um vendedor le a carteira do colega precisa ser
/// alcancavel por teste barato — senao ela e conferida uma vez, no dia em que
/// foi escrita.
/// </summary>
public static class EscopoDeLeitura
{
    /// <summary>
    /// Gestor ve tudo. Vendedor ve o que e dele e o que ainda nao tem dono.
    ///
    /// O lead SEM dono ser visivel e decisao de produto, nao descuido: ele
    /// chega pelo WhatsApp sem atribuicao, e esconde-lo ate alguem assumir
    /// significaria que a primeira mensagem de um cliente novo nao aparece para
    /// ninguem. O preco e que a fila de entrada e comum — e ela e comum de
    /// verdade, porque qualquer um pode assumir.
    /// </summary>
    public static bool PodeVer(Usuario usuario, Lead lead)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        ArgumentNullException.ThrowIfNull(lead);

        if (usuario.EhGestor) return true;

        return lead.VendedorId is null || lead.VendedorId == usuario.Id;
    }

    /// <summary>
    /// O filtro equivalente, para a consulta nao trazer o que sera descartado
    /// depois. Filtrar em memoria funcionaria e seria pior: o dado do outro
    /// vendedor teria saido do banco, passado pela rede e chegado ao processo —
    /// tres lugares a mais onde ele pode aparecer num log.
    /// </summary>
    public static IQueryable<Lead> Visiveis(IQueryable<Lead> leads, Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        return usuario.EhGestor
            ? leads
            : leads.Where(l => l.VendedorId == null || l.VendedorId == usuario.Id);
    }
}
