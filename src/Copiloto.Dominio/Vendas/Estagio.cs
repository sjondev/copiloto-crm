namespace Copiloto.Dominio.Vendas;

/// <summary>
/// O funil, na ordem em que a venda anda.
///
/// A ordem numerica importa: e por ela que <see cref="Deal"/> decide o que e
/// avanco e o que e recuo, em vez de uma tabela de pares permitidos que teria
/// de crescer ao quadrado a cada estagio novo.
/// </summary>
public enum Estagio
{
    Novo = 0,
    Qualificacao = 1,
    Proposta = 2,
    Negociacao = 3,
    Ganho = 4,
    Perdido = 5,
}
