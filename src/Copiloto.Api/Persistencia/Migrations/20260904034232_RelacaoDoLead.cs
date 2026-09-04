using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class RelacaoDoLead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O default gerado era string vazia, que NAO e um valor do enum: a
            // primeira leitura de um lead antigo quebraria a conversao, e o
            // erro apareceria como falha de consulta, longe da causa.
            //
            // "Cliente" e o default certo tambem no merito: todo lead que ja
            // existe entrou como comprador, e errar para esse lado e o lado
            // seguro — parceiro marcado como cliente e o que precisa ser
            // corrigido a mao, e por isso o UPDATE abaixo e explicito.
            migrationBuilder.AddColumn<string>(
                name: "Relacao",
                table: "leads",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Cliente");

            migrationBuilder.Sql(
                "UPDATE leads SET \"Relacao\" = 'Cliente' WHERE \"Relacao\" = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Relacao",
                table: "leads");
        }
    }
}
