using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XlightsQueue.Migrations
{
    public partial class AddSortOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Playlists",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Songs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Keep today's order: start from the current ids.
            migrationBuilder.Sql("UPDATE Playlists SET SortOrder = Id;");
            migrationBuilder.Sql("UPDATE Songs SET SortOrder = Id;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SortOrder", table: "Playlists");
            migrationBuilder.DropColumn(name: "SortOrder", table: "Songs");
        }
    }
}
