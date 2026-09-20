using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class AddMealPlanningIncrementOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                schema: "catering",
                table: "MealPlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "catering",
                table: "MealPlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChangeVersion",
                schema: "catering",
                table: "CampMeals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CookingUnitGroups",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingUnitGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MealPlanOfferGroups",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampMealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlanOfferGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlanOfferGroups_CampMeals_CampMealId",
                        column: x => x.CampMealId,
                        principalSchema: "catering",
                        principalTable: "CampMeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MealPlanOfferGroups_MealPlans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalSchema: "catering",
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealPlanSnapshots",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    SavedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlanSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlanSnapshots_MealPlans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalSchema: "catering",
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CookingUnits",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardMealPlanId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CookingUnits_CookingUnitGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "catering",
                        principalTable: "CookingUnitGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CookingUnits_MealPlans_StandardMealPlanId",
                        column: x => x.StandardMealPlanId,
                        principalSchema: "catering",
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MealPlanEntries",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsStandard = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlanEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlanEntries_MealPlanOfferGroups_OfferGroupId",
                        column: x => x.OfferGroupId,
                        principalSchema: "catering",
                        principalTable: "MealPlanOfferGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CookingUnitMealStates",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    CookingUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampMealId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionState = table.Column<int>(type: "integer", nullable: false),
                    DemandOverride = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    CalculatedDemand = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    EffectiveDemand = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    MealPlanVersion = table.Column<int>(type: "integer", nullable: true),
                    MealPlanSnapshotId = table.Column<Guid>(type: "uuid", nullable: true),
                    CalculationSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    SourceFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WarningsJson = table.Column<string>(type: "text", nullable: true),
                    CalculatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingUnitMealStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealStates_CampMeals_CampMealId",
                        column: x => x.CampMealId,
                        principalSchema: "catering",
                        principalTable: "CampMeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealStates_CookingUnits_CookingUnitId",
                        column: x => x.CookingUnitId,
                        principalSchema: "catering",
                        principalTable: "CookingUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealStates_MealPlanSnapshots_MealPlanSnapshotId",
                        column: x => x.MealPlanSnapshotId,
                        principalSchema: "catering",
                        principalTable: "MealPlanSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealStates_MealPlans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalSchema: "catering",
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CookingUnitStructureAssignments",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    CookingUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampMealId = table.Column<Guid>(type: "uuid", nullable: true),
                    StructureNodeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingUnitStructureAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CookingUnitStructureAssignments_CampMeals_CampMealId",
                        column: x => x.CampMealId,
                        principalSchema: "catering",
                        principalTable: "CampMeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CookingUnitStructureAssignments_CookingUnits_CookingUnitId",
                        column: x => x.CookingUnitId,
                        principalSchema: "catering",
                        principalTable: "CookingUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CookingUnitMealOfferTargets",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CookingUnitMealStateId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetOverride = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingUnitMealOfferTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealOfferTargets_CookingUnitMealStates_CookingUn~",
                        column: x => x.CookingUnitMealStateId,
                        principalSchema: "catering",
                        principalTable: "CookingUnitMealStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealOfferTargets_MealPlanOfferGroups_OfferGroupId",
                        column: x => x.OfferGroupId,
                        principalSchema: "catering",
                        principalTable: "MealPlanOfferGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CookingUnitMealRecipeChoices",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CookingUnitMealStateId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    MealPlanEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingUnitMealRecipeChoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealRecipeChoices_CookingUnitMealStates_CookingU~",
                        column: x => x.CookingUnitMealStateId,
                        principalSchema: "catering",
                        principalTable: "CookingUnitMealStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealRecipeChoices_MealPlanEntries_MealPlanEntryId",
                        column: x => x.MealPlanEntryId,
                        principalSchema: "catering",
                        principalTable: "MealPlanEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CookingUnitMealRecipeChoices_MealPlanOfferGroups_OfferGroup~",
                        column: x => x.OfferGroupId,
                        principalSchema: "catering",
                        principalTable: "MealPlanOfferGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MealPlans_CampId_SortOrder",
                schema: "catering",
                table: "MealPlans",
                columns: new[] { "CampId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitGroups_CampId_Name",
                schema: "catering",
                table: "CookingUnitGroups",
                columns: new[] { "CampId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitGroups_CampId_SortOrder",
                schema: "catering",
                table: "CookingUnitGroups",
                columns: new[] { "CampId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealOfferTargets_CookingUnitMealStateId_OfferGro~",
                schema: "catering",
                table: "CookingUnitMealOfferTargets",
                columns: new[] { "CookingUnitMealStateId", "OfferGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealOfferTargets_OfferGroupId",
                schema: "catering",
                table: "CookingUnitMealOfferTargets",
                column: "OfferGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealRecipeChoices_CookingUnitMealStateId_SortOrd~",
                schema: "catering",
                table: "CookingUnitMealRecipeChoices",
                columns: new[] { "CookingUnitMealStateId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealRecipeChoices_MealPlanEntryId",
                schema: "catering",
                table: "CookingUnitMealRecipeChoices",
                column: "MealPlanEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealRecipeChoices_OfferGroupId",
                schema: "catering",
                table: "CookingUnitMealRecipeChoices",
                column: "OfferGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealRecipeChoices_RecipeRevisionId",
                schema: "catering",
                table: "CookingUnitMealRecipeChoices",
                column: "RecipeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealStates_CampId",
                schema: "catering",
                table: "CookingUnitMealStates",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealStates_CampMealId",
                schema: "catering",
                table: "CookingUnitMealStates",
                column: "CampMealId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealStates_CookingUnitId_CampMealId",
                schema: "catering",
                table: "CookingUnitMealStates",
                columns: new[] { "CookingUnitId", "CampMealId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealStates_MealPlanId",
                schema: "catering",
                table: "CookingUnitMealStates",
                column: "MealPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitMealStates_MealPlanSnapshotId",
                schema: "catering",
                table: "CookingUnitMealStates",
                column: "MealPlanSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnits_CampId_Name",
                schema: "catering",
                table: "CookingUnits",
                columns: new[] { "CampId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnits_CampId_SortOrder",
                schema: "catering",
                table: "CookingUnits",
                columns: new[] { "CampId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnits_GroupId",
                schema: "catering",
                table: "CookingUnits",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnits_StandardMealPlanId",
                schema: "catering",
                table: "CookingUnits",
                column: "StandardMealPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitStructureAssignments_CampId",
                schema: "catering",
                table: "CookingUnitStructureAssignments",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitStructureAssignments_CampMealId",
                schema: "catering",
                table: "CookingUnitStructureAssignments",
                column: "CampMealId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnitStructureAssignments_CookingUnitId_CampMealId_St~",
                schema: "catering",
                table: "CookingUnitStructureAssignments",
                columns: new[] { "CookingUnitId", "CampMealId", "StructureNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanEntries_OfferGroupId_RecipeRevisionId",
                schema: "catering",
                table: "MealPlanEntries",
                columns: new[] { "OfferGroupId", "RecipeRevisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanEntries_OfferGroupId_SortOrder",
                schema: "catering",
                table: "MealPlanEntries",
                columns: new[] { "OfferGroupId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanEntries_RecipeRevisionId",
                schema: "catering",
                table: "MealPlanEntries",
                column: "RecipeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanOfferGroups_CampMealId",
                schema: "catering",
                table: "MealPlanOfferGroups",
                column: "CampMealId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanOfferGroups_MealPlanId_CampMealId_SortOrder",
                schema: "catering",
                table: "MealPlanOfferGroups",
                columns: new[] { "MealPlanId", "CampMealId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanSnapshots_CampId",
                schema: "catering",
                table: "MealPlanSnapshots",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanSnapshots_MealPlanId_Version",
                schema: "catering",
                table: "MealPlanSnapshots",
                columns: new[] { "MealPlanId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CookingUnitMealOfferTargets",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "CookingUnitMealRecipeChoices",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "CookingUnitStructureAssignments",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "CookingUnitMealStates",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "MealPlanEntries",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "CookingUnits",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "MealPlanSnapshots",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "MealPlanOfferGroups",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "CookingUnitGroups",
                schema: "catering");

            migrationBuilder.DropIndex(
                name: "IX_MealPlans_CampId_SortOrder",
                schema: "catering",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                schema: "catering",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "catering",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "ChangeVersion",
                schema: "catering",
                table: "CampMeals");
        }
    }
}
