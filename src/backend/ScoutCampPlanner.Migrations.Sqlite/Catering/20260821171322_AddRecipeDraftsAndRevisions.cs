using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddRecipeDraftsAndRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScopeType = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipeType = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: true),
                    InternalNotes = table.Column<string>(type: "TEXT", nullable: true),
                    ReferenceServings = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    AuthoringStageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AuthoringStageName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AuthoringStageFactor = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    ReferenceQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    ReferenceUnitId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DefaultAgeGroupScalingApplies = table.Column<bool>(type: "INTEGER", nullable: true),
                    DraftVersion = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ArchivedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ReactivatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReactivatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DerivedFromRecipeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DerivedFromRevisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CentralSourceRecipeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CentralSourceRevisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantSourceRecipeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantSourceRevisionId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                    table.CheckConstraint("CK_Recipes_ScopeOwner", "(\"ScopeType\" = 0 AND \"ScopeId\" IS NULL) OR (\"ScopeType\" IN (1, 2) AND \"ScopeId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Recipes_MeasurementUnits_ReferenceUnitId",
                        column: x => x.ReferenceUnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recipes_Recipes_DerivedFromRecipeId",
                        column: x => x.DerivedFromRecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecipeDraftTags",
                columns: table => new
                {
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeDraftTags", x => new { x.RecipeId, x.Value });
                    table.ForeignKey(
                        name: "FK_RecipeDraftTags_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeGroups_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChangeNote = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SnapshotSchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    RestoredFromRevisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CentralSubmissionId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeRevisions", x => x.Id);
                    table.CheckConstraint("CK_RecipeRevisions_Number_Positive", "\"RevisionNumber\" > 0");
                    table.CheckConstraint("CK_RecipeRevisions_SchemaVersion_Positive", "\"SnapshotSchemaVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_RecipeRevisions_RecipeRevisions_RestoredFromRevisionId",
                        column: x => x.RestoredFromRevisionId,
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeRevisions_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredientPositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BaseIngredientId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ScalingMode = table.Column<int>(type: "INTEGER", nullable: false),
                    AgeGroupScaling = table.Column<int>(type: "INTEGER", nullable: false),
                    StepSize = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    QuantityPerStep = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredientPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientPositions_BaseIngredients_BaseIngredientId",
                        column: x => x.BaseIngredientId,
                        principalTable: "BaseIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientPositions_MeasurementUnits_UnitId",
                        column: x => x.UnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientPositions_RecipeGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "RecipeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientPositions_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeRevisionWarnings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WarningCode = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ContextJson = table.Column<string>(type: "TEXT", nullable: false),
                    SnapshotPositionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SnapshotReplacementId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ConflictType = table.Column<int>(type: "INTEGER", nullable: true),
                    ConflictId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AcknowledgedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeRevisionWarnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeRevisionWarnings_RecipeRevisions_RecipeRevisionId",
                        column: x => x.RecipeRevisionId,
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeSubrecipePositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecipeRevisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RequiredServings = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    RequiredQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    RequiredUnitId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSubrecipePositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipePositions_MeasurementUnits_RequiredUnitId",
                        column: x => x.RequiredUnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipePositions_RecipeGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "RecipeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipePositions_RecipeRevisions_RecipeRevisionId",
                        column: x => x.RecipeRevisionId,
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipePositions_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredientReplacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IngredientPositionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReplacementBaseIngredientId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReplacementQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    ReplacementUnitId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredientReplacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacements_BaseIngredients_ReplacementBaseIngredientId",
                        column: x => x.ReplacementBaseIngredientId,
                        principalTable: "BaseIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacements_MeasurementUnits_ReplacementUnitId",
                        column: x => x.ReplacementUnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacements_RecipeIngredientPositions_IngredientPositionId",
                        column: x => x.IngredientPositionId,
                        principalTable: "RecipeIngredientPositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeSubrecipeReplacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubrecipePositionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReplacementRecipeRevisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReplacementServings = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    ReplacementQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    ReplacementUnitId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSubrecipeReplacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacements_MeasurementUnits_ReplacementUnitId",
                        column: x => x.ReplacementUnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacements_RecipeRevisions_ReplacementRecipeRevisionId",
                        column: x => x.ReplacementRecipeRevisionId,
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacements_RecipeSubrecipePositions_SubrecipePositionId",
                        column: x => x.SubrecipePositionId,
                        principalTable: "RecipeSubrecipePositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredientReplacementAllergens",
                columns: table => new
                {
                    ReplacementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllergenId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredientReplacementAllergens", x => new { x.ReplacementId, x.AllergenId });
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacementAllergens_Allergens_AllergenId",
                        column: x => x.AllergenId,
                        principalTable: "Allergens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacementAllergens_RecipeIngredientReplacements_ReplacementId",
                        column: x => x.ReplacementId,
                        principalTable: "RecipeIngredientReplacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredientReplacementDietaryRequirements",
                columns: table => new
                {
                    ReplacementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DietaryRequirementId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredientReplacementDietaryRequirements", x => new { x.ReplacementId, x.DietaryRequirementId });
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacementDietaryRequirements_DietaryRequirements_DietaryRequirementId",
                        column: x => x.DietaryRequirementId,
                        principalTable: "DietaryRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacementDietaryRequirements_RecipeIngredientReplacements_ReplacementId",
                        column: x => x.ReplacementId,
                        principalTable: "RecipeIngredientReplacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredientReplacementIntolerances",
                columns: table => new
                {
                    ReplacementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IntoleranceId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredientReplacementIntolerances", x => new { x.ReplacementId, x.IntoleranceId });
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacementIntolerances_Intolerances_IntoleranceId",
                        column: x => x.IntoleranceId,
                        principalTable: "Intolerances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacementIntolerances_RecipeIngredientReplacements_ReplacementId",
                        column: x => x.ReplacementId,
                        principalTable: "RecipeIngredientReplacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeSubrecipeReplacementAllergens",
                columns: table => new
                {
                    ReplacementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllergenId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSubrecipeReplacementAllergens", x => new { x.ReplacementId, x.AllergenId });
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacementAllergens_Allergens_AllergenId",
                        column: x => x.AllergenId,
                        principalTable: "Allergens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacementAllergens_RecipeSubrecipeReplacements_ReplacementId",
                        column: x => x.ReplacementId,
                        principalTable: "RecipeSubrecipeReplacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeSubrecipeReplacementDietaryRequirements",
                columns: table => new
                {
                    ReplacementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DietaryRequirementId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSubrecipeReplacementDietaryRequirements", x => new { x.ReplacementId, x.DietaryRequirementId });
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacementDietaryRequirements_DietaryRequirements_DietaryRequirementId",
                        column: x => x.DietaryRequirementId,
                        principalTable: "DietaryRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacementDietaryRequirements_RecipeSubrecipeReplacements_ReplacementId",
                        column: x => x.ReplacementId,
                        principalTable: "RecipeSubrecipeReplacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeSubrecipeReplacementIntolerances",
                columns: table => new
                {
                    ReplacementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IntoleranceId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSubrecipeReplacementIntolerances", x => new { x.ReplacementId, x.IntoleranceId });
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacementIntolerances_Intolerances_IntoleranceId",
                        column: x => x.IntoleranceId,
                        principalTable: "Intolerances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacementIntolerances_RecipeSubrecipeReplacements_ReplacementId",
                        column: x => x.ReplacementId,
                        principalTable: "RecipeSubrecipeReplacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeGroups_RecipeId_SortOrder",
                table: "RecipeGroups",
                columns: new[] { "RecipeId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_BaseIngredientId",
                table: "RecipeIngredientPositions",
                column: "BaseIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_GroupId",
                table: "RecipeIngredientPositions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_BaseIngredientId",
                table: "RecipeIngredientPositions",
                columns: new[] { "RecipeId", "GroupId", "BaseIngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_SortOrder",
                table: "RecipeIngredientPositions",
                columns: new[] { "RecipeId", "GroupId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_UnitId",
                table: "RecipeIngredientPositions",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientReplacementAllergens_AllergenId",
                table: "RecipeIngredientReplacementAllergens",
                column: "AllergenId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientReplacementDietaryRequirements_DietaryRequirementId",
                table: "RecipeIngredientReplacementDietaryRequirements",
                column: "DietaryRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientReplacementIntolerances_IntoleranceId",
                table: "RecipeIngredientReplacementIntolerances",
                column: "IntoleranceId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientReplacements_IngredientPositionId",
                table: "RecipeIngredientReplacements",
                column: "IngredientPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientReplacements_ReplacementBaseIngredientId",
                table: "RecipeIngredientReplacements",
                column: "ReplacementBaseIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientReplacements_ReplacementUnitId",
                table: "RecipeIngredientReplacements",
                column: "ReplacementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeRevisions_RecipeId_RevisionNumber",
                table: "RecipeRevisions",
                columns: new[] { "RecipeId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeRevisions_RestoredFromRevisionId",
                table: "RecipeRevisions",
                column: "RestoredFromRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeRevisionWarnings_RecipeRevisionId",
                table: "RecipeRevisionWarnings",
                column: "RecipeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_DerivedFromRecipeId",
                table: "Recipes",
                column: "DerivedFromRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ReferenceUnitId",
                table: "Recipes",
                column: "ReferenceUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ScopeId",
                table: "Recipes",
                column: "ScopeId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ScopeType_NormalizedName",
                table: "Recipes",
                columns: new[] { "ScopeType", "NormalizedName" },
                unique: true,
                filter: "\"ScopeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ScopeType_ScopeId_NormalizedName",
                table: "Recipes",
                columns: new[] { "ScopeType", "ScopeId", "NormalizedName" },
                unique: true,
                filter: "\"ScopeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_GroupId",
                table: "RecipeSubrecipePositions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_RecipeId_GroupId_RecipeRevisionId",
                table: "RecipeSubrecipePositions",
                columns: new[] { "RecipeId", "GroupId", "RecipeRevisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_RecipeId_GroupId_SortOrder",
                table: "RecipeSubrecipePositions",
                columns: new[] { "RecipeId", "GroupId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_RecipeRevisionId",
                table: "RecipeSubrecipePositions",
                column: "RecipeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_RequiredUnitId",
                table: "RecipeSubrecipePositions",
                column: "RequiredUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipeReplacementAllergens_AllergenId",
                table: "RecipeSubrecipeReplacementAllergens",
                column: "AllergenId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipeReplacementDietaryRequirements_DietaryRequirementId",
                table: "RecipeSubrecipeReplacementDietaryRequirements",
                column: "DietaryRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipeReplacementIntolerances_IntoleranceId",
                table: "RecipeSubrecipeReplacementIntolerances",
                column: "IntoleranceId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipeReplacements_ReplacementRecipeRevisionId",
                table: "RecipeSubrecipeReplacements",
                column: "ReplacementRecipeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipeReplacements_ReplacementUnitId",
                table: "RecipeSubrecipeReplacements",
                column: "ReplacementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipeReplacements_SubrecipePositionId",
                table: "RecipeSubrecipeReplacements",
                column: "SubrecipePositionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeDraftTags");

            migrationBuilder.DropTable(
                name: "RecipeIngredientReplacementAllergens");

            migrationBuilder.DropTable(
                name: "RecipeIngredientReplacementDietaryRequirements");

            migrationBuilder.DropTable(
                name: "RecipeIngredientReplacementIntolerances");

            migrationBuilder.DropTable(
                name: "RecipeRevisionWarnings");

            migrationBuilder.DropTable(
                name: "RecipeSubrecipeReplacementAllergens");

            migrationBuilder.DropTable(
                name: "RecipeSubrecipeReplacementDietaryRequirements");

            migrationBuilder.DropTable(
                name: "RecipeSubrecipeReplacementIntolerances");

            migrationBuilder.DropTable(
                name: "RecipeIngredientReplacements");

            migrationBuilder.DropTable(
                name: "RecipeSubrecipeReplacements");

            migrationBuilder.DropTable(
                name: "RecipeIngredientPositions");

            migrationBuilder.DropTable(
                name: "RecipeSubrecipePositions");

            migrationBuilder.DropTable(
                name: "RecipeGroups");

            migrationBuilder.DropTable(
                name: "RecipeRevisions");

            migrationBuilder.DropTable(
                name: "Recipes");
        }
    }
}
