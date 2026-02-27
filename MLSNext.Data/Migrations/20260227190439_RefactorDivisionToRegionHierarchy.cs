using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLSNext.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorDivisionToRegionHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Divisions_DivisionId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Divisions_Name",
                table: "Divisions");

            migrationBuilder.RenameColumn(
                name: "DivisionId",
                table: "Matches",
                newName: "RegionId");

            migrationBuilder.RenameIndex(
                name: "IX_Matches_DivisionId",
                table: "Matches",
                newName: "IX_Matches_RegionId");

            migrationBuilder.AddColumn<int>(
                name: "LeagueId",
                table: "Divisions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TournamentId",
                table: "Divisions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Leagues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leagues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DivisionId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Regions_Divisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "Divisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_LeagueId_Name",
                table: "Divisions",
                columns: new[] { "LeagueId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_Name",
                table: "Leagues",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regions_DivisionId_Name",
                table: "Regions",
                columns: new[] { "DivisionId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Divisions_Leagues_LeagueId",
                table: "Divisions",
                column: "LeagueId",
                principalTable: "Leagues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Regions_RegionId",
                table: "Matches",
                column: "RegionId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Divisions_Leagues_LeagueId",
                table: "Divisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Regions_RegionId",
                table: "Matches");

            migrationBuilder.DropTable(
                name: "Leagues");

            migrationBuilder.DropTable(
                name: "Regions");

            migrationBuilder.DropIndex(
                name: "IX_Divisions_LeagueId_Name",
                table: "Divisions");

            migrationBuilder.DropColumn(
                name: "LeagueId",
                table: "Divisions");

            migrationBuilder.DropColumn(
                name: "TournamentId",
                table: "Divisions");

            migrationBuilder.RenameColumn(
                name: "RegionId",
                table: "Matches",
                newName: "DivisionId");

            migrationBuilder.RenameIndex(
                name: "IX_Matches_RegionId",
                table: "Matches",
                newName: "IX_Matches_DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_Name",
                table: "Divisions",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Divisions_DivisionId",
                table: "Matches",
                column: "DivisionId",
                principalTable: "Divisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
