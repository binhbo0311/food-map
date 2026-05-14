-- Phase 2 schema migration for PostgreSQL
-- Target: existing schema from postgresql-init.sql (POI Id is integer)
-- Result: schema compatible with AppDbContext Phase 2 (POI Id is varchar business key)

BEGIN;

-- 1) Users: role + owner metadata
ALTER TABLE "Users"
    ADD COLUMN IF NOT EXISTS "Role" varchar(20),
    ADD COLUMN IF NOT EXISTS "OwnerIdentificationCode" varchar(30),
    ADD COLUMN IF NOT EXISTS "OwnerApprovedUtc" timestamptz;

UPDATE "Users"
SET "Role" = 'user'
WHERE "Role" IS NULL;

ALTER TABLE "Users"
    ALTER COLUMN "Role" SET NOT NULL,
    ALTER COLUMN "Role" SET DEFAULT 'user';

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_OwnerIdentificationCode"
    ON "Users" ("OwnerIdentificationCode")
    WHERE "OwnerIdentificationCode" IS NOT NULL;

-- 2) POIs: add Phase 2 columns first
ALTER TABLE "POIs"
    ADD COLUMN IF NOT EXISTS "Type" varchar(20),
    ADD COLUMN IF NOT EXISTS "ApprovalStatus" varchar(20),
    ADD COLUMN IF NOT EXISTS "SubmittedUtc" timestamptz,
    ADD COLUMN IF NOT EXISTS "ReviewedUtc" timestamptz,
    ADD COLUMN IF NOT EXISTS "OwnerId" integer,
    ADD COLUMN IF NOT EXISTS "ReviewedByAdminUserId" integer,
    ADD COLUMN IF NOT EXISTS "ListenCount" integer;

UPDATE "POIs"
SET
    "Type" = COALESCE("Type", 'visit'),
    "ApprovalStatus" = COALESCE("ApprovalStatus", 'approved'),
    "SubmittedUtc" = COALESCE("SubmittedUtc", NOW());

ALTER TABLE "POIs"
    ALTER COLUMN "Type" SET NOT NULL,
    ALTER COLUMN "ApprovalStatus" SET NOT NULL,
    ALTER COLUMN "SubmittedUtc" SET NOT NULL,
    ALTER COLUMN "Type" SET DEFAULT 'visit',
    ALTER COLUMN "ApprovalStatus" SET DEFAULT 'pending',
    ALTER COLUMN "SubmittedUtc" SET DEFAULT NOW(),
    ALTER COLUMN "ListenCount" SET DEFAULT 0;

UPDATE "POIs"
SET "ListenCount" = COALESCE("ListenCount", 0);

ALTER TABLE "POIs"
    ALTER COLUMN "ListenCount" SET NOT NULL;

-- 3) Convert POI id from integer to varchar business key and migrate all FK columns.
DO $$
DECLARE
    poi_id_type text;
BEGIN
    SELECT c.data_type
    INTO poi_id_type
    FROM information_schema.columns c
    WHERE c.table_schema = 'public'
      AND c.table_name = 'POIs'
      AND c.column_name = 'Id';

    IF poi_id_type = 'integer' THEN
        CREATE TEMP TABLE _poi_id_map AS
        SELECT
            "Id"::integer AS old_id,
            'VS-' || LPAD(ROW_NUMBER() OVER (ORDER BY "Id")::text, 3, '0') AS new_id
        FROM "POIs";

        ALTER TABLE "POITranslations" DROP CONSTRAINT IF EXISTS "FK_POITranslations_POIs_PoiId";
        ALTER TABLE "UserFavorites" DROP CONSTRAINT IF EXISTS "FK_UserFavorites_POIs_PoiId";
        ALTER TABLE "UserTours" DROP CONSTRAINT IF EXISTS "FK_UserTours_POIs_PoiId";

        ALTER TABLE "POITranslations" DROP CONSTRAINT IF EXISTS "UX_POITranslations_PoiId_LanguageId";
        ALTER TABLE "UserFavorites" DROP CONSTRAINT IF EXISTS "UX_UserFavorites_UserId_PoiId";

        ALTER TABLE "POIs" ALTER COLUMN "Id" DROP IDENTITY IF EXISTS;
        ALTER TABLE "POIs" ALTER COLUMN "Id" TYPE varchar(20) USING "Id"::text;

        UPDATE "POIs" p
        SET "Id" = m.new_id
        FROM _poi_id_map m
        WHERE p."Id" = m.old_id::text;

        ALTER TABLE "POITranslations" ALTER COLUMN "PoiId" TYPE varchar(20) USING "PoiId"::text;
        UPDATE "POITranslations" t
        SET "PoiId" = m.new_id
        FROM _poi_id_map m
        WHERE t."PoiId" = m.old_id::text;

        ALTER TABLE "UserFavorites" ALTER COLUMN "PoiId" TYPE varchar(20) USING "PoiId"::text;
        UPDATE "UserFavorites" f
        SET "PoiId" = m.new_id
        FROM _poi_id_map m
        WHERE f."PoiId" = m.old_id::text;

        ALTER TABLE "UserTours" ALTER COLUMN "PoiId" TYPE varchar(20) USING "PoiId"::text;
        UPDATE "UserTours" t
        SET "PoiId" = m.new_id
        FROM _poi_id_map m
        WHERE t."PoiId" = m.old_id::text;

        ALTER TABLE "POITranslations"
            ADD CONSTRAINT "FK_POITranslations_POIs_PoiId"
            FOREIGN KEY ("PoiId") REFERENCES "POIs" ("Id") ON DELETE CASCADE;

        ALTER TABLE "UserFavorites"
            ADD CONSTRAINT "FK_UserFavorites_POIs_PoiId"
            FOREIGN KEY ("PoiId") REFERENCES "POIs" ("Id") ON DELETE CASCADE;

        ALTER TABLE "UserTours"
            ADD CONSTRAINT "FK_UserTours_POIs_PoiId"
            FOREIGN KEY ("PoiId") REFERENCES "POIs" ("Id") ON DELETE CASCADE;

        ALTER TABLE "POITranslations"
            ADD CONSTRAINT "UX_POITranslations_PoiId_LanguageId"
            UNIQUE ("PoiId", "LanguageId");

        ALTER TABLE "UserFavorites"
            ADD CONSTRAINT "UX_UserFavorites_UserId_PoiId"
            UNIQUE ("UserId", "PoiId");
    END IF;
END $$;

ALTER TABLE "POIs"
    ALTER COLUMN "Id" TYPE varchar(20);

ALTER TABLE "POITranslations"
    ALTER COLUMN "PoiId" TYPE varchar(20);

ALTER TABLE "UserFavorites"
    ALTER COLUMN "PoiId" TYPE varchar(20);

ALTER TABLE "UserTours"
    ALTER COLUMN "PoiId" TYPE varchar(20);

-- 3.1) POITranslations: rich content HTML cho popup chi tiet.
ALTER TABLE "POITranslations"
    ADD COLUMN IF NOT EXISTS "RichContentHtml" text;

UPDATE "POITranslations"
SET "RichContentHtml" = ''
WHERE "RichContentHtml" IS NULL;

ALTER TABLE "POITranslations"
    ALTER COLUMN "RichContentHtml" SET DEFAULT '';

ALTER TABLE "POITranslations"
    ALTER COLUMN "RichContentHtml" SET NOT NULL;

-- 4) POI owner/reviewer foreign keys
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_POIs_Users_OwnerId'
    ) THEN
        ALTER TABLE "POIs"
            ADD CONSTRAINT "FK_POIs_Users_OwnerId"
            FOREIGN KEY ("OwnerId") REFERENCES "Users" ("Id") ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_POIs_Users_ReviewedByAdminUserId'
    ) THEN
        ALTER TABLE "POIs"
            ADD CONSTRAINT "FK_POIs_Users_ReviewedByAdminUserId"
            FOREIGN KEY ("ReviewedByAdminUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_POIs_Type_ApprovalStatus"
    ON "POIs" ("Type", "ApprovalStatus");

CREATE INDEX IF NOT EXISTS "IX_POIs_OwnerId"
    ON "POIs" ("OwnerId");

-- 5) Owner registration request table
CREATE TABLE IF NOT EXISTS "OwnerRegistrationRequests" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "UserId" integer NOT NULL,
    "BusinessName" varchar(200) NOT NULL,
    "BusinessAddress" varchar(300) NOT NULL,
    "ContactPhone" varchar(30) NOT NULL,
    "Notes" varchar(1000) NULL,
    "Status" varchar(20) NOT NULL,
    "RequestedUtc" timestamptz NOT NULL,
    "ReviewedUtc" timestamptz NULL,
    "ReviewedByAdminUserId" integer NULL,
    "RejectionReason" varchar(500) NULL,
    "ApprovedOwnerCode" varchar(30) NULL,
    CONSTRAINT "FK_OwnerRegistrationRequests_Users_UserId"
        FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_OwnerRegistrationRequests_Users_ReviewedByAdminUserId"
        FOREIGN KEY ("ReviewedByAdminUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS "IX_OwnerRegistrationRequests_UserId_Status_RequestedUtc"
    ON "OwnerRegistrationRequests" ("UserId", "Status", "RequestedUtc");

-- 6) Language ownership requests for owner-managed base languages
CREATE TABLE IF NOT EXISTS "LanguageOwnershipRequests" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "OwnerUserId" integer NOT NULL,
    "LanguageId" integer NOT NULL,
    "Status" varchar(20) NOT NULL DEFAULT 'pending',
    "RequestedUtc" timestamptz NOT NULL DEFAULT NOW(),
    "ReviewedUtc" timestamptz NULL,
    "ReviewedByAdminUserId" integer NULL,
    "RejectionReason" varchar(500) NULL,
    CONSTRAINT "FK_LanguageOwnershipRequests_Users_OwnerUserId"
        FOREIGN KEY ("OwnerUserId") REFERENCES "Users" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_LanguageOwnershipRequests_Languages_LanguageId"
        FOREIGN KEY ("LanguageId") REFERENCES "Languages" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_LanguageOwnershipRequests_Users_ReviewedByAdminUserId"
        FOREIGN KEY ("ReviewedByAdminUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS "IX_LanguageOwnershipRequests_OwnerUserId_LanguageId_Status_RequestedUtc"
    ON "LanguageOwnershipRequests" ("OwnerUserId", "LanguageId", "Status", "RequestedUtc");

-- 7) FoodItems table for POI type = food
CREATE TABLE IF NOT EXISTS "FoodItems" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "PoiId" varchar(20) NOT NULL,
    "OwnerId" integer NULL,
    "Name" varchar(200) NOT NULL,
    "Description" varchar(1000) NULL,
    "Price" numeric(12,2) NOT NULL DEFAULT 0,
    "Currency" varchar(10) NOT NULL DEFAULT 'VND',
    "IsAvailable" boolean NOT NULL DEFAULT true,
    "DisplayOrder" integer NOT NULL DEFAULT 0,
    "CreatedUtc" timestamptz NOT NULL,
    "UpdatedUtc" timestamptz NOT NULL,
    CONSTRAINT "FK_FoodItems_POIs_PoiId"
        FOREIGN KEY ("PoiId") REFERENCES "POIs" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_FoodItems_Users_OwnerId"
        FOREIGN KEY ("OwnerId") REFERENCES "Users" ("Id") ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS "IX_FoodItems_PoiId_DisplayOrder"
    ON "FoodItems" ("PoiId", "DisplayOrder");

COMMIT;

-- 8) Optional test data for Phase 2 flows
-- This block is idempotent and safe to run many times.
BEGIN;

INSERT INTO "Languages" ("LanguageCode", "LanguageName")
VALUES
    ('vi', 'Tiếng Việt'),
    ('en', 'English')
ON CONFLICT ("LanguageCode") DO UPDATE
SET "LanguageName" = EXCLUDED."LanguageName";

-- Password hash below is SHA256('123456') in uppercase hex.
INSERT INTO "Users" (
    "UserName", "PasswordHash", "DisplayName", "Role", "OwnerIdentificationCode", "OwnerApprovedUtc", "CreatedUtc")
VALUES (
    'phase2_admin',
    '8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92',
    'Phase2 Admin',
    'admin',
    NULL,
    NULL,
    NOW())
ON CONFLICT ("UserName") DO UPDATE
SET
    "DisplayName" = EXCLUDED."DisplayName",
    "Role" = 'admin';

INSERT INTO "Users" (
    "UserName", "PasswordHash", "DisplayName", "Role", "OwnerIdentificationCode", "OwnerApprovedUtc", "CreatedUtc")
VALUES (
    'phase2_owner',
    '8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92',
    'Phase2 Owner',
    'owner',
    'OWN-9001',
    NOW(),
    NOW())
ON CONFLICT ("UserName") DO UPDATE
SET
    "DisplayName" = EXCLUDED."DisplayName",
    "Role" = 'owner',
    "OwnerIdentificationCode" = 'OWN-9001',
    "OwnerApprovedUtc" = COALESCE("Users"."OwnerApprovedUtc", NOW());

INSERT INTO "Users" (
    "UserName", "PasswordHash", "DisplayName", "Role", "OwnerIdentificationCode", "OwnerApprovedUtc", "CreatedUtc")
VALUES (
    'phase2_user',
    '8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92',
    'Phase2 User',
    'user',
    NULL,
    NULL,
    NOW())
ON CONFLICT ("UserName") DO UPDATE
SET
    "DisplayName" = EXCLUDED."DisplayName",
    "Role" = 'user';

INSERT INTO "OwnerRegistrationRequests" (
    "UserId", "BusinessName", "BusinessAddress", "ContactPhone", "Notes", "Status", "RequestedUtc")
SELECT
    u."Id",
    'Phase2 Bistro',
    '123 Test Street, District 1',
    '0909000001',
    'Please approve owner account for test flow.',
    'pending',
    NOW()
FROM "Users" u
WHERE u."UserName" = 'phase2_user'
  AND NOT EXISTS (
      SELECT 1
      FROM "OwnerRegistrationRequests" r
      WHERE r."UserId" = u."Id" AND r."Status" = 'pending'
  );

INSERT INTO "POIs" (
    "Id", "Type", "Latitude", "Longitude", "ActivationRadius", "Priority",
    "ApprovalStatus", "SubmittedUtc", "ReviewedUtc", "OwnerId", "ReviewedByAdminUserId", "QRCodeId")
SELECT
    'FD-901',
    'food',
    10.776500,
    106.700200,
    120,
    901,
    'approved',
    NOW(),
    NOW(),
    o."Id",
    a."Id",
    'QR-FD-901'
FROM "Users" o
JOIN "Users" a ON a."UserName" = 'phase2_admin'
WHERE o."UserName" = 'phase2_owner'
  AND NOT EXISTS (SELECT 1 FROM "POIs" p WHERE p."Id" = 'FD-901');

INSERT INTO "POIs" (
    "Id", "Type", "Latitude", "Longitude", "ActivationRadius", "Priority",
    "ApprovalStatus", "SubmittedUtc", "ReviewedUtc", "OwnerId", "ReviewedByAdminUserId", "QRCodeId")
SELECT
    'VS-902',
    'visit',
    10.777700,
    106.699800,
    140,
    902,
    'pending',
    NOW(),
    NULL,
    o."Id",
    NULL,
    'QR-VS-902'
FROM "Users" o
WHERE o."UserName" = 'phase2_owner'
  AND NOT EXISTS (SELECT 1 FROM "POIs" p WHERE p."Id" = 'VS-902');

INSERT INTO "POITranslations" (
    "PoiId", "LanguageId", "LocationName", "Description", "ImageUrl", "AudioFileUrl", "TtsScript", "RichContentHtml")
SELECT
    'FD-901', l."Id",
    CASE WHEN l."LanguageCode" = 'vi' THEN 'Bếp Nhà Thử Nghiệm' ELSE 'Test Kitchen House' END,
    CASE WHEN l."LanguageCode" = 'vi'
         THEN 'Điểm ăn uống dùng để test chức năng menu món ăn.'
         ELSE 'Food POI used to test the menu flow.' END,
    'https://picsum.photos/seed/fd901/' || l."LanguageCode" || '/1200/800',
    'audio/' || l."LanguageCode" || '/fd-901.mp3',
    CASE WHEN l."LanguageCode" = 'vi'
         THEN 'Bạn đang ở điểm ăn uống thử nghiệm.'
            ELSE 'You are near the test food location.' END,
        CASE WHEN l."LanguageCode" = 'vi'
            THEN '<h3>Món nổi bật</h3><ul><li>Phở bò đặc biệt</li><li>Chả giò hải sản</li></ul><p>Không gian phù hợp nhóm 2-6 người.</p>'
            ELSE '<h3>Featured dishes</h3><ul><li>Special beef pho</li><li>Seafood spring rolls</li></ul><p>Best for groups of 2-6 guests.</p>' END
FROM "Languages" l
WHERE l."LanguageCode" IN ('vi', 'en')
ON CONFLICT ("PoiId", "LanguageId") DO UPDATE
SET
    "LocationName" = EXCLUDED."LocationName",
    "Description" = EXCLUDED."Description",
    "ImageUrl" = EXCLUDED."ImageUrl",
    "AudioFileUrl" = EXCLUDED."AudioFileUrl",
    "TtsScript" = EXCLUDED."TtsScript",
    "RichContentHtml" = EXCLUDED."RichContentHtml";

INSERT INTO "POITranslations" (
    "PoiId", "LanguageId", "LocationName", "Description", "ImageUrl", "AudioFileUrl", "TtsScript", "RichContentHtml")
SELECT
    'VS-902', l."Id",
    CASE WHEN l."LanguageCode" = 'vi' THEN 'Điểm Tham Quan Chờ Duyệt' ELSE 'Pending Visit Spot' END,
    CASE WHEN l."LanguageCode" = 'vi'
         THEN 'POI tham quan đang ở trạng thái chờ admin duyệt.'
         ELSE 'Visit POI that is waiting for admin approval.' END,
    'https://picsum.photos/seed/vs902/' || l."LanguageCode" || '/1200/800',
    'audio/' || l."LanguageCode" || '/vs-902.mp3',
    CASE WHEN l."LanguageCode" = 'vi'
         THEN 'POI này hiện đang chờ phê duyệt.'
            ELSE 'This POI is currently pending approval.' END,
        CASE WHEN l."LanguageCode" = 'vi'
            THEN '<h3>Thông tin duyệt</h3><p>POI này đang chờ quản trị viên xét duyệt trước khi hiển thị công khai.</p>'
            ELSE '<h3>Approval status</h3><p>This POI is waiting for admin review before it can be public.</p>' END
FROM "Languages" l
WHERE l."LanguageCode" IN ('vi', 'en')
ON CONFLICT ("PoiId", "LanguageId") DO UPDATE
SET
    "LocationName" = EXCLUDED."LocationName",
    "Description" = EXCLUDED."Description",
    "ImageUrl" = EXCLUDED."ImageUrl",
    "AudioFileUrl" = EXCLUDED."AudioFileUrl",
    "TtsScript" = EXCLUDED."TtsScript",
    "RichContentHtml" = EXCLUDED."RichContentHtml";

INSERT INTO "FoodItems" (
    "PoiId", "OwnerId", "Name", "Description", "Price", "Currency", "IsAvailable", "DisplayOrder", "CreatedUtc", "UpdatedUtc")
SELECT
    'FD-901',
    o."Id",
    'Phở Bò Đặc Biệt',
    'Món chính để test hiển thị menu food item.',
    79000,
    'VND',
    true,
    1,
    NOW(),
    NOW()
FROM "Users" o
WHERE o."UserName" = 'phase2_owner'
  AND NOT EXISTS (
      SELECT 1 FROM "FoodItems" f
      WHERE f."PoiId" = 'FD-901' AND f."Name" = 'Phở Bò Đặc Biệt'
  );

INSERT INTO "FoodItems" (
    "PoiId", "OwnerId", "Name", "Description", "Price", "Currency", "IsAvailable", "DisplayOrder", "CreatedUtc", "UpdatedUtc")
SELECT
    'FD-901',
    o."Id",
    'Bánh Mì Gà Nướng',
    'Món phụ để test sắp xếp DisplayOrder.',
    39000,
    'VND',
    true,
    2,
    NOW(),
    NOW()
FROM "Users" o
WHERE o."UserName" = 'phase2_owner'
  AND NOT EXISTS (
      SELECT 1 FROM "FoodItems" f
      WHERE f."PoiId" = 'FD-901' AND f."Name" = 'Bánh Mì Gà Nướng'
  );

INSERT INTO "UserFavorites" ("UserId", "PoiId", "CreatedUtc")
SELECT u."Id", 'FD-901', NOW()
FROM "Users" u
WHERE u."UserName" = 'phase2_user'
ON CONFLICT ("UserId", "PoiId") DO NOTHING;

INSERT INTO "UserTours" ("UserId", "PoiId", "LanguageCode", "TriggerType", "VisitedUtc")
SELECT u."Id", 'FD-901', 'vi', 'ManualTap', NOW() - INTERVAL '2 day'
FROM "Users" u
WHERE u."UserName" = 'phase2_user'
  AND NOT EXISTS (
      SELECT 1 FROM "UserTours" t
      WHERE t."UserId" = u."Id"
        AND t."PoiId" = 'FD-901'
        AND t."LanguageCode" = 'vi'
        AND t."TriggerType" = 'ManualTap'
  );

COMMIT;

-- Usage:
-- psql -h <host> -p <port> -U <user> -d food_map -f 04-phase2-schema-update.sql
