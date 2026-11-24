using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CandidateCount",
                table: "ClassificationResults",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CandidateHit",
                table: "ClassificationResults",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CandidateCount",
                table: "ClassificationResults");

            migrationBuilder.DropColumn(
                name: "CandidateHit",
                table: "ClassificationResults");
        }
    }
}
