using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class InicialComCustoIa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "deals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbertoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Estagio = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CustoIaAcumulado = table.Column<decimal>(type: "numeric(18,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mensagens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Autor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false),
                    EnviadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConversaId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mensagens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mensagens_conversas_ConversaId",
                        column: x => x.ConversaId,
                        principalTable: "conversas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_invocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: true),
                    Modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustoEmReais = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    Quando = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_invocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_invocations_deals_DealId",
                        column: x => x.DealId,
                        principalTable: "deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_invocations_deal",
                table: "ai_invocations",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "ux_leads_telefone",
                table: "leads",
                column: "Telefone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mensagens_ConversaId",
                table: "mensagens",
                column: "ConversaId");

            migrationBuilder.CreateIndex(
                name: "ix_mensagens_enviada_em",
                table: "mensagens",
                column: "EnviadaEm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_invocations");

            migrationBuilder.DropTable(
                name: "leads");

            migrationBuilder.DropTable(
                name: "mensagens");

            migrationBuilder.DropTable(
                name: "deals");

            migrationBuilder.DropTable(
                name: "conversas");
        }
    }
}
