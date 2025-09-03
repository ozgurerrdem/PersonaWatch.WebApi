using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonaWatch.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class AddContentHashToNewsContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "NewsContents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "NewsContents");
        }
    }
}
