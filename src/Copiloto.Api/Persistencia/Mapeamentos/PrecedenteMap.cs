using Copiloto.Dominio.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class PrecedenteMap : IEntityTypeConfiguration<Precedente>
{
    public void Configure(EntityTypeBuilder<Precedente> e)
    {
        e.ToTable("precedentes");
        e.HasKey(p => p.Id);
        e.Property(p => p.LeadId).IsRequired();
        e.Property(p => p.Trecho).IsRequired();
        e.Property(p => p.CriadoEm).IsRequired();

        // float[] no dominio, `vector` na coluna: a conversao mora aqui porque
        // o dominio nao tem pacote (#48) e nao pode conhecer o tipo do pgvector.
        e.Property(p => p.Vetor)
            .HasColumnType($"vector({Embedding.Dimensoes})")
            .HasConversion(
                v => new Vector(v),
                v => v.ToArray().ToArray())
            .IsRequired();

        // Indice HNSW com distancia de COSSENO — a mesma que a consulta usa. Um
        // indice criado com outro operador simplesmente nao e usado pela
        // consulta, e o sintoma e lentidao silenciosa, nao erro.
        e.HasIndex(p => p.Vetor)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasDatabaseName("ix_precedentes_vetor");

        // O expurgo por titular (#46) roda por aqui: sem indice, apagar o que e
        // de uma pessoa varre a tabela inteira — no dia em que ela pediu.
        e.HasIndex(p => p.LeadId).HasDatabaseName("ix_precedentes_lead");
    }
}
