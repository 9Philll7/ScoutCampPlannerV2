-- One-off repair for the diagnosed empty desktop database, NOT a startup migration.
-- Requires an offline database/audit backup and explicit operator approval.
-- sqlite3 CLI: .bail ensures any failed guard rolls back when the connection closes.
.bail on
PRAGMA foreign_keys = ON;
BEGIN IMMEDIATE;
CREATE TEMP TABLE RepairGuard (Ok INTEGER NOT NULL CHECK (Ok = 1));
INSERT INTO RepairGuard SELECT CASE WHEN COUNT(*) = 0 THEN 1 ELSE 0 END FROM Camps;
INSERT INTO RepairGuard SELECT CASE WHEN COUNT(*) = 0 THEN 1 ELSE 0 END FROM CampParticipants;
DELETE FROM CampParticipantAbsentDays WHERE NOT EXISTS (SELECT 1 FROM CampParticipants p WHERE p.Id = ParticipantId);
DELETE FROM CampParticipantAbsentMeals WHERE NOT EXISTS (SELECT 1 FROM CampParticipants p WHERE p.Id = ParticipantId);
DELETE FROM CampParticipantAllergens WHERE NOT EXISTS (SELECT 1 FROM CampParticipants p WHERE p.Id = ParticipantId);
DELETE FROM CampParticipantIntolerances WHERE NOT EXISTS (SELECT 1 FROM CampParticipants p WHERE p.Id = ParticipantId);
DELETE FROM LocalCampPermissionGrants WHERE NOT EXISTS (
    SELECT 1 FROM LocalCampAccessGrants g WHERE g.DeviceIdentityId = LocalCampPermissionGrants.DeviceIdentityId
    AND g.CampId = LocalCampPermissionGrants.CampId);
DELETE FROM RecipeRevisions WHERE NOT EXISTS (SELECT 1 FROM Recipes r WHERE r.Id = RecipeId);
DELETE FROM IngredientRevisions WHERE NOT EXISTS (SELECT 1 FROM IngredientIdentities i WHERE i.Id = IngredientId);
INSERT INTO RepairGuard SELECT CASE WHEN COUNT(*) = 0 THEN 1 ELSE 0 END FROM pragma_foreign_key_check;
INSERT INTO RepairGuard SELECT CASE WHEN integrity_check = 'ok' THEN 1 ELSE 0 END FROM pragma_integrity_check;
COMMIT;
SELECT 'foreign_key_violations', COUNT(*) FROM pragma_foreign_key_check;
PRAGMA integrity_check;
