using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonaWatch.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class AddBookmarkCountToNewsContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookmarkCount",
                table: "NewsContents",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookmarkCount",
                table: "NewsContents");
        }
    }
}
