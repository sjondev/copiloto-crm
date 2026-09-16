using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class FatoEImpressaoNaFicha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Empresa_ComoChegou",
                table: "fichas_cliente");

            migrationBuilder.DropColumn(
                name: "Empresa_Momento",
                table: "fichas_cliente");

            migrationBuilder.DropColumn(
                name: "Empresa_Porte",
                table: "fichas_cliente");

            migrationBuilder.DropColumn(
                name: "Empresa_Ramo",
                table: "fichas_cliente");

            migrationBuilder.DropColumn(
                name: "Negocio_OrcamentoEstimado",
                table: "fichas_cliente");

            migrationBuilder.DropColumn(
                name: "Negocio_ProvavelNecessidade",
                table: "fichas_cliente");

            migrationBuilder.DropColumn(
                name: "Negocio_RiscoConhecido",
                table: "fichas_cliente");

            migrationBuilder.DropColumn(
                name: "Negocio_UsaHoje",
                table: "fichas_cliente");

            migrationBuilder.DropColumn(
                name: "Pessoa_Cargo",
                table: "fichas_cliente");

            migrationBuilder.DropColumn(
                name: "Pessoa_EstiloObservado",
                table: "fichas_cliente");

            migrationBuilder.DropColumn(
                name: "Pessoa_PapelNaDecisao",
                table: "fichas_cliente");

            migrationBuilder.DropColumn(
                name: "Pessoa_QuemMaisDecide",
                table: "fichas_cliente");

            migrationBuilder.AddColumn<string>(
                name: "empresa",
                table: "fichas_cliente",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "negocio",
                table: "fichas_cliente",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "pessoa",
                table: "fichas_cliente",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "empresa",
                table: "fichas_cliente");

            migrationBuilder.DropColumn(
                name: "negocio",
                table: "fichas_cliente");

            migrationBuilder.DropColumn(
                name: "pessoa",
                table: "fichas_cliente");

            migrationBuilder.AddColumn<string>(
                name: "Empresa_ComoChegou",
                table: "fichas_cliente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Empresa_Momento",
                table: "fichas_cliente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Empresa_Porte",
                table: "fichas_cliente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Empresa_Ramo",
                table: "fichas_cliente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Negocio_OrcamentoEstimado",
                table: "fichas_cliente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Negocio_ProvavelNecessidade",
                table: "fichas_cliente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Negocio_RiscoConhecido",
                table: "fichas_cliente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Negocio_UsaHoje",
                table: "fichas_cliente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pessoa_Cargo",
                table: "fichas_cliente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pessoa_EstiloObservado",
                table: "fichas_cliente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pessoa_PapelNaDecisao",
                table: "fichas_cliente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pessoa_QuemMaisDecide",
                table: "fichas_cliente",
                type: "text",
                nullable: true);
        }
    }
}
