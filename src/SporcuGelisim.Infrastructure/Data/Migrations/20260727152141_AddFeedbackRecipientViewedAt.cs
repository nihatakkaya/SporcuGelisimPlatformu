using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SporcuGelisim.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackRecipientViewedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RecipientViewedAt",
                table: "Feedbacks",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecipientViewedAt",
                table: "Feedbacks");
        }
    }
}
