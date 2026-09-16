using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Api.Auth;

/// <summary>
/// Cria o primeiro gestor, para que exista alguem capaz de entrar (#182).
///
/// A #49 entregou login, perfis e politica — e nenhuma forma de criar usuario.
/// Enquanto as rotas eram anonimas isso nao aparecia; no instante em que elas
/// passaram a exigir credencial, o sistema ficaria TRANCADO: tela pedindo login
/// e banco sem ninguem para logar.
///
/// So roda com a tabela VAZIA. Nao e "criar se nao existir aquele email": e
/// "criar o primeiro", e uma vez que haja gente, esta rotina nao toca em nada.
/// Assim a variavel de ambiente esquecida num .env nao vira porta dos fundos
/// que recria acesso depois que alguem removeu o usuario.
/// </summary>
public static class PrimeiroGestor
{
    public const string ChaveEmail = "ADMIN_EMAIL";
    public const string ChaveSenha = "ADMIN_SENHA";

    /// <summary>Senha curta e o mesmo que senha nenhuma quando a porta e publica.</summary>
    public const int TamanhoMinimoDaSenha = 12;

    public static async Task Garantir(
        CopilotoDbContext ctx, IConfiguration configuracao, ILogger log, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(log);

        if (await ctx.Usuarios.AnyAsync(ct)) return;

        var email = configuracao[ChaveEmail];
        var senha = configuracao[ChaveSenha];

        // `IsNullOrWhiteSpace`, e nao `is null`: as duas chaves sao declaradas
        // VAZIAS no .env.example, e string vazia passaria por uma checagem de
        // nulo. Esse detalhe ja derrubou a subida em SEED_RESPOSTAS e em
        // PROMPTS_DIR.
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
        {
            // AVISO e nao excecao: quem sobe para rodar a suite ou olhar o
            // /saude nao deveria ser obrigado a inventar um administrador. Mas o
            // aviso precisa dizer o que aconteceu E como resolver, senao vira a
            // linha de log que todo mundo rola para cima.
            log.LogWarning(
                "Nenhum usuario no banco e {Email}/{Senha} nao foram definidos: NINGUEM consegue "
                + "entrar na tela, porque as rotas de leitura exigem credencial. Defina as duas "
                + "variaveis e suba de novo para criar o primeiro gestor.",
                ChaveEmail, ChaveSenha);
            return;
        }

        if (senha.Length < TamanhoMinimoDaSenha)
        {
            throw new InvalidOperationException(
                $"{ChaveSenha} tem menos de {TamanhoMinimoDaSenha} caracteres. Esta e a senha do "
                + "primeiro GESTOR, que enxerga a conversa de todo mundo — e derrubar a subida "
                + "aqui e mais barato que descobrir depois que ela era 'admin123'.");
        }

        var gestor = new Usuario(
            Guid.NewGuid(),
            nome: "Gestor",
            email: email.Trim(),
            senhaHash: Senhas.Hash(senha),
            perfil: PerfilDeAcesso.Gestor);

        ctx.Usuarios.Add(gestor);
        await ctx.SaveChangesAsync(ct);

        log.LogInformation(
            "Primeiro gestor criado para {Email}. Troque a senha no primeiro acesso.",
            gestor.Email);
    }
}
