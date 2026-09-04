using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class TrilhaDeAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "acessos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quando = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Detalhe = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acessos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acessos_lead",
                table: "acessos",
                columns: new[] { "LeadId", "Quando" });

            migrationBuilder.CreateIndex(
                name: "ix_acessos_usuario",
                table: "acessos",
                columns: new[] { "UsuarioId", "Quando" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acessos");
        }
    }
}
