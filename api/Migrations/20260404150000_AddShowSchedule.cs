using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XlightsQueue.Migrations
{
    public partial class AddShowSchedule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShowScheduleJson",
                table: "ShowConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ShowScheduleJson", table: "ShowConfigs");
        }
    }
}
