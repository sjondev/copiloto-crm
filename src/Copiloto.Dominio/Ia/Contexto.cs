using Copiloto.Dominio.Conversas;

namespace Copiloto.Dominio.Ia;

/// <summary>
/// As quatro camadas do contexto, da mais protegida para a mais cortavel.
/// </summary>
public enum Camada
{
    /// <summary>Identidade do agente e regra de ancoragem. Nunca cortada.</summary>
    C0Identidade = 0,

    /// <summary>Playbook da empresa. Entra inteiro ou nao entra.</summary>
    C1Playbook = 1,

    /// <summary>Ficha do negocio. Cortavel pelo fim.</summary>
    C2Ficha = 2,

    /// <summary>A conversa. Cortavel do mais antigo para o mais novo.</summary>
    C3Conversa = 3,
}

/// <summary>
/// Quantos tokens cada camada pode ocupar (#31). Vem de configuracao.
/// </summary>
/// <param name="Total">O teto da janela que se aceita gastar.</param>
/// <param name="FalasSempreLiterais">
/// Quantas falas do fim jamais viram resumo, custe o que custar ao orcamento.
///
/// E o criterio mais importante da issue e o menos obvio: tom, ironia e objecao
/// velada so sobrevivem no texto CRU. Resumir o recente destroi exatamente o
/// sinal que o dossie existe para captar — "vou pensar" vira "cliente
/// demonstrou interesse" e a leitura inverte de sentido.
/// </param>
public record OrcamentoDeContexto(
    int Total = 8000,
    int C0 = 200,
    int C1 = 800,
    int C2 = 1000,
    int FalasSempreLiterais = 6);

/// <summary>O contexto pronto para ir ao modelo, e o que foi deixado de fora.</summary>
public record ContextoMontado(
    string Texto,
    int TokensEstimados,
    IReadOnlyList<string> Cortes)
{
    public bool Cortou => Cortes.Count > 0;
}

/// <summary>
/// Monta o contexto em quatro camadas, dentro de um orcamento (#31).
///
/// Conversa de tres meses nao cabe em janela de contexto nenhuma, e a pergunta
/// nao e "como mandar tudo" — e "o que sacrificar primeiro". A ordem de
/// sacrificio E a decisao de produto inteira: corta-se o passado distante, nunca
/// a regra que impede o agente de inventar, e nunca a fala recente.
/// </summary>
public class MontadorDeContexto
{
    private const string Separador = "\n\n";

    private readonly OrcamentoDeContexto _orcamento;
    private readonly Func<string, int> _contar;

    /// <param name="contar">
    /// Como estimar tokens. Injetavel porque cada provedor tokeniza diferente,
    /// e o dia em que houver um tokenizador de verdade ele entra por aqui sem
    /// mexer na regra de corte.
    /// </param>
    public MontadorDeContexto(OrcamentoDeContexto? orcamento = null, Func<string, int>? contar = null)
    {
        _orcamento = orcamento ?? new OrcamentoDeContexto();
        _contar = contar ?? EstimarTokens;

        if (_orcamento.C0 >= _orcamento.Total)
            throw new ArgumentException(
                "A identidade sozinha ocupa o orcamento inteiro: nao sobra espaco para "
                + "conversa nenhuma, e o agente responderia sem ter lido nada.",
                nameof(orcamento));
    }

    /// <summary>
    /// Estimativa por caracteres, e ESTIMATIVA e a palavra certa.
    ///
    /// Quatro caracteres por token e a aproximacao usual para texto latino; o
    /// numero real depende do tokenizador de cada provedor e so se sabe depois
    /// de enviar. Por isso ela erra para MAIS: estourar a janela custa a
    /// chamada inteira, e sobrar um pouco de espaco nao custa nada.
    /// </summary>
    public static int EstimarTokens(string texto) =>
        string.IsNullOrEmpty(texto) ? 0 : (int)Math.Ceiling(texto.Length / 3.5);

    public ContextoMontado Montar(
        string identidade, string playbook, string ficha, IReadOnlyList<Fala> conversa)
    {
        ArgumentNullException.ThrowIfNull(conversa);

        var cortes = new List<string>();
        var partes = new List<string>();

        // C0 entra inteira, sempre. E a camada que carrega a regra de
        // ancoragem: sem ela o agente para de saber que nao pode inventar
        // escassez, e um contexto cortado ali e pior que contexto nenhum.
        partes.Add(identidade);
        var restante = _orcamento.Total - Custo(identidade);

        restante = AdicionarC1(playbook, partes, cortes, restante);
        restante = AdicionarC2(ficha, partes, cortes, restante);
        AdicionarC3(conversa, partes, cortes, restante);

        var texto = string.Join(Separador, partes.Where(p => !string.IsNullOrWhiteSpace(p)));
        return new ContextoMontado(texto, _contar(texto), cortes);
    }

    /// <summary>
    /// O playbook entra inteiro ou nao entra: e um conjunto de regras, e meia
    /// regra e pior que nenhuma — o agente seguiria a metade que sobrou sem
    /// saber que havia mais.
    /// </summary>
    private int AdicionarC1(string playbook, List<string> partes, List<string> cortes, int restante)
    {
        if (string.IsNullOrWhiteSpace(playbook)) return restante;

        var custo = Custo(playbook);
        if (custo > _orcamento.C1 || custo > restante)
        {
            cortes.Add($"C1 playbook fora: {custo} tokens nao cabem em {Math.Min(_orcamento.C1, restante)}");
            return restante;
        }

        partes.Add(playbook);
        return restante - custo;
    }

    /// <summary>
    /// A ficha corta pelo FIM. O comeco dela traz o que identifica o negocio;
    /// o fim traz detalhe acessorio, e e o que se perde com menos dano.
    /// </summary>
    private int AdicionarC2(string ficha, List<string> partes, List<string> cortes, int restante)
    {
        if (string.IsNullOrWhiteSpace(ficha)) return restante;

        var teto = Math.Min(_orcamento.C2, restante);
        var custo = Custo(ficha);

        if (custo <= teto)
        {
            partes.Add(ficha);
            return restante - custo;
        }

        var linhas = ficha.Split('\n');
        var mantidas = new List<string>();
        var usado = 0;

        foreach (var linha in linhas)
        {
            var custoDaLinha = _contar(linha + "\n");
            if (usado + custoDaLinha > teto) break;

            mantidas.Add(linha);
            usado += custoDaLinha;
        }

        cortes.Add($"C2 ficha cortada: {linhas.Length - mantidas.Count} de {linhas.Length} linhas fora");

        if (mantidas.Count > 0) partes.Add(string.Join("\n", mantidas));
        return restante - usado;
    }

    /// <summary>
    /// A conversa fica com o que sobrou, e corta do mais ANTIGO para o mais
    /// novo — com as ultimas falas protegidas por contrato.
    ///
    /// O que substitui o trecho cortado e uma MARCA de omissao, e nao um
    /// resumo: resumir de verdade exige ler, e ler custa uma chamada de modelo
    /// (#35). Dizer "12 falas omitidas" e honesto; inventar "o cliente
    /// demonstrou interesse" seria colocar na boca do agente uma leitura que
    /// ninguem fez.
    /// </summary>
    private void AdicionarC3(
        IReadOnlyList<Fala> conversa, List<string> partes, List<string> cortes, int restante)
    {
        if (conversa.Count == 0) return;

        var escolhidas = new LinkedList<Fala>();
        var protegidas = Math.Min(_orcamento.FalasSempreLiterais, conversa.Count);

        // A marca de omissao tambem ocupa espaco, e so se sabe se ela e
        // necessaria depois de escolher as falas. Reservar sempre custa ~20
        // tokens e evita o caso em que o proprio aviso de corte estoura o teto.
        var usado = Custo($"[{conversa.Count} fala(s) anteriores omitidas por orcamento de contexto]");

        // De tras para a frente: a fala mais recente e a que menos pode faltar.
        for (var i = conversa.Count - 1; i >= 0; i--)
        {
            var custo = Custo(Render(conversa[i]));
            var protegida = conversa.Count - i <= protegidas;

            if (!protegida && usado + custo > restante) break;

            escolhidas.AddFirst(conversa[i]);
            usado += custo;
        }

        var omitidas = conversa.Count - escolhidas.Count;
        if (omitidas > 0)
        {
            cortes.Add($"C3 conversa cortada: {omitidas} de {conversa.Count} falas omitidas");
            partes.Add($"[{omitidas} fala(s) anteriores omitidas por orcamento de contexto]");
        }

        partes.AddRange(escolhidas.Select(Render));
    }

    private static string Render(Fala fala) => $"{fala.Autor}: {fala.Texto}";

    /// <summary>
    /// O custo de uma parte inclui o separador que a une as outras.
    ///
    /// Sem isso o orcamento fecha na conta e estoura no texto: cada juncao
    /// acrescenta caracteres que ninguem somou, e o estouro aparece so depois
    /// do envio — quando ja custou a chamada inteira.
    /// </summary>
    private int Custo(string parte) => _contar(parte) + _contar(Separador);
}
