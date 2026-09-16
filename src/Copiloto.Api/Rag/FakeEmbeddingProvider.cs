using System.Security.Cryptography;
using System.Text;
using Copiloto.Dominio.Rag;

namespace Copiloto.Api.Rag;

/// <summary>
/// Embedding deterministico a partir do texto, sem rede e sem custo (#60).
///
/// E o padrao, pela mesma razao do FakeSource e do FakeProvider: a suite roda
/// offline e de graca, e teste que precisa de chave de provedor para passar
/// quebra no primeiro clone.
///
/// O que ele NAO e: uma aproximacao de semantica. Textos com sentido parecido
/// nao ficam proximos aqui — quem fica proximo e o texto parecido no BYTE. Isso
/// basta para o que a #60 precisa provar (guardar, indexar, recuperar por
/// distancia, expurgar), e nao basta para avaliar qualidade de recuperacao, que
/// e a #65 e exige provedor real.
/// </summary>
public class FakeEmbeddingProvider : IEmbeddingProvider
{
    public string Modelo => "fake-deterministico-v1";

    public Task<float[]> Vetorizar(string texto, CancellationToken ct)
    {
        var vetor = new float[Embedding.Dimensoes];
        var semente = SHA256.HashData(Encoding.UTF8.GetBytes(texto ?? ""));

        // Gerador simples alimentado pelo hash: mesmo texto, mesmo vetor,
        // sempre — em qualquer maquina e em qualquer execucao. Um Random sem
        // semente fixa daria um teste que passa uma vez e nunca mais.
        var estado = BitConverter.ToUInt64(semente, 0) | 1UL;

        for (var i = 0; i < vetor.Length; i++)
        {
            estado = (estado * 6364136223846793005UL) + 1442695040888963407UL;
            vetor[i] = ((estado >> 33) / (float)uint.MaxValue) - 0.5f;
        }

        // Normalizado: com vetores de norma 1, a distancia de cosseno fica
        // comparavel entre consultas, e o numero que sobe com o resultado quer
        // dizer a mesma coisa sempre.
        var norma = MathF.Sqrt(vetor.Sum(v => v * v));
        if (norma > 0)
            for (var i = 0; i < vetor.Length; i++) vetor[i] /= norma;

        return Task.FromResult(vetor);
    }
}
