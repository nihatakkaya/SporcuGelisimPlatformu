using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SporcuGelisim.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260725183000_AddAthleteProfileDetailsAndWordAssignments")]
    public partial class AddAthleteProfileDetailsAndWordAssignments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "AthleteProfiles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalIdentityNumber",
                table: "AthleteProfiles",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentPhoneNumber",
                table: "AthleteProfiles",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "AthleteProfiles",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryPhoneNumber",
                table: "AthleteProfiles",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AthleteWordAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AthleteProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MotivationWordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteWordAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteWordAssignments_AspNetUsers_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AthleteWordAssignments_AthleteProfiles_AthleteProfileId",
                        column: x => x.AthleteProfileId,
                        principalTable: "AthleteProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AthleteWordAssignments_MotivationWords_MotivationWordId",
                        column: x => x.MotivationWordId,
                        principalTable: "MotivationWords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AthleteWordAssignments_AssignedByUserId",
                table: "AthleteWordAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteWordAssignments_AthleteProfileId_MotivationWordId_IsActive",
                table: "AthleteWordAssignments",
                columns: new[] { "AthleteProfileId", "MotivationWordId", "IsActive" },
                unique: true,
                filter: "[IsActive] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteWordAssignments_MotivationWordId",
                table: "AthleteWordAssignments",
                column: "MotivationWordId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AthleteWordAssignments");

            migrationBuilder.DropColumn(name: "Address", table: "AthleteProfiles");
            migrationBuilder.DropColumn(name: "NationalIdentityNumber", table: "AthleteProfiles");
            migrationBuilder.DropColumn(name: "ParentPhoneNumber", table: "AthleteProfiles");
            migrationBuilder.DropColumn(name: "PhoneNumber", table: "AthleteProfiles");
            migrationBuilder.DropColumn(name: "SecondaryPhoneNumber", table: "AthleteProfiles");
        }
    }
}
