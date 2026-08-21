using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class AddRecipeDraftsAndRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recipes",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RecipeType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    InternalNotes = table.Column<string>(type: "text", nullable: true),
                    ReferenceServings = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AuthoringStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthoringStageName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AuthoringStageFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ReferenceQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ReferenceUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultAgeGroupScalingApplies = table.Column<bool>(type: "boolean", nullable: true),
                    DraftVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReactivatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReactivatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DerivedFromRecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DerivedFromRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CentralSourceRecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CentralSourceRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantSourceRecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantSourceRevisionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                    table.CheckConstraint("CK_Recipes_ScopeOwner", "(\"ScopeType\" = 0 AND \"ScopeId\" IS NULL) OR (\"ScopeType\" IN (1, 2) AND \"ScopeId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Recipes_MeasurementUnits_ReferenceUnitId",
                        column: x => x.ReferenceUnitId,
                        principalSchema: "catering",
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recipes_Recipes_DerivedFromRecipeId",
                        column: x => x.DerivedFromRecipeId,
                        principalSchema: "catering",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecipeDraftTags",
                schema: "catering",
                columns: table => new
                {
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeDraftTags", x => new { x.RecipeId, x.Value });
                    table.ForeignKey(
                        name: "FK_RecipeDraftTags_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "catering",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeGroups",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeGroups_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "catering",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeRevisions",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SnapshotSchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    RestoredFromRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CentralSubmissionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeRevisions", x => x.Id);
                    table.CheckConstraint("CK_RecipeRevisions_Number_Positive", "\"RevisionNumber\" > 0");
                    table.CheckConstraint("CK_RecipeRevisions_SchemaVersion_Positive", "\"SnapshotSchemaVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_RecipeRevisions_RecipeRevisions_RestoredFromRevisionId",
                        column: x => x.RestoredFromRevisionId,
                        principalSchema: "catering",
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeRevisions_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "catering",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredientPositions",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    BaseIngredientId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ScalingMode = table.Column<int>(type: "integer", nullable: false),
                    AgeGroupScaling = table.Column<int>(type: "integer", nullable: false),
                    StepSize = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    QuantityPerStep = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredientPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientPositions_BaseIngredients_BaseIngredientId",
                        column: x => x.BaseIngredientId,
                        principalSchema: "catering",
                        principalTable: "BaseIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientPositions_MeasurementUnits_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "catering",
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientPositions_RecipeGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "catering",
                        principalTable: "RecipeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientPositions_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "catering",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeRevisionWarnings",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarningCode = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContextJson = table.Column<string>(type: "jsonb", nullable: false),
                    SnapshotPositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SnapshotReplacementId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConflictType = table.Column<int>(type: "integer", nullable: true),
                    ConflictId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcknowledgedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeRevisionWarnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeRevisionWarnings_RecipeRevisions_RecipeRevisionId",
                        column: x => x.RecipeRevisionId,
                        principalSchema: "catering",
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeSubrecipePositions",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipeRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequiredServings = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    RequiredQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    RequiredUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSubrecipePositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipePositions_MeasurementUnits_RequiredUnitId",
                        column: x => x.RequiredUnitId,
                        principalSchema: "catering",
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipePositions_RecipeGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "catering",
                        principalTable: "RecipeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipePositions_RecipeRevisions_RecipeRevisionId",
                        column: x => x.RecipeRevisionId,
                        principalSchema: "catering",
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipePositions_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "catering",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredientReplacements",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientPositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplacementBaseIngredientId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReplacementQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ReplacementUnitId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredientReplacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacements_BaseIngredients_ReplacementBas~",
                        column: x => x.ReplacementBaseIngredientId,
                        principalSchema: "catering",
                        principalTable: "BaseIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacements_MeasurementUnits_ReplacementUn~",
                        column: x => x.ReplacementUnitId,
                        principalSchema: "catering",
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacements_RecipeIngredientPositions_Ingr~",
                        column: x => x.IngredientPositionId,
                        principalSchema: "catering",
                        principalTable: "RecipeIngredientPositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeSubrecipeReplacements",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubrecipePositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplacementRecipeRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReplacementServings = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ReplacementQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ReplacementUnitId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSubrecipeReplacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacements_MeasurementUnits_ReplacementUni~",
                        column: x => x.ReplacementUnitId,
                        principalSchema: "catering",
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacements_RecipeRevisions_ReplacementReci~",
                        column: x => x.ReplacementRecipeRevisionId,
                        principalSchema: "catering",
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacements_RecipeSubrecipePositions_Subrec~",
                        column: x => x.SubrecipePositionId,
                        principalSchema: "catering",
                        principalTable: "RecipeSubrecipePositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredientReplacementAllergens",
                schema: "catering",
                columns: table => new
                {
                    ReplacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllergenId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredientReplacementAllergens", x => new { x.ReplacementId, x.AllergenId });
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacementAllergens_Allergens_AllergenId",
                        column: x => x.AllergenId,
                        principalSchema: "catering",
                        principalTable: "Allergens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacementAllergens_RecipeIngredientReplac~",
                        column: x => x.ReplacementId,
                        principalSchema: "catering",
                        principalTable: "RecipeIngredientReplacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredientReplacementDietaryRequirements",
                schema: "catering",
                columns: table => new
                {
                    ReplacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    DietaryRequirementId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredientReplacementDietaryRequirements", x => new { x.ReplacementId, x.DietaryRequirementId });
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacementDietaryRequirements_DietaryRequi~",
                        column: x => x.DietaryRequirementId,
                        principalSchema: "catering",
                        principalTable: "DietaryRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacementDietaryRequirements_RecipeIngred~",
                        column: x => x.ReplacementId,
                        principalSchema: "catering",
                        principalTable: "RecipeIngredientReplacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredientReplacementIntolerances",
                schema: "catering",
                columns: table => new
                {
                    ReplacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntoleranceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredientReplacementIntolerances", x => new { x.ReplacementId, x.IntoleranceId });
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacementIntolerances_Intolerances_Intole~",
                        column: x => x.IntoleranceId,
                        principalSchema: "catering",
                        principalTable: "Intolerances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientReplacementIntolerances_RecipeIngredientRep~",
                        column: x => x.ReplacementId,
                        principalSchema: "catering",
                        principalTable: "RecipeIngredientReplacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeSubrecipeReplacementAllergens",
                schema: "catering",
                columns: table => new
                {
                    ReplacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllergenId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSubrecipeReplacementAllergens", x => new { x.ReplacementId, x.AllergenId });
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacementAllergens_Allergens_AllergenId",
                        column: x => x.AllergenId,
                        principalSchema: "catering",
                        principalTable: "Allergens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacementAllergens_RecipeSubrecipeReplacem~",
                        column: x => x.ReplacementId,
                        principalSchema: "catering",
                        principalTable: "RecipeSubrecipeReplacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeSubrecipeReplacementDietaryRequirements",
                schema: "catering",
                columns: table => new
                {
                    ReplacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    DietaryRequirementId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSubrecipeReplacementDietaryRequirements", x => new { x.ReplacementId, x.DietaryRequirementId });
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacementDietaryRequirements_DietaryRequir~",
                        column: x => x.DietaryRequirementId,
                        principalSchema: "catering",
                        principalTable: "DietaryRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacementDietaryRequirements_RecipeSubreci~",
                        column: x => x.ReplacementId,
                        principalSchema: "catering",
                        principalTable: "RecipeSubrecipeReplacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeSubrecipeReplacementIntolerances",
                schema: "catering",
                columns: table => new
                {
                    ReplacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntoleranceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSubrecipeReplacementIntolerances", x => new { x.ReplacementId, x.IntoleranceId });
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacementIntolerances_Intolerances_Intoler~",
                        column: x => x.IntoleranceId,
                        principalSchema: "catering",
                        principalTable: "Intolerances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeSubrecipeReplacementIntolerances_RecipeSubrecipeRepla~",
                        column: x => x.ReplacementId,
                        principalSchema: "catering",
                        principalTable: "RecipeSubrecipeReplacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeGroups_RecipeId_SortOrder",
                schema: "catering",
                table: "RecipeGroups",
                columns: new[] { "RecipeId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_BaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                column: "BaseIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_GroupId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_BaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                columns: new[] { "RecipeId", "GroupId", "BaseIngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_SortOrder",
                schema: "catering",
                table: "RecipeIngredientPositions",
                columns: new[] { "RecipeId", "GroupId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_UnitId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientReplacementAllergens_AllergenId",
                schema: "catering",
                table: "RecipeIngredientReplacementAllergens",
                column: "AllergenId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientReplacementDietaryRequirements_DietaryRequi~",
                schema: "catering",
                table: "RecipeIngredientReplacementDietaryRequirements",
                column: "DietaryRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientReplacementIntolerances_IntoleranceId",
                schema: "catering",
                table: "RecipeIngredientReplacementIntolerances",
                column: "IntoleranceId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientReplacements_IngredientPositionId",
                schema: "catering",
                table: "RecipeIngredientReplacements",
                column: "IngredientPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientReplacements_ReplacementBaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientReplacements",
                column: "ReplacementBaseIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientReplacements_ReplacementUnitId",
                schema: "catering",
                table: "RecipeIngredientReplacements",
                column: "ReplacementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeRevisions_RecipeId_RevisionNumber",
                schema: "catering",
                table: "RecipeRevisions",
                columns: new[] { "RecipeId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeRevisions_RestoredFromRevisionId",
                schema: "catering",
                table: "RecipeRevisions",
                column: "RestoredFromRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeRevisionWarnings_RecipeRevisionId",
                schema: "catering",
                table: "RecipeRevisionWarnings",
                column: "RecipeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_DerivedFromRecipeId",
                schema: "catering",
                table: "Recipes",
                column: "DerivedFromRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ReferenceUnitId",
                schema: "catering",
                table: "Recipes",
                column: "ReferenceUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ScopeId",
                schema: "catering",
                table: "Recipes",
                column: "ScopeId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ScopeType_NormalizedName",
                schema: "catering",
                table: "Recipes",
                columns: new[] { "ScopeType", "NormalizedName" },
                unique: true,
                filter: "\"ScopeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ScopeType_ScopeId_NormalizedName",
                schema: "catering",
                table: "Recipes",
                columns: new[] { "ScopeType", "ScopeId", "NormalizedName" },
                unique: true,
                filter: "\"ScopeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_GroupId",
                schema: "catering",
                table: "RecipeSubrecipePositions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_RecipeId_GroupId_RecipeRevisionId",
                schema: "catering",
                table: "RecipeSubrecipePositions",
                columns: new[] { "RecipeId", "GroupId", "RecipeRevisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_RecipeId_GroupId_SortOrder",
                schema: "catering",
                table: "RecipeSubrecipePositions",
                columns: new[] { "RecipeId", "GroupId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_RecipeRevisionId",
                schema: "catering",
                table: "RecipeSubrecipePositions",
                column: "RecipeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_RequiredUnitId",
                schema: "catering",
                table: "RecipeSubrecipePositions",
                column: "RequiredUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipeReplacementAllergens_AllergenId",
                schema: "catering",
                table: "RecipeSubrecipeReplacementAllergens",
                column: "AllergenId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipeReplacementDietaryRequirements_DietaryRequir~",
                schema: "catering",
                table: "RecipeSubrecipeReplacementDietaryRequirements",
                column: "DietaryRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipeReplacementIntolerances_IntoleranceId",
                schema: "catering",
                table: "RecipeSubrecipeReplacementIntolerances",
                column: "IntoleranceId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipeReplacements_ReplacementRecipeRevisionId",
                schema: "catering",
                table: "RecipeSubrecipeReplacements",
                column: "ReplacementRecipeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipeReplacements_ReplacementUnitId",
                schema: "catering",
                table: "RecipeSubrecipeReplacements",
                column: "ReplacementUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipeReplacements_SubrecipePositionId",
                schema: "catering",
                table: "RecipeSubrecipeReplacements",
                column: "SubrecipePositionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeDraftTags",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "RecipeIngredientReplacementAllergens",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "RecipeIngredientReplacementDietaryRequirements",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "RecipeIngredientReplacementIntolerances",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "RecipeRevisionWarnings",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "RecipeSubrecipeReplacementAllergens",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "RecipeSubrecipeReplacementDietaryRequirements",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "RecipeSubrecipeReplacementIntolerances",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "RecipeIngredientReplacements",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "RecipeSubrecipeReplacements",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "RecipeIngredientPositions",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "RecipeSubrecipePositions",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "RecipeGroups",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "RecipeRevisions",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "Recipes",
                schema: "catering");
        }
    }
}
