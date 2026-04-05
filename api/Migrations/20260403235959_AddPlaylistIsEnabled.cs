using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XlightsQueue.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylistIsEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "Playlists",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DonateCost",
                table: "ShowConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: 5.00m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsEnabled", table: "Playlists");
            migrationBuilder.DropColumn(name: "DonateCost", table: "ShowConfigs");
        }
    }
}
