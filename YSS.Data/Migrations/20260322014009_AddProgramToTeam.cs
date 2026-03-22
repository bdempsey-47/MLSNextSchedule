using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramToTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_Name",
                table: "Teams");

            migrationBuilder.AddColumn<string>(
                name: "Program",
                table: "Teams",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "HG");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Name_Program",
                table: "Teams",
                columns: new[] { "Name", "Program" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_Name_Program",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "Program",
                table: "Teams");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Name",
                table: "Teams",
                column: "Name",
                unique: true);
        }
    }
}
