BEGIN;

DELETE FROM "TourList"
WHERE "TourCode" IN ('TEST_ALL', 'TEST_FOOD', 'TEST_VISIT');

WITH approved_pois AS (
    SELECT
        p."Id",
        p."Type",
        ROW_NUMBER() OVER (ORDER BY p."Priority", p."Id") AS rn
    FROM "POIs" p
    WHERE p."ApprovalStatus" = 'approved'
)
INSERT INTO "TourList" ("TourCode", "TourName", "OwnerUserId", "IsPublic", "PoiId", "SortOrder", "CreatedUtc")
SELECT 'TEST_ALL', 'TEST_ALL', NULL, true, ap."Id", ap.rn, NOW()
FROM approved_pois ap
WHERE ap.rn <= 12
ORDER BY ap.rn;

WITH approved_food AS (
    SELECT
        p."Id",
        ROW_NUMBER() OVER (ORDER BY p."Priority", p."Id") AS rn
    FROM "POIs" p
    WHERE p."ApprovalStatus" = 'approved'
        AND p."Type" = 'food'
)
INSERT INTO "TourList" ("TourCode", "TourName", "OwnerUserId", "IsPublic", "PoiId", "SortOrder", "CreatedUtc")
SELECT 'TEST_FOOD', 'TEST_FOOD', NULL, true, ap."Id", ap.rn, NOW()
FROM approved_food ap
WHERE ap.rn <= 8
ORDER BY ap.rn;

WITH approved_visit AS (
    SELECT
        p."Id",
        ROW_NUMBER() OVER (ORDER BY p."Priority", p."Id") AS rn
    FROM "POIs" p
    WHERE p."ApprovalStatus" = 'approved'
        AND p."Type" = 'visit'
)
INSERT INTO "TourList" ("TourCode", "TourName", "OwnerUserId", "IsPublic", "PoiId", "SortOrder", "CreatedUtc")
SELECT 'TEST_VISIT', 'TEST_VISIT', NULL, true, ap."Id", ap.rn, NOW()
FROM approved_visit ap
WHERE ap.rn <= 8
ORDER BY ap.rn;

COMMIT;