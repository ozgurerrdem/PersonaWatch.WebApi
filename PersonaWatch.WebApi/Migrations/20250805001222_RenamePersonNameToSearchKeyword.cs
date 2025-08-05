using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonaWatch.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class RenamePersonNameToSearchKeyword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PersonName",
                table: "NewsContents",
                newName: "SearchKeyword");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SearchKeyword",
                table: "NewsContents",
                newName: "PersonName");
        }
    }
}
