using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class AddCampMealSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampMeals",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    MealTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampMeals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampMealTypes",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampMealTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampMeals_CampId_Date_MealTypeId",
                schema: "catering",
                table: "CampMeals",
                columns: new[] { "CampId", "Date", "MealTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampMeals_MealTypeId",
                schema: "catering",
                table: "CampMeals",
                column: "MealTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CampMealTypes_CampId_NormalizedName",
                schema: "catering",
                table: "CampMealTypes",
                columns: new[] { "CampId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampMealTypes_CampId_SortOrder",
                schema: "catering",
                table: "CampMealTypes",
                columns: new[] { "CampId", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampMeals",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "CampMealTypes",
                schema: "catering");
        }
    }
}
