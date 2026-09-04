using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class UsuarioMap : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> e)
    {
        e.ToTable("usuarios");
        e.HasKey(u => u.Id);
        e.Property(u => u.Nome).HasMaxLength(200).IsRequired();
        e.Property(u => u.Email).HasMaxLength(200).IsRequired();

        // 100 e folga sobre os 60 do BCrypt: Argon2 e mais longo, e a coluna
        // curta demais seria descoberta no dia da troca de algoritmo, com o
        // hash truncado silenciosamente pelo banco.
        e.Property(u => u.SenhaHash).HasMaxLength(100).IsRequired();
        e.Property(u => u.Perfil).HasConversion<string>().HasMaxLength(20).IsRequired();

        // O email e a credencial de login: dois usuarios com o mesmo email
        // fariam o login depender de qual linha o banco devolve primeiro.
        e.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ux_usuarios_email");
    }
}
