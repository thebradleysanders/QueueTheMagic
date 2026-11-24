using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XlightsQueue.Migrations
{
    public partial class AddListenLive : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ListenEnabled",
                table: "ShowConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ListenMaxListeners",
                table: "ShowConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 50);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ListenEnabled", table: "ShowConfigs");
            migrationBuilder.DropColumn(name: "ListenMaxListeners", table: "ShowConfigs");
        }
    }
}
