using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutClientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "Workouts",
                type: "uniqueidentifier",
                nullable: false,
                // Not the generated empty Guid: existing rows need distinct values, or the unique index below fails.
                // Only here, not in the model: SQLite (bUnit tests, EnsureCreated) has no NEWID().
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_Workouts_ClientId",
                table: "Workouts",
                column: "ClientId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Workouts_ClientId",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Workouts");
        }
    }
}
