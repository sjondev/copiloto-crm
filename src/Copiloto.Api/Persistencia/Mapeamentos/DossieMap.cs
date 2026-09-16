using Copiloto.Dominio.Dossies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class DossieMap : IEntityTypeConfiguration<Dossie>
{
    public void Configure(EntityTypeBuilder<Dossie> e)
    {
        e.ToTable("dossies");

        // O id nasce no dominio, nunca no banco (#158). Ver ConversaMap.
        e.Property(d => d.Id).ValueGeneratedNever();
        e.HasKey(d => d.Id);

        e.Property(d => d.DealId).IsRequired();
        e.Property(d => d.GeradoEm).IsRequired();

        // A leitura entra decomposta e volta montada pela propriedade.
        e.Property(d => d.TemperaturaLida).HasConversion<string>().HasMaxLength(20);
        e.Property(d => d.DirecaoLida).HasConversion<string>().HasMaxLength(20);
        e.Ignore(d => d.Termometro);

        // As lacunas sao texto solto e so fazem sentido dentro do dossie que as
        // gerou: viram JSON numa coluna, e nao tabela. Tabela pediria chave para
        // uma frase, e frase nao tem identidade — "nao sabemos o orcamento" de
        // ontem e de hoje sao a mesma frase e coisas diferentes.
        e.PrimitiveCollection(d => d.Lacunas).HasField("_lacunas");

        // Sinal e objeto de VALOR: nao tem id proprio, nao existe fora do dossie
        // e nunca e consultado sozinho — a tela sempre pede o dossie inteiro.
        // Por isso vai como JSON numa coluna, e nao em tabela.
        //
        // Tabela exigiria chave para o sinal, e a unica disponivel seria um
        // contador gerado pelo banco. Isso sai caro de duas formas: inventa
        // identidade para algo que nao tem, e quebra no SQLite da suite, onde
        // coluna int dentro de chave composta nao auto-incrementa. Teste que so
        // roda contra Postgres nao roda.
        e.OwnsMany(d => d.Sinais, s =>
        {
            s.ToJson("sinais");

            s.Property(x => x.Descricao).IsRequired();
            s.Property(x => x.MensagemId).IsRequired();
            s.Property(x => x.TrechoCitado).IsRequired();
            s.Property(x => x.Tipo).HasConversion<string>().IsRequired();
        });

        e.Navigation(d => d.Sinais).UsePropertyAccessMode(PropertyAccessMode.Field);

        // A tela sempre pede o dossie MAIS RECENTE de um negocio.
        e.HasIndex(d => new { d.DealId, d.GeradoEm }).HasDatabaseName("ix_dossies_deal_gerado");
    }
}
