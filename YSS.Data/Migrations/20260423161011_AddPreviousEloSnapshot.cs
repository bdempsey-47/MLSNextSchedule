using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPreviousEloSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreviousEloRating",
                table: "TeamAgeGroupElos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreviousEloSnapshotAt",
                table: "TeamAgeGroupElos",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousEloRating",
                table: "TeamAgeGroupElos");

            migrationBuilder.DropColumn(
                name: "PreviousEloSnapshotAt",
                table: "TeamAgeGroupElos");
        }
    }
}
