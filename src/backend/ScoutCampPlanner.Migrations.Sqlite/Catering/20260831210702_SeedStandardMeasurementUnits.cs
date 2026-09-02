using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class SeedStandardMeasurementUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "MeasurementUnits" ("Id", "BaseUnitFactor", "Dimension", "Name", "NormalizedName", "Symbol")
                SELECT '51111111-1111-1111-1111-000000000001', 1, 0, 'Gramm', 'GRAMM', 'g'
                WHERE NOT EXISTS (SELECT 1 FROM "MeasurementUnits" WHERE "NormalizedName" = 'GRAMM' OR ("Dimension" = 0 AND "Symbol" = 'g'));
                INSERT INTO "MeasurementUnits" ("Id", "Name", "NormalizedName", "Symbol", "Dimension", "BaseUnitFactor") SELECT '51111111-1111-1111-1111-000000000002', 'Kilogramm', 'KILOGRAMM', 'kg', 0, 1000
                WHERE NOT EXISTS (SELECT 1 FROM "MeasurementUnits" WHERE "NormalizedName" = 'KILOGRAMM' OR ("Dimension" = 0 AND "Symbol" = 'kg'));
                INSERT INTO "MeasurementUnits" ("Id", "Name", "NormalizedName", "Symbol", "Dimension", "BaseUnitFactor") SELECT '51111111-1111-1111-1111-000000000003', 'Milliliter', 'MILLILITER', 'ml', 1, 1
                WHERE NOT EXISTS (SELECT 1 FROM "MeasurementUnits" WHERE "NormalizedName" = 'MILLILITER' OR ("Dimension" = 1 AND "Symbol" = 'ml'));
                INSERT INTO "MeasurementUnits" ("Id", "Name", "NormalizedName", "Symbol", "Dimension", "BaseUnitFactor") SELECT '51111111-1111-1111-1111-000000000004', 'Liter', 'LITER', 'l', 1, 1000
                WHERE NOT EXISTS (SELECT 1 FROM "MeasurementUnits" WHERE "NormalizedName" = 'LITER' OR ("Dimension" = 1 AND "Symbol" = 'l'));
                INSERT INTO "MeasurementUnits" ("Id", "Name", "NormalizedName", "Symbol", "Dimension", "BaseUnitFactor") SELECT '51111111-1111-1111-1111-000000000005', 'Stück', 'STÜCK', 'Stk.', 2, 1
                WHERE NOT EXISTS (SELECT 1 FROM "MeasurementUnits" WHERE "NormalizedName" = 'STÜCK' OR ("Dimension" = 2 AND "Symbol" = 'Stk.'));
                INSERT INTO "MeasurementUnits" ("Id", "Name", "NormalizedName", "Symbol", "Dimension", "BaseUnitFactor") SELECT '51111111-1111-1111-1111-000000000006', 'Teelöffel', 'TEELÖFFEL', 'TL', 2, 1
                WHERE NOT EXISTS (SELECT 1 FROM "MeasurementUnits" WHERE "NormalizedName" = 'TEELÖFFEL' OR ("Dimension" = 2 AND "Symbol" = 'TL'));
                INSERT INTO "MeasurementUnits" ("Id", "Name", "NormalizedName", "Symbol", "Dimension", "BaseUnitFactor") SELECT '51111111-1111-1111-1111-000000000007', 'Esslöffel', 'ESSLÖFFEL', 'EL', 2, 1
                WHERE NOT EXISTS (SELECT 1 FROM "MeasurementUnits" WHERE "NormalizedName" = 'ESSLÖFFEL' OR ("Dimension" = 2 AND "Symbol" = 'EL'));
                INSERT INTO "MeasurementUnits" ("Id", "Name", "NormalizedName", "Symbol", "Dimension", "BaseUnitFactor") SELECT '51111111-1111-1111-1111-000000000008', 'Prise', 'PRISE', 'Prise', 2, 1
                WHERE NOT EXISTS (SELECT 1 FROM "MeasurementUnits" WHERE "NormalizedName" = 'PRISE' OR ("Dimension" = 2 AND "Symbol" = 'Prise'));
                INSERT INTO "MeasurementUnits" ("Id", "Name", "NormalizedName", "Symbol", "Dimension", "BaseUnitFactor") SELECT '51111111-1111-1111-1111-000000000009', 'Bund', 'BUND', 'Bund', 2, 1
                WHERE NOT EXISTS (SELECT 1 FROM "MeasurementUnits" WHERE "NormalizedName" = 'BUND' OR ("Dimension" = 2 AND "Symbol" = 'Bund'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "MeasurementUnits",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000001"));

            migrationBuilder.DeleteData(
                table: "MeasurementUnits",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000002"));

            migrationBuilder.DeleteData(
                table: "MeasurementUnits",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000003"));

            migrationBuilder.DeleteData(
                table: "MeasurementUnits",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000004"));

            migrationBuilder.DeleteData(
                table: "MeasurementUnits",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000005"));

            migrationBuilder.DeleteData(
                table: "MeasurementUnits",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000006"));

            migrationBuilder.DeleteData(
                table: "MeasurementUnits",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000007"));

            migrationBuilder.DeleteData(
                table: "MeasurementUnits",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000008"));

            migrationBuilder.DeleteData(
                table: "MeasurementUnits",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000009"));
        }
    }
}
