using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SporcuGelisim.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackRecipientsAndWordRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RecipientUserId",
                table: "Feedbacks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AthleteWordRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AthleteProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetCoachUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NormalizedText = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedWordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteWordRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteWordRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AthleteWordRequests_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AthleteWordRequests_AspNetUsers_TargetCoachUserId",
                        column: x => x.TargetCoachUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AthleteWordRequests_AthleteProfiles_AthleteProfileId",
                        column: x => x.AthleteProfileId,
                        principalTable: "AthleteProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AthleteWordRequests_MotivationWords_CreatedWordId",
                        column: x => x.CreatedWordId,
                        principalTable: "MotivationWords",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_RecipientUserId",
                table: "Feedbacks",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteWordRequests_AthleteProfileId_TargetCoachUserId_NormalizedText_Status",
                table: "AthleteWordRequests",
                columns: new[] { "AthleteProfileId", "TargetCoachUserId", "NormalizedText", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AthleteWordRequests_CreatedWordId",
                table: "AthleteWordRequests",
                column: "CreatedWordId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteWordRequests_RequestedByUserId",
                table: "AthleteWordRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteWordRequests_ReviewedByUserId",
                table: "AthleteWordRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteWordRequests_TargetCoachUserId",
                table: "AthleteWordRequests",
                column: "TargetCoachUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_AspNetUsers_RecipientUserId",
                table: "Feedbacks",
                column: "RecipientUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_AspNetUsers_RecipientUserId",
                table: "Feedbacks");

            migrationBuilder.DropTable(
                name: "AthleteWordRequests");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_RecipientUserId",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                table: "Feedbacks");
        }
    }
}
