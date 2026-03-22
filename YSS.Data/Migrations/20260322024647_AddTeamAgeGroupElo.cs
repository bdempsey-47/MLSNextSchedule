using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamAgeGroupElo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamAgeGroupElos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    AgeGroupId = table.Column<int>(type: "int", nullable: false),
                    EloRating = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamAgeGroupElos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamAgeGroupElos_AgeGroups_AgeGroupId",
                        column: x => x.AgeGroupId,
                        principalTable: "AgeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamAgeGroupElos_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamAgeGroupElos_AgeGroupId",
                table: "TeamAgeGroupElos",
                column: "AgeGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamAgeGroupElos_TeamId_AgeGroupId",
                table: "TeamAgeGroupElos",
                columns: new[] { "TeamId", "AgeGroupId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamAgeGroupElos");
        }
    }
}
