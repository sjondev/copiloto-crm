using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <summary>
    /// VAZIA DE PROPOSITO, e nao esquecida.
    ///
    /// `ValueGeneratedNever()` nas chaves e metadado do MODELO: nao ha coluna,
    /// default nem indice a alterar no banco. O que muda e como o EF interpreta
    /// uma chave ja preenchida — antes ele lia "veio do banco" e marcava a
    /// entidade nova como Modified, gerando UPDATE numa linha inexistente.
    ///
    /// Ela existe porque o SNAPSHOT do modelo mudou. Apagar este arquivo faria
    /// a proxima migration nascer com um diff que ninguem pediu.
    /// </summary>
    public partial class IdsGeradosNoDominio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sem operacao: ver o resumo da classe.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem operacao: ver o resumo da classe.
        }
    }
}
