namespace Copiloto.Dominio.Dossies;

/// <summary>
/// Uma leitura da conversa, presa a fala que a originou.
///
/// A citacao e OBRIGATORIA no construtor, e nao um campo que alguem preenche
/// depois. E a regra que nao se negocia: "todo sinal do dossie cita a fala que o
/// originou; sem citacao, o bloco nao e exibido".
///
/// Torna-la obrigatoria aqui muda a natureza da regra. Como campo opcional, ela
/// depende de todo caminho de criacao lembrar de preencher, e o dia em que um
/// esquecer o sinal aparece na tela sem procedencia — que e exatamente o
/// "a IA ta ruim" que ninguem consegue verificar. Como parametro obrigatorio, o
/// sinal sem citacao nao COMPILA.
/// </summary>
public class Sinal
{
    public Sinal(string descricao, Guid mensagemId, string trechoCitado)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("Sinal sem descricao.", nameof(descricao));
        if (mensagemId == Guid.Empty)
            throw new ArgumentException(
                "Sinal precisa apontar para a mensagem que o originou. Sem procedencia, "
                + "o vendedor nao tem como conferir, e o sinal vira palpite com cara de dado.",
                nameof(mensagemId));
        if (string.IsNullOrWhiteSpace(trechoCitado))
            throw new ArgumentException(
                "Sinal precisa do trecho citado, e nao so do id: o vendedor le a frase na "
                + "tela, sem abrir a conversa inteira para procurar.",
                nameof(trechoCitado));

        Descricao = descricao.Trim();
        MensagemId = mensagemId;
        TrechoCitado = trechoCitado.Trim();
    }

    public string Descricao { get; }
    public Guid MensagemId { get; }
    public string TrechoCitado { get; }
}
