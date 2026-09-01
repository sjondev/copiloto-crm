using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class FichaDoCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fichas_cliente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Empresa_Ramo = table.Column<string>(type: "text", nullable: true),
                    Empresa_Porte = table.Column<string>(type: "text", nullable: true),
                    Empresa_Momento = table.Column<string>(type: "text", nullable: true),
                    Empresa_ComoChegou = table.Column<string>(type: "text", nullable: true),
                    Pessoa_Cargo = table.Column<string>(type: "text", nullable: true),
                    Pessoa_PapelNaDecisao = table.Column<string>(type: "text", nullable: true),
                    Pessoa_QuemMaisDecide = table.Column<string>(type: "text", nullable: true),
                    Pessoa_EstiloObservado = table.Column<string>(type: "text", nullable: true),
                    Negocio_ProvavelNecessidade = table.Column<string>(type: "text", nullable: true),
                    Negocio_UsaHoje = table.Column<string>(type: "text", nullable: true),
                    Negocio_OrcamentoEstimado = table.Column<string>(type: "text", nullable: true),
                    Negocio_RiscoConhecido = table.Column<string>(type: "text", nullable: true),
                    historico = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fichas_cliente", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_fichas_lead",
                table: "fichas_cliente",
                column: "LeadId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fichas_cliente");
        }
    }
}
