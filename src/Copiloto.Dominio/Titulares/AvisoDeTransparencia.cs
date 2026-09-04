namespace Copiloto.Dominio.Titulares;

/// <summary>
/// O que o cliente precisa saber, no tamanho que ele vai ler (#80).
///
/// Transparencia e principio expresso (art. 6), e a base de legitimo interesse
/// depende dela: o titular so pode se opor a analise se souber que ela existe.
///
/// O desafio real nao e juridico, e de formato — ninguem le termo de quatro
/// paragrafos no WhatsApp. Um aviso que o cliente pula cumpre a formalidade e
/// falha na finalidade, entao aqui a versao curta e a principal, e a completa
/// mora atras de um link.
///
/// ISTO NAO E A IA FALANDO COM O CLIENTE. E texto fixo da empresa, escrito uma
/// vez e revisado por gente — nenhum modelo o gera e nenhum modelo o altera. A
/// tese do produto continua inteira: quem conversa e o vendedor.
/// </summary>
public static class AvisoDeTransparencia
{
    /// <summary>
    /// Teto do aviso curto.
    ///
    /// E escolha de produto, nao limite de plataforma: acima disso o texto vira
    /// bloco na tela do celular, e bloco em conversa de venda e o que se pula.
    /// Preferir cortar conteudo a perder o leitor e a decisao — o que nao cabe
    /// aqui esta no link, que continua sendo o documento completo.
    /// </summary>
    public const int TetoDoAvisoCurto = 220;

    /// <summary>
    /// A frase que vai na primeira resposta ao cliente.
    ///
    /// A ordem e deliberada: primeiro o que acontece com a conversa, depois a
    /// tranquilizacao que so este produto pode dar — "quem responde e uma
    /// pessoa" —, e por ultimo o link. Comecar pelo link faria o resto nao ser
    /// lido; terminar pela pessoa deixaria o aviso soando como robo se
    /// desculpando.
    /// </summary>
    public static string Curto(string empresa, string linkDaPolitica)
    {
        Exigir(empresa, nameof(empresa));
        Exigir(linkDaPolitica, nameof(linkDaPolitica));

        var aviso =
            $"Oi! Esta conversa fica registrada na {empresa.Trim()} e usamos IA para "
            + "organizar o atendimento. Quem responde aqui é uma pessoa. "
            + $"Detalhes e seus direitos: {linkDaPolitica.Trim()}";

        if (aviso.Length > TetoDoAvisoCurto)
            throw new InvalidOperationException(
                $"O aviso ficou com {aviso.Length} caracteres, acima de "
                + $"{TetoDoAvisoCurto}: nome de empresa ou link comprido demais. "
                + "Encurte o link — o texto do aviso ja esta no minimo que informa.");

        return aviso;
    }

    /// <summary>
    /// A versao completa, que mora na politica de privacidade.
    ///
    /// Cinco coisas, na linguagem de quem compra cafe e nao de quem escreve
    /// contrato: o que e registrado, que ha analise por IA, para que, com quem
    /// e compartilhado, e como exercer direitos.
    /// </summary>
    public static string Completo(string empresa, string canalDeContato)
    {
        Exigir(empresa, nameof(empresa));
        Exigir(canalDeContato, nameof(canalDeContato));

        var nome = empresa.Trim();

        return string.Join("\n\n",
            $"**O que registramos.** As mensagens que você troca com a {nome} pelo "
            + "WhatsApp ficam guardadas, junto com seu telefone e o que você nos contou "
            + "sobre o que procura.",

            "**O que a IA faz — e o que ela não faz.** Um sistema de inteligência "
            + "artificial lê essas mensagens e monta, para o vendedor, um resumo do que "
            + "você pediu e do que ficou em aberto. **A IA não conversa com você**: toda "
            + "mensagem que chega até aqui foi escrita por uma pessoa da equipe.",

            "**Para quê.** Para atender melhor: não perder o que você pediu, não repetir "
            + "pergunta que você já respondeu, e não esquecer de voltar quando você pede "
            + "para pensar.",

            "**Com quem compartilhamos.** Com o provedor de tecnologia que roda a "
            + "análise, e apenas com ele. Antes de sair daqui, dados como telefone, "
            + "e-mail e documento são substituídos por marcadores. Não vendemos e não "
            + "cedemos seus dados para publicidade.",

            "**Seus direitos.** Você pode pedir para ver tudo o que temos sobre você, "
            + "corrigir o que estiver errado, levar seus dados embora, pedir exclusão, e "
            + "**pedir que a análise por IA pare** — sem perder o atendimento. É só falar "
            + $"com a gente: {canalDeContato.Trim()}.");
    }

    /// <summary>
    /// Se este cliente ainda precisa receber o aviso.
    ///
    /// Uma vez por pessoa, e nao uma vez por conversa: repetir o aviso a cada
    /// retomada transforma transparencia em ruido, e ruido e a forma mais
    /// eficiente de nao ser lido.
    /// </summary>
    public static bool PrecisaAvisar(DateTimeOffset? avisadoEm) => avisadoEm is null;

    private static void Exigir(string valor, string nome)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException(
                $"Aviso sem {nome} nao informa nada: um texto generico deixaria o "
                + "cliente sem saber com quem esta falando nem onde reclamar.", nome);
    }
}
