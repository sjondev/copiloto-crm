using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class NumerosDoLead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NomeFonte",
                table: "leads",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            // `defaultValue` NAO e enfeite: sem ele o EF gera a coluna NOT NULL sem
            // valor, e o Postgres recusa a alteracao inteira em qualquer base que
            // ja tenha linha — `23502: column "outros_numeros" contains null
            // values`. Conferido aqui rodando contra o banco de verdade; a suite
            // nao pegaria, porque ela monta o schema com EnsureCreated() a partir
            // do modelo e nunca executa migration (#172).
            //
            // Lista vazia e o valor certo tambem no merito: lead que ja existia
            // fala por um numero so, que e o principal.
            migrationBuilder.AddColumn<List<string>>(
                name: "outros_numeros",
                table: "leads",
                type: "text[]",
                nullable: false,
                defaultValue: new List<string>());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NomeFonte",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "outros_numeros",
                table: "leads");
        }
    }
}
