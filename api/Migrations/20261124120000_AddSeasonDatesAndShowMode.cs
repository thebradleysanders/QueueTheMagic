using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XlightsQueue.Migrations
{
    public partial class AddSeasonDatesAndShowMode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SeasonStart",
                table: "ShowConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeasonEnd",
                table: "ShowConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShowMode",
                table: "ShowConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "auto");

            // Anyone who had the old season switch turned off keeps the show off.
            migrationBuilder.Sql("UPDATE ShowConfigs SET ShowMode = 'off' WHERE IsSeasonActive = 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SeasonStart", table: "ShowConfigs");
            migrationBuilder.DropColumn(name: "SeasonEnd", table: "ShowConfigs");
            migrationBuilder.DropColumn(name: "ShowMode", table: "ShowConfigs");
        }
    }
}
