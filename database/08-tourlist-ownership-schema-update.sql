BEGIN;

ALTER TABLE "TourList" ADD COLUMN IF NOT EXISTS "TourName" varchar(150);
ALTER TABLE "TourList" ADD COLUMN IF NOT EXISTS "OwnerUserId" integer;
ALTER TABLE "TourList" ADD COLUMN IF NOT EXISTS "IsPublic" boolean NOT NULL DEFAULT false;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_TourList_Users_OwnerUserId'
    ) THEN
        ALTER TABLE "TourList"
        ADD CONSTRAINT "FK_TourList_Users_OwnerUserId"
        FOREIGN KEY ("OwnerUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_TourList_IsPublic"
    ON "TourList" ("IsPublic");

CREATE INDEX IF NOT EXISTS "IX_TourList_OwnerUserId_IsPublic"
    ON "TourList" ("OwnerUserId", "IsPublic");

UPDATE "TourList"
SET "IsPublic" = true
WHERE "IsPublic" = false
  AND "TourCode" LIKE 'PUB\_%' ESCAPE '\';

UPDATE "TourList" t
SET "OwnerUserId" = parsed."OwnerId"
FROM (
    SELECT
        "Id",
        split_part("TourCode", '_', 2)::integer AS "OwnerId"
    FROM "TourList"
    WHERE "OwnerUserId" IS NULL
      AND "TourCode" LIKE 'USR\_%' ESCAPE '\'
      AND split_part("TourCode", '_', 2) ~ '^[0-9]+$'
) parsed
WHERE t."Id" = parsed."Id";

UPDATE "TourList" t
SET "TourName" = parsed."NamePart"
FROM (
    SELECT
        "Id",
        NULLIF(REGEXP_REPLACE(split_part("TourCode", '_', 3), '[^A-Z0-9_]', '', 'g'), '') AS "NamePart"
    FROM "TourList"
    WHERE "TourName" IS NULL
      AND "TourCode" LIKE 'USR\_%' ESCAPE '\'
) parsed
WHERE t."Id" = parsed."Id"
  AND parsed."NamePart" IS NOT NULL;

UPDATE "TourList"
SET "TourName" = REGEXP_REPLACE(split_part("TourCode", '_', 2), '[^A-Z0-9_]', '', 'g')
WHERE "TourName" IS NULL
  AND "TourCode" LIKE 'PUB\_%' ESCAPE '\';

COMMIT;
