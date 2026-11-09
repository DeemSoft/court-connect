using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DeemSoft.CourtConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Courts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nickname = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourtId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "time", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    MinimumAge = table.Column<int>(type: "int", nullable: false),
                    PricePerPlayer = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Games_Courts_CourtId",
                        column: x => x.CourtId,
                        principalTable: "Courts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Registrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Registrations_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Registrations_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Courts",
                columns: new[] { "Id", "Location", "Name" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "Barcelona", "Central" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "Girona", "North" }
                });

            migrationBuilder.InsertData(
                table: "Players",
                columns: new[] { "Id", "DateOfBirth", "Email", "FullName", "Nickname" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), new DateOnly(2002, 4, 18), "maria@example.com", "Maria López", "maria" },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), new DateOnly(1999, 8, 3), "pau@example.com", "Pau García", "pau" },
                    { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new DateOnly(2005, 11, 28), "irene@example.com", "Irene Soler", "irene" },
                    { new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), new DateOnly(2001, 6, 10), "leo@example.com", "Leo Torres", "leo" },
                    { new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), new DateOnly(2003, 2, 14), "sara@example.com", "Sara Valls", "sara" }
                });

            migrationBuilder.InsertData(
                table: "Games",
                columns: new[] { "Id", "Capacity", "CourtId", "Duration", "MinimumAge", "PricePerPlayer", "StartAt", "Status" },
                values: new object[,]
                {
                    { new Guid("33333333-3333-3333-3333-333333333333"), 4, new Guid("11111111-1111-1111-1111-111111111111"), new TimeSpan(0, 1, 30, 0, 0), 16, 12.50m, new DateTimeOffset(new DateTime(2026, 1, 12, 18, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 1, 0, 0, 0)), "Open" },
                    { new Guid("44444444-4444-4444-4444-444444444444"), 2, new Guid("22222222-2222-2222-2222-222222222222"), new TimeSpan(0, 2, 0, 0, 0), 18, 15m, new DateTimeOffset(new DateTime(2026, 1, 15, 20, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 1, 0, 0, 0)), "Open" },
                    { new Guid("55555555-5555-5555-5555-555555555555"), 4, new Guid("11111111-1111-1111-1111-111111111111"), new TimeSpan(0, 1, 0, 0, 0), 16, 10m, new DateTimeOffset(new DateTime(2026, 1, 10, 20, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 1, 0, 0, 0)), "Closed" }
                });

            migrationBuilder.InsertData(
                table: "Registrations",
                columns: new[] { "Id", "Amount", "CreatedAt", "GameId", "PlayerId", "Status" },
                values: new object[,]
                {
                    { new Guid("11111111-0001-0000-0000-000000000001"), 12.50m, new DateTimeOffset(new DateTime(2026, 1, 3, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 1, 0, 0, 0)), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Confirmed" },
                    { new Guid("11111111-0001-0000-0000-000000000002"), 12.50m, new DateTimeOffset(new DateTime(2026, 1, 4, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 1, 0, 0, 0)), new Guid("33333333-3333-3333-3333-333333333333"), new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Confirmed" },
                    { new Guid("11111111-0001-0000-0000-000000000003"), 15m, new DateTimeOffset(new DateTime(2026, 1, 5, 18, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 1, 0, 0, 0)), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), "Confirmed" },
                    { new Guid("11111111-0001-0000-0000-000000000004"), 15m, new DateTimeOffset(new DateTime(2026, 1, 5, 19, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 1, 0, 0, 0)), new Guid("44444444-4444-4444-4444-444444444444"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), "Confirmed" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_CourtId",
                table: "Games",
                column: "CourtId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Nickname",
                table: "Players",
                column: "Nickname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_GameId_PlayerId",
                table: "Registrations",
                columns: new[] { "GameId", "PlayerId" },
                unique: true,
                filter: "[Status] = 'Confirmed'");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_PlayerId",
                table: "Registrations",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Registrations");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Courts");
        }
    }
}
