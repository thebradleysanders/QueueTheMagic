using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XlightsQueue.Migrations
{
    public partial class AddSeasonToggle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSeasonActive",
                table: "ShowConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "OffSeasonMessage",
                table: "ShowConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "The holiday light show is not running right now. Check back soon!");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsSeasonActive", table: "ShowConfigs");
            migrationBuilder.DropColumn(name: "OffSeasonMessage", table: "ShowConfigs");
        }
    }
}
