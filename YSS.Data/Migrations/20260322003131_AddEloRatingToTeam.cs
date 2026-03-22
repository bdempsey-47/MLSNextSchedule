using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEloRatingToTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EloRating",
                table: "Teams",
                type: "int",
                nullable: false,
                defaultValue: 1500);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EloRating",
                table: "Teams");
        }
    }
}
