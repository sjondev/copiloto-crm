using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Rag;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Copiloto.Api.Rag;

/// <summary>
/// Busca por semelhanca no Postgres que ja existe (#60).
///
/// Sem banco vetorial dedicado, e a decisao esta no ARQUITETURA: Qdrant ou
/// Pinecone seriam um segundo banco para operar, com backup proprio, credencial
/// propria e consistencia propria — em troca de nada nesta escala. O que a
/// gente ganharia em vazao nao tem quem consuma; o que a gente perderia em
/// transacao apareceria no primeiro expurgo pela metade.
/// </summary>
public class BuscaComPgvector : IBuscaPorSimilaridade
{
    private readonly CopilotoDbContext _ctx;

    public BuscaComPgvector(CopilotoDbContext ctx) => _ctx = ctx;

    public async Task Guardar(Precedente precedente, CancellationToken ct)
    {
        _ctx.Precedentes.Add(precedente);
        await _ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Os mais parecidos, com a distancia junto.
    ///
    /// A ordenacao e a distancia saem do BANCO, na mesma consulta: trazer tudo
    /// e ordenar em memoria funcionaria com mil linhas e deixaria de funcionar
    /// exatamente quando o indice passasse a importar.
    /// </summary>
    public async Task<IReadOnlyList<PrecedenteSemelhante>> MaisParecidos(
        float[] consulta, int quantos, CancellationToken ct)
    {
        if (consulta.Length != Embedding.Dimensoes)
            throw new ArgumentException(
                $"Consulta com {consulta.Length} dimensoes contra coluna de "
                + $"{Embedding.Dimensoes}.", nameof(consulta));

        var alvo = new Vector(consulta);

        var achados = await _ctx.Precedentes
            .AsNoTracking()
            .Select(p => new { Precedente = p, Distancia = p.Vetor.CosineDistance(alvo) })
            .OrderBy(x => x.Distancia)
            .Take(Math.Clamp(quantos, 1, 50))
            .ToListAsync(ct);

        return achados
            .Select(x => new PrecedenteSemelhante(x.Precedente, x.Distancia))
            .ToList();
    }

    public async Task<int> EsquecerTitular(Guid leadId, CancellationToken ct) =>
        await _ctx.Precedentes.Where(p => p.LeadId == leadId).ExecuteDeleteAsync(ct);
}
