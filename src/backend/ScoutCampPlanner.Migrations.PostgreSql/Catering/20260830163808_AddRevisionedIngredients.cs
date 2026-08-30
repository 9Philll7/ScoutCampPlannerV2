using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class AddRevisionedIngredients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngredientAllergenDefinitions",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentAllergenId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsEuMajorAllergen = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientAllergenDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientAllergenDefinitions_IngredientAllergenDefinitions~",
                        column: x => x.ParentAllergenId,
                        principalSchema: "catering",
                        principalTable: "IngredientAllergenDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientCategories",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientCategories_IngredientCategories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalSchema: "catering",
                        principalTable: "IngredientCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientIntoleranceDefinitions",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsQuantityDependent = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientIntoleranceDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IngredientOriginProperties",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsAnimalOrigin = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientOriginProperties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IngredientIdentities",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceIngredientId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentPublishedRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientIdentities", x => x.Id);
                    table.CheckConstraint("CK_IngredientIdentities_ScopeOwner", "(\"ScopeType\" = 0 AND \"ScopeId\" IS NULL) OR (\"ScopeType\" IN (1, 2) AND \"ScopeId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_IngredientIdentities_IngredientIdentities_SourceIngredientId",
                        column: x => x.SourceIngredientId,
                        principalSchema: "catering",
                        principalTable: "IngredientIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientRevisions",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    BasedOnRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MergedCentralRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllergenReviewState = table.Column<int>(type: "integer", nullable: false),
                    IntoleranceReviewState = table.Column<int>(type: "integer", nullable: false),
                    OriginReviewState = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientRevisions", x => x.Id);
                    table.CheckConstraint("CK_IngredientRevisions_Number_Positive", "\"RevisionNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_IngredientRevisions_IngredientCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "catering",
                        principalTable: "IngredientCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisions_IngredientIdentities_IngredientId",
                        column: x => x.IngredientId,
                        principalSchema: "catering",
                        principalTable: "IngredientIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientRevisions_IngredientRevisions_BasedOnRevisionId",
                        column: x => x.BasedOnRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisions_IngredientRevisions_MergedCentralRevisi~",
                        column: x => x.MergedCentralRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisions_MeasurementUnits_BaseUnitId",
                        column: x => x.BaseUnitId,
                        principalSchema: "catering",
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientRevisionAllergens",
                schema: "catering",
                columns: table => new
                {
                    IngredientRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllergenId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientRevisionAllergens", x => new { x.IngredientRevisionId, x.AllergenId });
                    table.ForeignKey(
                        name: "FK_IngredientRevisionAllergens_IngredientAllergenDefinitions_A~",
                        column: x => x.AllergenId,
                        principalSchema: "catering",
                        principalTable: "IngredientAllergenDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionAllergens_IngredientRevisions_IngredientR~",
                        column: x => x.IngredientRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientRevisionIntolerances",
                schema: "catering",
                columns: table => new
                {
                    IngredientRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntoleranceId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientRevisionIntolerances", x => new { x.IngredientRevisionId, x.IntoleranceId });
                    table.ForeignKey(
                        name: "FK_IngredientRevisionIntolerances_IngredientIntoleranceDefinit~",
                        column: x => x.IntoleranceId,
                        principalSchema: "catering",
                        principalTable: "IngredientIntoleranceDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionIntolerances_IngredientRevisions_Ingredie~",
                        column: x => x.IngredientRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientRevisionOrigins",
                schema: "catering",
                columns: table => new
                {
                    IngredientRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginPropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientRevisionOrigins", x => new { x.IngredientRevisionId, x.OriginPropertyId });
                    table.ForeignKey(
                        name: "FK_IngredientRevisionOrigins_IngredientOriginProperties_Origin~",
                        column: x => x.OriginPropertyId,
                        principalSchema: "catering",
                        principalTable: "IngredientOriginProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionOrigins_IngredientRevisions_IngredientRev~",
                        column: x => x.IngredientRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientRevisionUnitConversions",
                schema: "catering",
                columns: table => new
                {
                    IngredientRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactorToBaseUnit = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Precision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientRevisionUnitConversions", x => new { x.IngredientRevisionId, x.SourceUnitId });
                    table.CheckConstraint("CK_IngredientRevisionUnitConversions_Factor_Positive", "\"FactorToBaseUnit\" > 0");
                    table.ForeignKey(
                        name: "FK_IngredientRevisionUnitConversions_IngredientRevisions_Ingre~",
                        column: x => x.IngredientRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionUnitConversions_MeasurementUnits_SourceUn~",
                        column: x => x.SourceUnitId,
                        principalSchema: "catering",
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariantRevisions",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariantRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientVariantRevisions_IngredientRevisions_IngredientRe~",
                        column: x => x.IngredientRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariantAllergenOverrides",
                schema: "catering",
                columns: table => new
                {
                    VariantRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllergenId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariantAllergenOverrides", x => new { x.VariantRevisionId, x.AllergenId });
                    table.ForeignKey(
                        name: "FK_IngredientVariantAllergenOverrides_IngredientAllergenDefini~",
                        column: x => x.AllergenId,
                        principalSchema: "catering",
                        principalTable: "IngredientAllergenDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientVariantAllergenOverrides_IngredientVariantRevisio~",
                        column: x => x.VariantRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientVariantRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariantIntoleranceOverrides",
                schema: "catering",
                columns: table => new
                {
                    VariantRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntoleranceId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariantIntoleranceOverrides", x => new { x.VariantRevisionId, x.IntoleranceId });
                    table.ForeignKey(
                        name: "FK_IngredientVariantIntoleranceOverrides_IngredientIntolerance~",
                        column: x => x.IntoleranceId,
                        principalSchema: "catering",
                        principalTable: "IngredientIntoleranceDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientVariantIntoleranceOverrides_IngredientVariantRevi~",
                        column: x => x.VariantRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientVariantRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariantOriginOverrides",
                schema: "catering",
                columns: table => new
                {
                    VariantRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginPropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariantOriginOverrides", x => new { x.VariantRevisionId, x.OriginPropertyId });
                    table.ForeignKey(
                        name: "FK_IngredientVariantOriginOverrides_IngredientOriginProperties~",
                        column: x => x.OriginPropertyId,
                        principalSchema: "catering",
                        principalTable: "IngredientOriginProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientVariantOriginOverrides_IngredientVariantRevisions~",
                        column: x => x.VariantRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientVariantRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariantUnitConversionOverrides",
                schema: "catering",
                columns: table => new
                {
                    VariantRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactorToBaseUnit = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Precision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariantUnitConversionOverrides", x => new { x.VariantRevisionId, x.SourceUnitId });
                    table.CheckConstraint("CK_IngredientVariantUnitConversionOverrides_Factor_Positive", "\"FactorToBaseUnit\" > 0");
                    table.ForeignKey(
                        name: "FK_IngredientVariantUnitConversionOverrides_IngredientVariantR~",
                        column: x => x.VariantRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientVariantRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientVariantUnitConversionOverrides_MeasurementUnits_S~",
                        column: x => x.SourceUnitId,
                        principalSchema: "catering",
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientAllergenDefinitions_Code",
                schema: "catering",
                table: "IngredientAllergenDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientAllergenDefinitions_ParentAllergenId",
                schema: "catering",
                table: "IngredientAllergenDefinitions",
                column: "ParentAllergenId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCategories_Code",
                schema: "catering",
                table: "IngredientCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCategories_NormalizedName",
                schema: "catering",
                table: "IngredientCategories",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCategories_ParentCategoryId",
                schema: "catering",
                table: "IngredientCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientIdentities_CurrentPublishedRevisionId",
                schema: "catering",
                table: "IngredientIdentities",
                column: "CurrentPublishedRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientIdentities_ScopeType_ScopeId",
                schema: "catering",
                table: "IngredientIdentities",
                columns: new[] { "ScopeType", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientIdentities_SourceIngredientId_SourceRevisionId",
                schema: "catering",
                table: "IngredientIdentities",
                columns: new[] { "SourceIngredientId", "SourceRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientIdentities_SourceRevisionId",
                schema: "catering",
                table: "IngredientIdentities",
                column: "SourceRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientIntoleranceDefinitions_Code",
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientOriginProperties_Code",
                schema: "catering",
                table: "IngredientOriginProperties",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionAllergens_AllergenId",
                schema: "catering",
                table: "IngredientRevisionAllergens",
                column: "AllergenId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionIntolerances_IntoleranceId",
                schema: "catering",
                table: "IngredientRevisionIntolerances",
                column: "IntoleranceId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionOrigins_OriginPropertyId",
                schema: "catering",
                table: "IngredientRevisionOrigins",
                column: "OriginPropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisions_BasedOnRevisionId",
                schema: "catering",
                table: "IngredientRevisions",
                column: "BasedOnRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisions_BaseUnitId",
                schema: "catering",
                table: "IngredientRevisions",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisions_CategoryId",
                schema: "catering",
                table: "IngredientRevisions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisions_IngredientId",
                schema: "catering",
                table: "IngredientRevisions",
                column: "IngredientId",
                unique: true,
                filter: "\"State\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisions_IngredientId_RevisionNumber",
                schema: "catering",
                table: "IngredientRevisions",
                columns: new[] { "IngredientId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisions_MergedCentralRevisionId",
                schema: "catering",
                table: "IngredientRevisions",
                column: "MergedCentralRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionUnitConversions_SourceUnitId",
                schema: "catering",
                table: "IngredientRevisionUnitConversions",
                column: "SourceUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantAllergenOverrides_AllergenId",
                schema: "catering",
                table: "IngredientVariantAllergenOverrides",
                column: "AllergenId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantIntoleranceOverrides_IntoleranceId",
                schema: "catering",
                table: "IngredientVariantIntoleranceOverrides",
                column: "IntoleranceId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantOriginOverrides_OriginPropertyId",
                schema: "catering",
                table: "IngredientVariantOriginOverrides",
                column: "OriginPropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantRevisions_IngredientRevisionId_NormalizedN~",
                schema: "catering",
                table: "IngredientVariantRevisions",
                columns: new[] { "IngredientRevisionId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantRevisions_IngredientRevisionId_VariantKey",
                schema: "catering",
                table: "IngredientVariantRevisions",
                columns: new[] { "IngredientRevisionId", "VariantKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantUnitConversionOverrides_SourceUnitId",
                schema: "catering",
                table: "IngredientVariantUnitConversionOverrides",
                column: "SourceUnitId");

            migrationBuilder.Sql(
                """
                INSERT INTO catering."IngredientCategories"
                    ("Id", "Code", "Name", "NormalizedName", "Status", "ParentCategoryId")
                VALUES
                    ('11111111-1111-1111-1111-000000000001', 'LEGACY_UNCLASSIFIED',
                     'Nicht klassifiziert', 'NICHT KLASSIFIZIERT', 0, NULL);

                INSERT INTO catering."MeasurementUnits"
                    ("Id", "Name", "NormalizedName", "Symbol", "Dimension", "BaseUnitFactor")
                VALUES
                    ('11111111-1111-1111-1111-000000000002', 'Nicht festgelegt',
                     'NICHT FESTGELEGT', '[legacy-unit]', 2, 1);

                INSERT INTO catering."IngredientOriginProperties"
                    ("Id", "Code", "Name", "IsAnimalOrigin", "Status")
                VALUES
                    ('11111111-1111-1111-1111-000000000003', 'UNKNOWN_ORIGIN',
                     'Unbekannte Herkunft', FALSE, 0);

                INSERT INTO catering."IngredientAllergenDefinitions"
                    ("Id", "Code", "Name", "IsEuMajorAllergen", "Status", "ParentAllergenId")
                SELECT "Id", 'LEGACY_' || REPLACE("Id"::text, '-', ''), "Name", FALSE, 0, NULL
                FROM catering."Allergens";

                INSERT INTO catering."IngredientIntoleranceDefinitions"
                    ("Id", "Code", "Name", "IsQuantityDependent", "Status")
                SELECT "Id", 'LEGACY_' || REPLACE("Id"::text, '-', ''), "Name", FALSE, 0
                FROM catering."Intolerances";

                INSERT INTO catering."IngredientIdentities"
                    ("Id", "ScopeType", "ScopeId", "SourceIngredientId", "SourceRevisionId",
                     "CurrentPublishedRevisionId", "Status")
                SELECT "Id", "ScopeType", "ScopeId", NULL, NULL, NULL, 0
                FROM catering."BaseIngredients";

                INSERT INTO catering."IngredientRevisions"
                    ("Id", "IngredientId", "RevisionNumber", "State", "BasedOnRevisionId",
                     "MergedCentralRevisionId", "Name", "NormalizedName", "CategoryId", "BaseUnitId",
                     "AllergenReviewState", "IntoleranceReviewState", "OriginReviewState", "RowVersion",
                     "CreatedAtUtc", "CreatedBy", "UpdatedAtUtc", "UpdatedBy", "PublishedAtUtc", "PublishedBy")
                SELECT b."Id", b."Id", 1, 1, NULL, NULL, b."Name", b."NormalizedName",
                       '11111111-1111-1111-1111-000000000001',
                       COALESCE(
                           (SELECT c."UnitId" FROM catering."IngredientUnitConversions" c
                            WHERE c."BaseIngredientId" = b."Id"
                            ORDER BY CASE WHEN c."ReferenceQuantityPerUnit" = 1 THEN 0 ELSE 1 END, c."UnitId"
                            LIMIT 1),
                           '11111111-1111-1111-1111-000000000002'),
                       0, 0, 0, 1,
                       CURRENT_TIMESTAMP, '11111111-1111-1111-1111-000000000004',
                       CURRENT_TIMESTAMP, '11111111-1111-1111-1111-000000000004',
                       CURRENT_TIMESTAMP, '11111111-1111-1111-1111-000000000004'
                FROM catering."BaseIngredients" b;

                INSERT INTO catering."IngredientRevisionAllergens"
                    ("IngredientRevisionId", "AllergenId", "State", "Source")
                SELECT "BaseIngredientId", "AllergenId", 0, 1
                FROM catering."BaseIngredientAllergens";

                INSERT INTO catering."IngredientRevisionIntolerances"
                    ("IngredientRevisionId", "IntoleranceId", "State", "Source")
                SELECT "BaseIngredientId", "IntoleranceId", 0, 1
                FROM catering."BaseIngredientIntolerances";

                INSERT INTO catering."IngredientRevisionOrigins"
                    ("IngredientRevisionId", "OriginPropertyId", "State", "Source")
                SELECT "Id", '11111111-1111-1111-1111-000000000003', 3, 3
                FROM catering."BaseIngredients";

                INSERT INTO catering."IngredientRevisionUnitConversions"
                    ("IngredientRevisionId", "SourceUnitId", "FactorToBaseUnit", "Precision")
                SELECT c."BaseIngredientId", c."UnitId", c."ReferenceQuantityPerUnit", 1
                FROM catering."IngredientUnitConversions" c
                WHERE c."UnitId" <> (
                    SELECT selected."UnitId" FROM catering."IngredientUnitConversions" selected
                    WHERE selected."BaseIngredientId" = c."BaseIngredientId"
                    ORDER BY CASE WHEN selected."ReferenceQuantityPerUnit" = 1 THEN 0 ELSE 1 END,
                             selected."UnitId"
                    LIMIT 1);

                INSERT INTO catering."IngredientVariantRevisions"
                    ("Id", "IngredientRevisionId", "VariantKey", "Name", "NormalizedName", "Status", "SortOrder")
                SELECT "Id", "BaseIngredientId", 'legacy_' || REPLACE("Id"::text, '-', ''),
                       "Name", "NormalizedName", 0, 0
                FROM catering."IngredientVariants";

                UPDATE catering."IngredientIdentities"
                SET "CurrentPublishedRevisionId" = "Id";
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_IngredientIdentities_IngredientRevisions_CurrentPublishedRe~",
                schema: "catering",
                table: "IngredientIdentities",
                column: "CurrentPublishedRevisionId",
                principalSchema: "catering",
                principalTable: "IngredientRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IngredientIdentities_IngredientRevisions_SourceRevisionId",
                schema: "catering",
                table: "IngredientIdentities",
                column: "SourceRevisionId",
                principalSchema: "catering",
                principalTable: "IngredientRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IngredientIdentities_IngredientRevisions_CurrentPublishedRe~",
                schema: "catering",
                table: "IngredientIdentities");

            migrationBuilder.DropForeignKey(
                name: "FK_IngredientIdentities_IngredientRevisions_SourceRevisionId",
                schema: "catering",
                table: "IngredientIdentities");

            migrationBuilder.DropTable(
                name: "IngredientRevisionAllergens",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientRevisionIntolerances",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientRevisionOrigins",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientRevisionUnitConversions",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientVariantAllergenOverrides",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientVariantIntoleranceOverrides",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientVariantOriginOverrides",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientVariantUnitConversionOverrides",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientAllergenDefinitions",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientIntoleranceDefinitions",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientOriginProperties",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientVariantRevisions",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientRevisions",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientCategories",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientIdentities",
                schema: "catering");
        }
    }
}
