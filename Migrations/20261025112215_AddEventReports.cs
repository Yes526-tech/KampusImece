using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KampusImece.Migrations
{
    /// <inheritdoc />
    public partial class AddEventReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CleanupEvents_Users_CreatorId",
                table: "CleanupEvents");

            migrationBuilder.CreateTable(
                name: "EventReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdminNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventReports_CleanupEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "CleanupEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventReports_Users_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventReports_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventReports_EventId",
                table: "EventReports",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventReports_ReporterUserId",
                table: "EventReports",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventReports_ReviewedByUserId",
                table: "EventReports",
                column: "ReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CleanupEvents_Users_CreatorId",
                table: "CleanupEvents",
                column: "CreatorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CleanupEvents_Users_CreatorId",
                table: "CleanupEvents");

            migrationBuilder.DropTable(
                name: "EventReports");

            migrationBuilder.AddForeignKey(
                name: "FK_CleanupEvents_Users_CreatorId",
                table: "CleanupEvents",
                column: "CreatorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
