namespace Copiloto.Dominio.Vendas;

/// <summary>
/// De que lado da mesa esta a pessoa do outro lado da conversa (#85).
///
/// Dado de parceiro tambem e dado pessoal — o contato na empresa parceira e
/// uma pessoa fisica, ainda que a relacao seja B2B —, mas o motivo de separar
/// nao e so esse: conversa de fornecimento carrega margem, custo e condicao
/// que nao podem sair perto de um comprador.
/// </summary>
public enum Relacao
{
    /// <summary>Compra da empresa. O caso comum.</summary>
    Cliente = 0,

    /// <summary>Fornecedor, transportadora, representante: vende PARA a empresa.</summary>
    Parceiro = 1,
}
