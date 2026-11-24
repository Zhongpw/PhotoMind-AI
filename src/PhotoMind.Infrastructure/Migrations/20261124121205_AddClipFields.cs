using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClipFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "ClassificationResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Alias",
                table: "Categories",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "ClassificationResults");

            migrationBuilder.DropColumn(
                name: "Alias",
                table: "Categories");
        }
    }
}
