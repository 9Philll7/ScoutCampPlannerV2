using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddMealPlanningIncrementOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "MealPlans",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "MealPlans",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChangeVersion",
                table: "CampMeals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CookingUnitGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingUnitGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MealPlanOfferGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampMealId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlanOfferGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlanOfferGroups_CampMeals_CampMealId",
                        column: x => x.CampMealId,
                        principalTable: "CampMeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MealPlanOfferGroups_MealPlans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealPlanSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    SavedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlanSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlanSnapshots_MealPlans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CookingUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StandardMealPlanId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CookingUnits_CookingUnitGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "CookingUnitGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CookingUnits_MealPlans_StandardMealPlanId",
                        column: x => x.StandardMealPlanId,
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MealPlanEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfferGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsStandard = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Role = table.Column<int>(type: "INTEGER", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlanEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlanEntries_MealPlanOfferGroups_OfferGroupId",
                        column: x => x.OfferGroupId,
                        principalTable: "MealPlanOfferGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CookingUnitMealStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CookingUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampMealId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscriptionState = table.Column<int>(type: "INTEGER", nullable: false),
                    DemandOverride = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    CalculatedDemand = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    EffectiveDemand = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MealPlanVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    MealPlanSnapshotId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CalculationSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    SourceFingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    WarningsJson = table.Column<string>(type: "text", nullable: true),
                    CalculatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingUnitMealStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealStates_CampMeals_CampMealId",
                        column: x => x.CampMealId,
                        principalTable: "CampMeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealStates_CookingUnits_CookingUnitId",
                        column: x => x.CookingUnitId,
                        principalTable: "CookingUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealStates_MealPlanSnapshots_MealPlanSnapshotId",
                        column: x => x.MealPlanSnapshotId,
                        principalTable: "MealPlanSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealStates_MealPlans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CookingUnitStructureAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CookingUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampMealId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StructureNodeId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingUnitStructureAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CookingUnitStructureAssignments_CampMeals_CampMealId",
                        column: x => x.CampMealId,
                        principalTable: "CampMeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CookingUnitStructureAssignments_CookingUnits_CookingUnitId",
                        column: x => x.CookingUnitId,
                        principalTable: "CookingUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CookingUnitMealOfferTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CookingUnitMealStateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfferGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetOverride = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingUnitMealOfferTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealOfferTargets_CookingUnitMealStates_CookingUnitMealStateId",
                        column: x => x.CookingUnitMealStateId,
                        principalTable: "CookingUnitMealStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealOfferTargets_MealPlanOfferGroups_OfferGroupId",
                        column: x => x.OfferGroupId,
                        principalTable: "MealPlanOfferGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CookingUnitMealRecipeChoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CookingUnitMealStateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfferGroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MealPlanEntryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingUnitMealRecipeChoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealRecipeChoices_CookingUnitMealStates_CookingUnitMealStateId",
                        column: x => x.CookingUnitMealStateId,
                        principalTable: "CookingUnitMealStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealRecipeChoices_MealPlanEntries_MealPlanEntryId",
                        column: x => x.MealPlanEntryId,
                        principalTable: "MealPlanEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealRecipeChoices_MealPlanOfferGroups_OfferGroupId",
                        column: x => x.OfferGroupId,
                        principalTable: "MealPlanOfferGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MealPlans_CampId_SortOrder",
                table: "MealPlans",
                columns: new[] { "CampId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitGroups_CampId_Name",
                table: "CookingUnitGroups",
                columns: new[] { "CampId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitGroups_CampId_SortOrder",
                table: "CookingUnitGroups",
                columns: new[] { "CampId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealOfferTargets_CookingUnitMealStateId_OfferGroupId",
                table: "CookingUnitMealOfferTargets",
                columns: new[] { "CookingUnitMealStateId", "OfferGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealOfferTargets_OfferGroupId",
                table: "CookingUnitMealOfferTargets",
                column: "OfferGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealRecipeChoices_CookingUnitMealStateId_SortOrder",
                table: "CookingUnitMealRecipeChoices",
                columns: new[] { "CookingUnitMealStateId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealRecipeChoices_MealPlanEntryId",
                table: "CookingUnitMealRecipeChoices",
                column: "MealPlanEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealRecipeChoices_OfferGroupId",
                table: "CookingUnitMealRecipeChoices",
                column: "OfferGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealRecipeChoices_RecipeRevisionId",
                table: "CookingUnitMealRecipeChoices",
                column: "RecipeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealStates_CampId",
                table: "CookingUnitMealStates",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealStates_CampMealId",
                table: "CookingUnitMealStates",
                column: "CampMealId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealStates_CookingUnitId_CampMealId",
                table: "CookingUnitMealStates",
                columns: new[] { "CookingUnitId", "CampMealId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealStates_MealPlanId",
                table: "CookingUnitMealStates",
                column: "MealPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealStates_MealPlanSnapshotId",
                table: "CookingUnitMealStates",
                column: "MealPlanSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnits_CampId_Name",
                table: "CookingUnits",
                columns: new[] { "CampId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnits_CampId_SortOrder",
                table: "CookingUnits",
                columns: new[] { "CampId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnits_GroupId",
                table: "CookingUnits",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnits_StandardMealPlanId",
                table: "CookingUnits",
                column: "StandardMealPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitStructureAssignments_CampId",
                table: "CookingUnitStructureAssignments",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitStructureAssignments_CampMealId",
                table: "CookingUnitStructureAssignments",
                column: "CampMealId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitStructureAssignments_CookingUnitId_CampMealId_StructureNodeId",
                table: "CookingUnitStructureAssignments",
                columns: new[] { "CookingUnitId", "CampMealId", "StructureNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanEntries_OfferGroupId_RecipeRevisionId",
                table: "MealPlanEntries",
                columns: new[] { "OfferGroupId", "RecipeRevisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanEntries_OfferGroupId_SortOrder",
                table: "MealPlanEntries",
                columns: new[] { "OfferGroupId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanEntries_RecipeRevisionId",
                table: "MealPlanEntries",
                column: "RecipeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanOfferGroups_CampMealId",
                table: "MealPlanOfferGroups",
                column: "CampMealId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanOfferGroups_MealPlanId_CampMealId_SortOrder",
                table: "MealPlanOfferGroups",
                columns: new[] { "MealPlanId", "CampMealId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanSnapshots_CampId",
                table: "MealPlanSnapshots",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanSnapshots_MealPlanId_Version",
                table: "MealPlanSnapshots",
                columns: new[] { "MealPlanId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CookingUnitMealOfferTargets");

            migrationBuilder.DropTable(
                name: "CookingUnitMealRecipeChoices");

            migrationBuilder.DropTable(
                name: "CookingUnitStructureAssignments");

            migrationBuilder.DropTable(
                name: "CookingUnitMealStates");

            migrationBuilder.DropTable(
                name: "MealPlanEntries");

            migrationBuilder.DropTable(
                name: "CookingUnits");

            migrationBuilder.DropTable(
                name: "MealPlanSnapshots");

            migrationBuilder.DropTable(
                name: "MealPlanOfferGroups");

            migrationBuilder.DropTable(
                name: "CookingUnitGroups");

            migrationBuilder.DropIndex(
                name: "IX_MealPlans_CampId_SortOrder",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "ChangeVersion",
                table: "CampMeals");
        }
    }
}
