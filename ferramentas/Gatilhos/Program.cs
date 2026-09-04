using Gatilhos;

var raiz = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

if (!Directory.Exists(raiz))
{
    Console.Error.WriteLine($"Raiz inexistente: {raiz}");
    return 2;
}

string[] pastas = ["src", "testes", "ferramentas"];

var arquivos = pastas
    .Select(pasta => Path.Combine(raiz, pasta))
    .Where(Directory.Exists)
    .SelectMany(pasta => Directory.EnumerateFiles(pasta, "*.cs", SearchOption.AllDirectories))
    .Where(Interessa)
    .OrderBy(c => c, StringComparer.Ordinal)
    .Select(c => (Caminho: Path.GetRelativePath(raiz, c), Texto: File.ReadAllText(c)))
    .ToList();

var achados = arquivos
    .SelectMany(a => Medidas.DoArquivo(a.Caminho, a.Texto))
    .Concat(Duplicacao.Entre(arquivos))
    .ToList();

Console.WriteLine($"{arquivos.Count} arquivo(s) medido(s) em {raiz}.");
Console.WriteLine();

// Zero arquivo nao e "nada bateu limite": e a ferramenta apontada para o lugar
// errado. Foi o que aconteceu na primeira versao deste workflow — `--nologo`
// virou o caminho da raiz, a medicao rodou sobre uma pasta que nao existe e o
// gate ficou VERDE sem ter olhado para nada. Verde por nao ter olhado e pior
// que vermelho, porque vermelho manda consertar.
if (arquivos.Count == 0)
{
    Console.Error.WriteLine(
        "Nenhum arquivo .cs encontrado. Ou a raiz esta errada, ou o repositorio "
        + "mudou de forma — as duas coisas exigem olho humano, e nenhuma delas e "
        + "motivo para dizer que nada bateu limite.");
    return 2;
}
Relatorio.NoTerminal(achados);

if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
    Relatorio.ComoAnotacao(achados);

var resumo = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
if (!string.IsNullOrEmpty(resumo))
    File.AppendAllText(resumo, Relatorio.Resumo(achados));

// Sempre 0. A politica da #75 e explicita: tamanho e duplicacao AVISAM e
// exigem issue aberta; quem reprova o PR e estilo, segredo, build e teste.
return 0;

// Codigo que a ferramenta gerou nao e codigo que alguem escreveu: cobrar 300
// linhas de uma migration do EF seria cobrar de quem rodou o comando.
static bool Interessa(string caminho) =>
    !caminho.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
    && !caminho.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
    && !caminho.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
