using Copiloto.Dominio.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class PrecedenteMap : IEntityTypeConfiguration<Precedente>
{
    public void Configure(EntityTypeBuilder<Precedente> builder)
    {
        builder.ToTable("precedentes");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.LeadId).IsRequired();
        builder.Property(p => p.Trecho).IsRequired();
        builder.Property(p => p.CriadoEm).IsRequired();

        // float[] no dominio, `vector` na coluna: a conversao mora aqui porque
        // o dominio nao tem pacote (#48) e nao pode conhecer o tipo do pgvector.
        builder.Property(p => p.Vetor)
            .HasColumnType($"vector({Embedding.Dimensoes})")
            .HasConversion(
                v => new Vector(v),
                v => v.ToArray().ToArray())
            .IsRequired();

        // Indice HNSW com distancia de COSSENO — a mesma que a consulta usa. Um
        // indice criado com outro operador simplesmente nao e usado pela
        // consulta, e o sintoma e lentidao silenciosa, nao erro.
        builder.HasIndex(p => p.Vetor)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasDatabaseName("ix_precedentes_vetor");

        // O expurgo por titular (#46) roda por aqui: sem indice, apagar o que e
        // de uma pessoa varre a tabela inteira — no dia em que ela pediu.
        builder.HasIndex(p => p.LeadId).HasDatabaseName("ix_precedentes_lead");
    }
}
