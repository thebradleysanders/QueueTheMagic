using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XlightsQueue.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Donations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StripePaymentIntentId = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionToken = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Donations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Playlists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    FppPlaylistName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShowConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SiteName = table.Column<string>(type: "TEXT", nullable: false),
                    FmRadioStation = table.Column<string>(type: "TEXT", nullable: false),
                    SocialMediaJson = table.Column<string>(type: "TEXT", nullable: false),
                    SongRequestCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    BumpCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    HoursStart = table.Column<string>(type: "TEXT", nullable: true),
                    HoursEnd = table.Column<string>(type: "TEXT", nullable: true),
                    FppAddress = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultPlaylistName = table.Column<string>(type: "TEXT", nullable: false),
                    AutoPlayDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    InterruptForUserSongs = table.Column<bool>(type: "INTEGER", nullable: false),
                    FppPollingIntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxSongsPerWindow = table.Column<int>(type: "INTEGER", nullable: false),
                    RateLimitWindowMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultTheme = table.Column<string>(type: "TEXT", nullable: false),
                    MqttEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MqttBrokerHost = table.Column<string>(type: "TEXT", nullable: false),
                    MqttBrokerPort = table.Column<int>(type: "INTEGER", nullable: false),
                    MqttUsername = table.Column<string>(type: "TEXT", nullable: false),
                    MqttPassword = table.Column<string>(type: "TEXT", nullable: false),
                    MqttTopicPrefix = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShowConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThankYous",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThankYous", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionToken = table.Column<string>(type: "TEXT", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Songs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Artist = table.Column<string>(type: "TEXT", nullable: false),
                    PlaylistId = table.Column<int>(type: "INTEGER", nullable: false),
                    Filename = table.Column<string>(type: "TEXT", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Songs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Songs_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QueueItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SongId = table.Column<int>(type: "INTEGER", nullable: false),
                    DonationId = table.Column<int>(type: "INTEGER", nullable: true),
                    SessionToken = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QueueItems_Donations_DonationId",
                        column: x => x.DonationId,
                        principalTable: "Donations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QueueItems_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Playlists",
                columns: new[] { "Id", "FppPlaylistName", "Name" },
                values: new object[] { 1, "HolidayFavorites", "Holiday Favorites" });

            migrationBuilder.InsertData(
                table: "ShowConfigs",
                columns: new[] { "Id", "AutoPlayDefault", "BumpCost", "DefaultPlaylistName", "DefaultTheme", "FmRadioStation", "FppAddress", "FppPollingIntervalSeconds", "HoursEnd", "HoursStart", "InterruptForUserSongs", "MaxSongsPerWindow", "MqttBrokerHost", "MqttBrokerPort", "MqttEnabled", "MqttPassword", "MqttTopicPrefix", "MqttUsername", "RateLimitWindowMinutes", "SiteName", "SocialMediaJson", "SongRequestCost" },
                values: new object[] { 1, true, 1.00m, "", "dark", "", "192.168.1.100", 5, null, null, true, 3, "", 1883, false, "", "xlights", "", 60, "Queue The Magic", "[]", 1.00m });

            migrationBuilder.CreateIndex(
                name: "IX_QueueItems_DonationId",
                table: "QueueItems",
                column: "DonationId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueItems_SongId",
                table: "QueueItems",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_Songs_PlaylistId",
                table: "Songs",
                column: "PlaylistId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QueueItems");

            migrationBuilder.DropTable(
                name: "ShowConfigs");

            migrationBuilder.DropTable(
                name: "ThankYous");

            migrationBuilder.DropTable(
                name: "UserSessions");

            migrationBuilder.DropTable(
                name: "Donations");

            migrationBuilder.DropTable(
                name: "Songs");

            migrationBuilder.DropTable(
                name: "Playlists");
        }
    }
}
