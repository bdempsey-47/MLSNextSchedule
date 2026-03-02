using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLSNext.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedInitialLeagues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed initial leagues for multi-league support
            migrationBuilder.InsertData(
                table: "Leagues",
                columns: new[] { "Name" },
                values: new object[,]
                {
                    { "MLS Next" },
                    { "ECNL" },
                    { "EDP" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove seeded leagues
            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Name",
                keyValue: "MLS Next");

            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Name",
                keyValue: "ECNL");

            migrationBuilder.DeleteData(
                table: "Leagues",
                keyColumn: "Name",
                keyValue: "EDP");
        }
    }
}
