using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddCampMealSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampMeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MealTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampMeals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampMealTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampMealTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampMeals_CampId_Date_MealTypeId",
                table: "CampMeals",
                columns: new[] { "CampId", "Date", "MealTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampMeals_MealTypeId",
                table: "CampMeals",
                column: "MealTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CampMealTypes_CampId_NormalizedName",
                table: "CampMealTypes",
                columns: new[] { "CampId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampMealTypes_CampId_SortOrder",
                table: "CampMealTypes",
                columns: new[] { "CampId", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampMeals");

            migrationBuilder.DropTable(
                name: "CampMealTypes");
        }
    }
}
