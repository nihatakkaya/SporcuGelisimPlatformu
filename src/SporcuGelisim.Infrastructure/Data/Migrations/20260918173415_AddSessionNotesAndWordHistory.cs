using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SporcuGelisim.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionNotesAndWordHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasWordHistory",
                table: "AthleteSessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PrivateCoachNote",
                table: "AthleteSessions",
                type: "nvarchar(max)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharedNote",
                table: "AthleteSessions",
                type: "nvarchar(max)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AthleteWordChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AthleteProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MotivationWordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WordText = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Added = table.Column<bool>(type: "bit", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteWordChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteWordChanges_AthleteProfiles_AthleteProfileId",
                        column: x => x.AthleteProfileId,
                        principalTable: "AthleteProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AthleteWordChanges_AthleteSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AthleteSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionWordSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MotivationWordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WordText = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsBeginning = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionWordSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionWordSnapshots_AthleteSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AthleteSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AthleteWordChanges_AthleteProfileId_SessionId",
                table: "AthleteWordChanges",
                columns: new[] { "AthleteProfileId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AthleteWordChanges_SessionId",
                table: "AthleteWordChanges",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionWordSnapshots_SessionId_MotivationWordId_IsBeginning",
                table: "SessionWordSnapshots",
                columns: new[] { "SessionId", "MotivationWordId", "IsBeginning" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AthleteWordChanges");

            migrationBuilder.DropTable(
                name: "SessionWordSnapshots");

            migrationBuilder.DropColumn(
                name: "HasWordHistory",
                table: "AthleteSessions");

            migrationBuilder.DropColumn(
                name: "PrivateCoachNote",
                table: "AthleteSessions");

            migrationBuilder.DropColumn(
                name: "SharedNote",
                table: "AthleteSessions");
        }
    }
}
