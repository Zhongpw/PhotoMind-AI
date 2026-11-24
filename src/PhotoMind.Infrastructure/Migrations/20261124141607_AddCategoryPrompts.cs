using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryPrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PromptsJson",
                table: "Categories",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromptsJson",
                table: "Categories");
        }
    }
}
