using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddRevisionedIngredients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngredientAllergenDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentAllergenId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    IsEuMajorAllergen = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientAllergenDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientAllergenDefinitions_IngredientAllergenDefinitions_ParentAllergenId",
                        column: x => x.ParentAllergenId,
                        principalTable: "IngredientAllergenDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientCategories_IngredientCategories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "IngredientCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientIntoleranceDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    IsQuantityDependent = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientIntoleranceDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IngredientOriginProperties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    IsAnimalOrigin = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientOriginProperties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IngredientIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScopeType = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceIngredientId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceRevisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CurrentPublishedRevisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientIdentities", x => x.Id);
                    table.CheckConstraint("CK_IngredientIdentities_ScopeOwner", "(\"ScopeType\" = 0 AND \"ScopeId\" IS NULL) OR (\"ScopeType\" IN (1, 2) AND \"ScopeId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_IngredientIdentities_IngredientIdentities_SourceIngredientId",
                        column: x => x.SourceIngredientId,
                        principalTable: "IngredientIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IngredientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    BasedOnRevisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MergedCentralRevisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BaseUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllergenReviewState = table.Column<int>(type: "INTEGER", nullable: false),
                    IntoleranceReviewState = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginReviewState = table.Column<int>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PublishedBy = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientRevisions", x => x.Id);
                    table.CheckConstraint("CK_IngredientRevisions_Number_Positive", "\"RevisionNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_IngredientRevisions_IngredientCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "IngredientCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisions_IngredientIdentities_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "IngredientIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientRevisions_IngredientRevisions_BasedOnRevisionId",
                        column: x => x.BasedOnRevisionId,
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisions_IngredientRevisions_MergedCentralRevisionId",
                        column: x => x.MergedCentralRevisionId,
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisions_MeasurementUnits_BaseUnitId",
                        column: x => x.BaseUnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientRevisionAllergens",
                columns: table => new
                {
                    IngredientRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllergenId = table.Column<Guid>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientRevisionAllergens", x => new { x.IngredientRevisionId, x.AllergenId });
                    table.ForeignKey(
                        name: "FK_IngredientRevisionAllergens_IngredientAllergenDefinitions_AllergenId",
                        column: x => x.AllergenId,
                        principalTable: "IngredientAllergenDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionAllergens_IngredientRevisions_IngredientRevisionId",
                        column: x => x.IngredientRevisionId,
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientRevisionIntolerances",
                columns: table => new
                {
                    IngredientRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IntoleranceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientRevisionIntolerances", x => new { x.IngredientRevisionId, x.IntoleranceId });
                    table.ForeignKey(
                        name: "FK_IngredientRevisionIntolerances_IngredientIntoleranceDefinitions_IntoleranceId",
                        column: x => x.IntoleranceId,
                        principalTable: "IngredientIntoleranceDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionIntolerances_IngredientRevisions_IngredientRevisionId",
                        column: x => x.IngredientRevisionId,
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientRevisionOrigins",
                columns: table => new
                {
                    IngredientRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginPropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientRevisionOrigins", x => new { x.IngredientRevisionId, x.OriginPropertyId });
                    table.ForeignKey(
                        name: "FK_IngredientRevisionOrigins_IngredientOriginProperties_OriginPropertyId",
                        column: x => x.OriginPropertyId,
                        principalTable: "IngredientOriginProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionOrigins_IngredientRevisions_IngredientRevisionId",
                        column: x => x.IngredientRevisionId,
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientRevisionUnitConversions",
                columns: table => new
                {
                    IngredientRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FactorToBaseUnit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    Precision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientRevisionUnitConversions", x => new { x.IngredientRevisionId, x.SourceUnitId });
                    table.CheckConstraint("CK_IngredientRevisionUnitConversions_Factor_Positive", "\"FactorToBaseUnit\" > 0");
                    table.ForeignKey(
                        name: "FK_IngredientRevisionUnitConversions_IngredientRevisions_IngredientRevisionId",
                        column: x => x.IngredientRevisionId,
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionUnitConversions_MeasurementUnits_SourceUnitId",
                        column: x => x.SourceUnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariantRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IngredientRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VariantKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariantRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientVariantRevisions_IngredientRevisions_IngredientRevisionId",
                        column: x => x.IngredientRevisionId,
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariantAllergenOverrides",
                columns: table => new
                {
                    VariantRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllergenId = table.Column<Guid>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariantAllergenOverrides", x => new { x.VariantRevisionId, x.AllergenId });
                    table.ForeignKey(
                        name: "FK_IngredientVariantAllergenOverrides_IngredientAllergenDefinitions_AllergenId",
                        column: x => x.AllergenId,
                        principalTable: "IngredientAllergenDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientVariantAllergenOverrides_IngredientVariantRevisions_VariantRevisionId",
                        column: x => x.VariantRevisionId,
                        principalTable: "IngredientVariantRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariantIntoleranceOverrides",
                columns: table => new
                {
                    VariantRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IntoleranceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariantIntoleranceOverrides", x => new { x.VariantRevisionId, x.IntoleranceId });
                    table.ForeignKey(
                        name: "FK_IngredientVariantIntoleranceOverrides_IngredientIntoleranceDefinitions_IntoleranceId",
                        column: x => x.IntoleranceId,
                        principalTable: "IngredientIntoleranceDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientVariantIntoleranceOverrides_IngredientVariantRevisions_VariantRevisionId",
                        column: x => x.VariantRevisionId,
                        principalTable: "IngredientVariantRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariantOriginOverrides",
                columns: table => new
                {
                    VariantRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginPropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariantOriginOverrides", x => new { x.VariantRevisionId, x.OriginPropertyId });
                    table.ForeignKey(
                        name: "FK_IngredientVariantOriginOverrides_IngredientOriginProperties_OriginPropertyId",
                        column: x => x.OriginPropertyId,
                        principalTable: "IngredientOriginProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientVariantOriginOverrides_IngredientVariantRevisions_VariantRevisionId",
                        column: x => x.VariantRevisionId,
                        principalTable: "IngredientVariantRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariantUnitConversionOverrides",
                columns: table => new
                {
                    VariantRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FactorToBaseUnit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    Precision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariantUnitConversionOverrides", x => new { x.VariantRevisionId, x.SourceUnitId });
                    table.CheckConstraint("CK_IngredientVariantUnitConversionOverrides_Factor_Positive", "\"FactorToBaseUnit\" > 0");
                    table.ForeignKey(
                        name: "FK_IngredientVariantUnitConversionOverrides_IngredientVariantRevisions_VariantRevisionId",
                        column: x => x.VariantRevisionId,
                        principalTable: "IngredientVariantRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientVariantUnitConversionOverrides_MeasurementUnits_SourceUnitId",
                        column: x => x.SourceUnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientAllergenDefinitions_Code",
                table: "IngredientAllergenDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientAllergenDefinitions_ParentAllergenId",
                table: "IngredientAllergenDefinitions",
                column: "ParentAllergenId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCategories_Code",
                table: "IngredientCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCategories_NormalizedName",
                table: "IngredientCategories",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCategories_ParentCategoryId",
                table: "IngredientCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientIdentities_CurrentPublishedRevisionId",
                table: "IngredientIdentities",
                column: "CurrentPublishedRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientIdentities_ScopeType_ScopeId",
                table: "IngredientIdentities",
                columns: new[] { "ScopeType", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientIdentities_SourceIngredientId_SourceRevisionId",
                table: "IngredientIdentities",
                columns: new[] { "SourceIngredientId", "SourceRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientIdentities_SourceRevisionId",
                table: "IngredientIdentities",
                column: "SourceRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientIntoleranceDefinitions_Code",
                table: "IngredientIntoleranceDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientOriginProperties_Code",
                table: "IngredientOriginProperties",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionAllergens_AllergenId",
                table: "IngredientRevisionAllergens",
                column: "AllergenId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionIntolerances_IntoleranceId",
                table: "IngredientRevisionIntolerances",
                column: "IntoleranceId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionOrigins_OriginPropertyId",
                table: "IngredientRevisionOrigins",
                column: "OriginPropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisions_BasedOnRevisionId",
                table: "IngredientRevisions",
                column: "BasedOnRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisions_BaseUnitId",
                table: "IngredientRevisions",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisions_CategoryId",
                table: "IngredientRevisions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisions_IngredientId",
                table: "IngredientRevisions",
                column: "IngredientId",
                unique: true,
                filter: "\"State\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisions_IngredientId_RevisionNumber",
                table: "IngredientRevisions",
                columns: new[] { "IngredientId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisions_MergedCentralRevisionId",
                table: "IngredientRevisions",
                column: "MergedCentralRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionUnitConversions_SourceUnitId",
                table: "IngredientRevisionUnitConversions",
                column: "SourceUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantAllergenOverrides_AllergenId",
                table: "IngredientVariantAllergenOverrides",
                column: "AllergenId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantIntoleranceOverrides_IntoleranceId",
                table: "IngredientVariantIntoleranceOverrides",
                column: "IntoleranceId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantOriginOverrides_OriginPropertyId",
                table: "IngredientVariantOriginOverrides",
                column: "OriginPropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantRevisions_IngredientRevisionId_NormalizedName",
                table: "IngredientVariantRevisions",
                columns: new[] { "IngredientRevisionId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantRevisions_IngredientRevisionId_VariantKey",
                table: "IngredientVariantRevisions",
                columns: new[] { "IngredientRevisionId", "VariantKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantUnitConversionOverrides_SourceUnitId",
                table: "IngredientVariantUnitConversionOverrides",
                column: "SourceUnitId");

            migrationBuilder.Sql(
                """
                INSERT INTO "IngredientCategories"
                    ("Id", "Code", "Name", "NormalizedName", "Status", "ParentCategoryId")
                VALUES
                    ('11111111-1111-1111-1111-000000000001', 'LEGACY_UNCLASSIFIED',
                     'Nicht klassifiziert', 'NICHT KLASSIFIZIERT', 0, NULL);

                INSERT INTO "MeasurementUnits"
                    ("Id", "Name", "NormalizedName", "Symbol", "Dimension", "BaseUnitFactor")
                VALUES
                    ('11111111-1111-1111-1111-000000000002', 'Nicht festgelegt',
                     'NICHT FESTGELEGT', '[legacy-unit]', 2, 1);

                INSERT INTO "IngredientOriginProperties"
                    ("Id", "Code", "Name", "IsAnimalOrigin", "Status")
                VALUES
                    ('11111111-1111-1111-1111-000000000003', 'UNKNOWN_ORIGIN',
                     'Unbekannte Herkunft', 0, 0);

                INSERT INTO "IngredientAllergenDefinitions"
                    ("Id", "Code", "Name", "IsEuMajorAllergen", "Status", "ParentAllergenId")
                SELECT "Id", 'LEGACY_' || REPLACE("Id", '-', ''), "Name", 0, 0, NULL
                FROM "Allergens";

                INSERT INTO "IngredientIntoleranceDefinitions"
                    ("Id", "Code", "Name", "IsQuantityDependent", "Status")
                SELECT "Id", 'LEGACY_' || REPLACE("Id", '-', ''), "Name", 0, 0
                FROM "Intolerances";

                INSERT INTO "IngredientIdentities"
                    ("Id", "ScopeType", "ScopeId", "SourceIngredientId", "SourceRevisionId",
                     "CurrentPublishedRevisionId", "Status")
                SELECT "Id", "ScopeType", "ScopeId", NULL, NULL, NULL, 0
                FROM "BaseIngredients";

                INSERT INTO "IngredientRevisions"
                    ("Id", "IngredientId", "RevisionNumber", "State", "BasedOnRevisionId",
                     "MergedCentralRevisionId", "Name", "NormalizedName", "CategoryId", "BaseUnitId",
                     "AllergenReviewState", "IntoleranceReviewState", "OriginReviewState", "RowVersion",
                     "CreatedAtUtc", "CreatedBy", "UpdatedAtUtc", "UpdatedBy", "PublishedAtUtc", "PublishedBy")
                SELECT b."Id", b."Id", 1, 1, NULL, NULL, b."Name", b."NormalizedName",
                       '11111111-1111-1111-1111-000000000001',
                       COALESCE(
                           (SELECT c."UnitId" FROM "IngredientUnitConversions" c
                            WHERE c."BaseIngredientId" = b."Id"
                            ORDER BY CASE WHEN c."ReferenceQuantityPerUnit" = 1 THEN 0 ELSE 1 END, c."UnitId"
                            LIMIT 1),
                           '11111111-1111-1111-1111-000000000002'),
                       0, 0, 0, 1,
                       STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now'), '11111111-1111-1111-1111-000000000004',
                       STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now'), '11111111-1111-1111-1111-000000000004',
                       STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now'), '11111111-1111-1111-1111-000000000004'
                FROM "BaseIngredients" b;

                INSERT INTO "IngredientRevisionAllergens"
                    ("IngredientRevisionId", "AllergenId", "State", "Source")
                SELECT "BaseIngredientId", "AllergenId", 0, 1
                FROM "BaseIngredientAllergens";

                INSERT INTO "IngredientRevisionIntolerances"
                    ("IngredientRevisionId", "IntoleranceId", "State", "Source")
                SELECT "BaseIngredientId", "IntoleranceId", 0, 1
                FROM "BaseIngredientIntolerances";

                INSERT INTO "IngredientRevisionOrigins"
                    ("IngredientRevisionId", "OriginPropertyId", "State", "Source")
                SELECT "Id", '11111111-1111-1111-1111-000000000003', 3, 3
                FROM "BaseIngredients";

                INSERT INTO "IngredientRevisionUnitConversions"
                    ("IngredientRevisionId", "SourceUnitId", "FactorToBaseUnit", "Precision")
                SELECT c."BaseIngredientId", c."UnitId", c."ReferenceQuantityPerUnit", 1
                FROM "IngredientUnitConversions" c
                WHERE c."UnitId" <> (
                    SELECT selected."UnitId" FROM "IngredientUnitConversions" selected
                    WHERE selected."BaseIngredientId" = c."BaseIngredientId"
                    ORDER BY CASE WHEN selected."ReferenceQuantityPerUnit" = 1 THEN 0 ELSE 1 END,
                             selected."UnitId"
                    LIMIT 1);

                INSERT INTO "IngredientVariantRevisions"
                    ("Id", "IngredientRevisionId", "VariantKey", "Name", "NormalizedName", "Status", "SortOrder")
                SELECT "Id", "BaseIngredientId", 'legacy_' || REPLACE("Id", '-', ''),
                       "Name", "NormalizedName", 0, 0
                FROM "IngredientVariants";

                UPDATE "IngredientIdentities"
                SET "CurrentPublishedRevisionId" = "Id";
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_IngredientIdentities_IngredientRevisions_CurrentPublishedRevisionId",
                table: "IngredientIdentities",
                column: "CurrentPublishedRevisionId",
                principalTable: "IngredientRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IngredientIdentities_IngredientRevisions_SourceRevisionId",
                table: "IngredientIdentities",
                column: "SourceRevisionId",
                principalTable: "IngredientRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IngredientIdentities_IngredientRevisions_CurrentPublishedRevisionId",
                table: "IngredientIdentities");

            migrationBuilder.DropForeignKey(
                name: "FK_IngredientIdentities_IngredientRevisions_SourceRevisionId",
                table: "IngredientIdentities");

            migrationBuilder.DropTable(
                name: "IngredientRevisionAllergens");

            migrationBuilder.DropTable(
                name: "IngredientRevisionIntolerances");

            migrationBuilder.DropTable(
                name: "IngredientRevisionOrigins");

            migrationBuilder.DropTable(
                name: "IngredientRevisionUnitConversions");

            migrationBuilder.DropTable(
                name: "IngredientVariantAllergenOverrides");

            migrationBuilder.DropTable(
                name: "IngredientVariantIntoleranceOverrides");

            migrationBuilder.DropTable(
                name: "IngredientVariantOriginOverrides");

            migrationBuilder.DropTable(
                name: "IngredientVariantUnitConversionOverrides");

            migrationBuilder.DropTable(
                name: "IngredientAllergenDefinitions");

            migrationBuilder.DropTable(
                name: "IngredientIntoleranceDefinitions");

            migrationBuilder.DropTable(
                name: "IngredientOriginProperties");

            migrationBuilder.DropTable(
                name: "IngredientVariantRevisions");

            migrationBuilder.DropTable(
                name: "IngredientRevisions");

            migrationBuilder.DropTable(
                name: "IngredientCategories");

            migrationBuilder.DropTable(
                name: "IngredientIdentities");
        }
    }
}
