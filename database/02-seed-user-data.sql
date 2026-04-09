-- Seed demo user and user activity data for FOOD_MAP (PostgreSQL)
-- Safe to run multiple times.

BEGIN;

-- Demo account: demo / 123456
INSERT INTO "Users" ("UserName", "PasswordHash", "DisplayName", "CreatedUtc")
VALUES ('demo', '8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92', 'Demo User', NOW())
ON CONFLICT ("UserName") DO UPDATE
SET "DisplayName" = EXCLUDED."DisplayName";

INSERT INTO "UserFavorites" ("UserId", "PoiId", "CreatedUtc")
SELECT u."Id", p."Id", NOW()
FROM "Users" u
JOIN "POIs" p ON p."QRCodeId" = 'QR-ND-001'
WHERE u."UserName" = 'demo'
ON CONFLICT ("UserId", "PoiId") DO NOTHING;

INSERT INTO "UserFavorites" ("UserId", "PoiId", "CreatedUtc")
SELECT u."Id", p."Id", NOW()
FROM "Users" u
JOIN "POIs" p ON p."QRCodeId" = 'QR-CH-003'
WHERE u."UserName" = 'demo'
ON CONFLICT ("UserId", "PoiId") DO NOTHING;

INSERT INTO "UserTours" ("UserId", "PoiId", "LanguageCode", "TriggerType", "VisitedUtc")
SELECT u."Id", p."Id", 'vi', 'ManualTap', TIMESTAMPTZ '2026-04-08 08:15:00+00'
FROM "Users" u
JOIN "POIs" p ON p."QRCodeId" = 'QR-ND-001'
WHERE u."UserName" = 'demo'
AND NOT EXISTS (
    SELECT 1
    FROM "UserTours" t
    WHERE t."UserId" = u."Id"
      AND t."PoiId" = p."Id"
      AND t."LanguageCode" = 'vi'
      AND t."TriggerType" = 'ManualTap'
      AND t."VisitedUtc" = TIMESTAMPTZ '2026-04-08 08:15:00+00'
);

INSERT INTO "UserTours" ("UserId", "PoiId", "LanguageCode", "TriggerType", "VisitedUtc")
SELECT u."Id", p."Id", 'en', 'AutoProximity', TIMESTAMPTZ '2026-04-08 09:30:00+00'
FROM "Users" u
JOIN "POIs" p ON p."QRCodeId" = 'QR-BT-002'
WHERE u."UserName" = 'demo'
AND NOT EXISTS (
    SELECT 1
    FROM "UserTours" t
    WHERE t."UserId" = u."Id"
      AND t."PoiId" = p."Id"
      AND t."LanguageCode" = 'en'
      AND t."TriggerType" = 'AutoProximity'
      AND t."VisitedUtc" = TIMESTAMPTZ '2026-04-08 09:30:00+00'
);

COMMIT;
