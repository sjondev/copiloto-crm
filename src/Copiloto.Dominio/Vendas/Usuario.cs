namespace Copiloto.Dominio.Vendas;

/// <summary>
/// O que a pessoa pode ver no CRM (#49).
///
/// Dois perfis, e nao uma lista de permissoes: a empresa que usa isto tem cinco
/// vendedores e um dono. Permissao granular esta fora de escopo por decisao, e
/// inventar niveis que ninguem pediu produz tela de administracao que ninguem
/// configura — e, na duvida, todo mundo vira gestor.
/// </summary>
public enum PerfilDeAcesso
{
    /// <summary>Ve os proprios leads.</summary>
    Vendedor = 0,

    /// <summary>Ve tudo, e e quem enxerga a conta de IA.</summary>
    Gestor = 1,
}

/// <summary>O vendedor. Quem decide, e a quem o dossie se dirige.</summary>
public class Usuario
{
    /// <summary>
    /// Tamanho abaixo do qual o "hash" nao e hash de senha moderno.
    ///
    /// MD5 tem 32 caracteres em hexadecimal e SHA-1 tem 40; BCrypt gera 60 com
    /// prefixo `$2`. A checagem existe porque trocar o algoritmo e uma linha, e
    /// o efeito de trocar para o errado nao aparece em teste nenhum — aparece
    /// no vazamento, anos depois, quando a base inteira e quebrada em horas.
    /// </summary>
    public const int TamanhoMinimoDoHash = 50;

    public Usuario(Guid id, string nome, string email, string senhaHash, PerfilDeAcesso perfil)
    {
        if (id == Guid.Empty) throw new ArgumentException("Usuario sem id.", nameof(id));
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Usuario sem nome.", nameof(nome));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Usuario sem email.", nameof(email));

        if (string.IsNullOrWhiteSpace(senhaHash) || senhaHash.Length < TamanhoMinimoDoHash)
            throw new ArgumentException(
                "Hash de senha curto demais para ser BCrypt ou Argon2. MD5 (32) e SHA-1 "
                + "(40) nao servem para senha: sao rapidos de calcular, e e justamente a "
                + "lentidao que protege a base depois de um vazamento.",
                nameof(senhaHash));

        Id = id;
        Nome = nome.Trim();
        Email = email.Trim().ToLowerInvariant();
        SenhaHash = senhaHash;
        Perfil = perfil;
    }

    public Guid Id { get; }
    public string Nome { get; }

    /// <summary>Guardado em minusculas: e por ele que o login procura.</summary>
    public string Email { get; }

    /// <summary>O hash. A senha em si nunca entra neste objeto.</summary>
    public string SenhaHash { get; private set; }

    /// <summary>
    /// O tipo se chama `PerfilDeAcesso` e a propriedade `Perfil` de proposito:
    /// com o mesmo nome nos dois, `Perfil == Perfil.Gestor` nao compila — o
    /// compilador resolve o identificador como a propriedade.
    /// </summary>
    public PerfilDeAcesso Perfil { get; private set; }

    public bool EhGestor => Perfil == PerfilDeAcesso.Gestor;

    public void TrocarSenha(string novoHash)
    {
        if (string.IsNullOrWhiteSpace(novoHash) || novoHash.Length < TamanhoMinimoDoHash)
            throw new ArgumentException("Hash curto demais.", nameof(novoHash));

        SenhaHash = novoHash;
    }
}
