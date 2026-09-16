using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Fichas;

namespace Copiloto.Dominio.Planos;

/// <summary>
/// Em que modo o copiloto opera para este lead (#87).
/// </summary>
public enum ModoDoCopiloto
{
    /// <summary>Ainda nao houve conversa: a proxima fala e a PRIMEIRA.</summary>
    AbordagemInicial = 0,

    /// <summary>Ja existe conversa: dossie do que foi dito + plano de continuidade.</summary>
    Continuidade = 1,
}

/// <summary>
/// A abordagem fria, que e o caso MAIS dificil e nao o mais facil (#87).
///
/// Na continuidade, sugestao ruim e descartada pelo vendedor e a conversa
/// segue. Aqui nao ha segunda chance: o erro vai para o cliente e queima o
/// lead de forma permanente — com menos contexto disponivel, porque sinal de
/// conversa, a materia-prima mais confiavel do sistema, ainda nao existe.
///
/// Por isso a classe monta ANGULO ancorado, e nao mensagem pronta. "Olá João,
/// espero que esteja bem! Vi que a empresa X..." e reconhecivel a um quilometro
/// e produz o efeito oposto — e um copiloto que gera isso com ficha vazia e
/// pior que copiloto nenhum, porque da ao vendedor a falsa sensacao de que a
/// mensagem foi pensada. Quem escreve continua sendo o vendedor.
/// </summary>
public static class AbordagemInicial
{
    /// <summary>
    /// Fatos que justificam falar HOJE, e nao em outro dia qualquer. Sao os
    /// unicos rotulos da ficha que respondem "por que agora": os demais dizem
    /// quem e o cliente, nao o que mudou nele.
    /// </summary>
    private static readonly string[] Ganchos =
        ["Momento", "Como chegou", "Usa hoje", "Risco conhecido"];

    /// <summary>Ausencia de conversa muda o modo do copiloto.</summary>
    public static ModoDoCopiloto ModoPara(Conversa? conversa) =>
        conversa is null || conversa.Mensagens.Count == 0
            ? ModoDoCopiloto.AbordagemInicial
            : ModoDoCopiloto.Continuidade;

    /// <summary>
    /// O plano da primeira fala, construido a partir de FATO especifico da
    /// ficha.
    ///
    /// Ficha sem fato devolve so perguntas. Recusar-se a inventar quando nao ha
    /// material e o comportamento correto — e o mesmo raciocinio da regra de
    /// ancoragem (#15), aplicado ao momento em que o vendedor mais procrastina.
    /// </summary>
    public static Plano Montar(
        Guid planoId, Guid dealId, FichaCliente? ficha, DateTimeOffset agora)
    {
        var plano = new Plano(planoId, dealId, agora);
        var fatos = ficha?.Fatos ?? new Dictionary<string, Anotacao>();

        if (fatos.Count == 0)
        {
            foreach (var bloco in SemMaterial(ficha)) plano.Adicionar(bloco);
            return plano;
        }

        foreach (var bloco in Aberturas(fatos)) plano.Adicionar(bloco);
        plano.Adicionar(PorQueAgora(fatos));
        plano.Adicionar(Canal(fatos));

        // O horario nao tem campo na ficha, entao nunca ha dado que o sustente:
        // sugerir "mande de manha" seria palpite com cara de recomendacao.
        plano.Adicionar(BlocoSugerido.Perguntar(Tatica.Livre,
            "Você sabe o horário em que essa pessoa costuma responder? "
            + "Sem isso, não dá para sugerir o momento do envio."));

        return plano;
    }

    /// <summary>
    /// Duas aberturas, de angulos diferentes, cada uma presa a um fato seu.
    ///
    /// Com um fato so, sai UMA abertura e uma pergunta — inventar o segundo
    /// angulo a partir do mesmo fato daria duas versoes do mesmo texto, que e'
    /// a aparencia de escolha sem a escolha.
    /// </summary>
    private static IEnumerable<BlocoSugerido> Aberturas(
        IReadOnlyDictionary<string, Anotacao> fatos)
    {
        foreach (var (rotulo, fato) in fatos.Take(2))
        {
            yield return BlocoSugerido.AncoradoEm(Tatica.Livre,
                $"Ângulo: abrir por {rotulo.ToLowerInvariant()} — {fato.Valor}. "
                + "A primeira linha é o motivo do contato, e não saudação.",
                fato);
        }

        if (fatos.Count == 1)
        {
            yield return BlocoSugerido.Perguntar(Tatica.Livre,
                "Só há um fato apurado, então só há um ângulo honesto. "
                + "O que mais você sabe desse cliente que eu possa usar como segundo?");
        }
    }

    private static BlocoSugerido PorQueAgora(IReadOnlyDictionary<string, Anotacao> fatos)
    {
        var gancho = Ganchos.Where(fatos.ContainsKey).Select(g => (Rotulo: g, Fato: fatos[g]))
                            .FirstOrDefault();

        return gancho.Fato is null
            ? BlocoSugerido.Perguntar(Tatica.Livre,
                "O que justifica falar com ele HOJE? Sem gancho, a mensagem vira "
                + "contato sem motivo — e é o tipo de coisa que só dá para mandar uma vez.")
            : BlocoSugerido.AncoradoEm(Tatica.Livre,
                $"Por que agora: {gancho.Fato.Valor}", gancho.Fato);
    }

    /// <summary>
    /// O canal sai de "Como chegou": quem veio pelo Instagram responde no
    /// Instagram. E dado, nao preferencia inventada.
    /// </summary>
    private static BlocoSugerido Canal(IReadOnlyDictionary<string, Anotacao> fatos) =>
        fatos.TryGetValue("Como chegou", out var origem)
            ? BlocoSugerido.AncoradoEm(Tatica.Livre,
                $"Canal: responder por onde ele chegou — {origem.Valor}", origem)
            : BlocoSugerido.Perguntar(Tatica.Livre,
                "Por onde esse lead chegou? É o que diz em qual canal falar com ele.");

    /// <summary>
    /// Ficha sem fato: o copiloto PEDE informacao, e nao produz mensagem.
    ///
    /// Impressao registrada na ficha nao muda isso — ela vira pergunta de
    /// confirmacao, porque "parece desconfiado" nao sustenta a primeira frase
    /// que o cliente vai ler na vida dele sobre esta empresa (#88).
    /// </summary>
    private static IEnumerable<BlocoSugerido> SemMaterial(FichaCliente? ficha)
    {
        yield return BlocoSugerido.Perguntar(Tatica.Livre,
            "Não há nenhum fato apurado sobre esse cliente, então não vou escrever "
            + "uma abordagem: ela sairia genérica, e primeira mensagem genérica "
            + "queima o lead de vez. Me diga uma coisa concreta e eu monto os ângulos.");

        foreach (var (rotulo, impressao) in ficha?.Impressoes ?? new Dictionary<string, Anotacao>())
        {
            yield return BlocoSugerido.Perguntar(Tatica.Livre,
                $"Você anotou uma impressão em {rotulo.ToLowerInvariant()}: "
                + $"\"{impressao.Valor}\". Dá para confirmar isso como fato antes de eu usar?");
        }

        foreach (var lacuna in (ficha?.Lacunas() ?? []).Take(3))
        {
            yield return BlocoSugerido.Perguntar(Tatica.Livre,
                $"O que você sabe sobre {lacuna.ToLowerInvariant()}?");
        }
    }
}
