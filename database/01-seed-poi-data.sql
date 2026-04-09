-- Seed base POI and translation data for FOOD_MAP (PostgreSQL)
-- Safe to run multiple times.

BEGIN;

INSERT INTO "Languages" ("LanguageCode", "LanguageName")
VALUES
    ('vi', 'Tiếng Việt'),
    ('en', 'English')
ON CONFLICT ("LanguageCode") DO UPDATE
SET "LanguageName" = EXCLUDED."LanguageName";

INSERT INTO "POIs" ("Latitude", "Longitude", "ActivationRadius", "Priority", "QRCodeId")
SELECT 10.776889, 106.700806, 120, 1, 'QR-ND-001'
WHERE NOT EXISTS (
    SELECT 1 FROM "POIs" WHERE "QRCodeId" = 'QR-ND-001'
);

INSERT INTO "POIs" ("Latitude", "Longitude", "ActivationRadius", "Priority", "QRCodeId")
SELECT 10.780245, 106.699020, 150, 2, 'QR-BT-002'
WHERE NOT EXISTS (
    SELECT 1 FROM "POIs" WHERE "QRCodeId" = 'QR-BT-002'
);

INSERT INTO "POIs" ("Latitude", "Longitude", "ActivationRadius", "Priority", "QRCodeId")
SELECT 10.773500, 106.704200, 100, 3, 'QR-CH-003'
WHERE NOT EXISTS (
    SELECT 1 FROM "POIs" WHERE "QRCodeId" = 'QR-CH-003'
);

INSERT INTO "POITranslations" (
    "PoiId",
    "LanguageId",
    "LocationName",
    "Description",
    "ImageUrl",
    "AudioFileUrl",
    "TtsScript")
SELECT
    poi."Id",
    lang."Id",
    'Nhà hát Thành phố',
    'Công trình kiến trúc biểu tượng ở trung tâm Sài Gòn.',
    'https://th.bing.com/th/id/OIP.H_KC5HNvLKDWiFFp7Ad7vgHaE8?w=192&h=180&c=7&r=0&o=7&pid=1.7&rm=3',
    'audio/vi/nha-hat-thanh-pho.mp3',
    'Bạn đang ở gần Nhà hát Thành phố. Đây là điểm đến nổi bật của khu trung tâm.'
FROM "POIs" poi
JOIN "Languages" lang ON lang."LanguageCode" = 'vi'
WHERE poi."QRCodeId" = 'QR-ND-001'
ON CONFLICT ("PoiId", "LanguageId") DO UPDATE
SET
    "LocationName" = EXCLUDED."LocationName",
    "Description" = EXCLUDED."Description",
    "ImageUrl" = EXCLUDED."ImageUrl",
    "AudioFileUrl" = EXCLUDED."AudioFileUrl",
    "TtsScript" = EXCLUDED."TtsScript";

INSERT INTO "POITranslations" (
    "PoiId",
    "LanguageId",
    "LocationName",
    "Description",
    "ImageUrl",
    "AudioFileUrl",
    "TtsScript")
SELECT
    poi."Id",
    lang."Id",
    'Bảo tàng Thành phố',
    'Bảo tàng lưu giữ nhiều hiện vật lịch sử và văn hóa.',
    'https://th.bing.com/th/id/OIP.OAmigQ1rgglHi4B91asl9gHaDE?w=263&h=145&c=7&r=0&o=7&pid=1.7&rm=3',
    'audio/vi/bao-tang-thanh-pho.mp3',
    'Bạn đã đến gần Bảo tàng Thành phố. Hãy khám phá những câu chuyện lịch sử tại đây.'
FROM "POIs" poi
JOIN "Languages" lang ON lang."LanguageCode" = 'vi'
WHERE poi."QRCodeId" = 'QR-BT-002'
ON CONFLICT ("PoiId", "LanguageId") DO UPDATE
SET
    "LocationName" = EXCLUDED."LocationName",
    "Description" = EXCLUDED."Description",
    "ImageUrl" = EXCLUDED."ImageUrl",
    "AudioFileUrl" = EXCLUDED."AudioFileUrl",
    "TtsScript" = EXCLUDED."TtsScript";

INSERT INTO "POITranslations" (
    "PoiId",
    "LanguageId",
    "LocationName",
    "Description",
    "ImageUrl",
    "AudioFileUrl",
    "TtsScript")
SELECT
    poi."Id",
    lang."Id",
    'Chợ Bến Thành',
    'Khu chợ nổi tiếng với ẩm thực và đặc sản địa phương.',
    'https://th.bing.com/th/id/OIP.8w4MoIrluD5V3ISNoqK7EwHaFj?w=223&h=180&c=7&r=0&o=7&pid=1.7&rm=3',
    'audio/vi/cho-ben-thanh.mp3',
    'Bạn đang ở gần Chợ Bến Thành. Đây là nơi lý tưởng để trải nghiệm ẩm thực Sài Gòn.'
FROM "POIs" poi
JOIN "Languages" lang ON lang."LanguageCode" = 'vi'
WHERE poi."QRCodeId" = 'QR-CH-003'
ON CONFLICT ("PoiId", "LanguageId") DO UPDATE
SET
    "LocationName" = EXCLUDED."LocationName",
    "Description" = EXCLUDED."Description",
    "ImageUrl" = EXCLUDED."ImageUrl",
    "AudioFileUrl" = EXCLUDED."AudioFileUrl",
    "TtsScript" = EXCLUDED."TtsScript";

INSERT INTO "POITranslations" (
    "PoiId",
    "LanguageId",
    "LocationName",
    "Description",
    "ImageUrl",
    "AudioFileUrl",
    "TtsScript")
SELECT
    poi."Id",
    lang."Id",
    'City Opera House',
    'A landmark architecture in the heart of Ho Chi Minh City.',
       'https://th.bing.com/th/id/OIP.H_KC5HNvLKDWiFFp7Ad7vgHaE8?w=192&h=180&c=7&r=0&o=7&pid=1.7&rm=3',
    'audio/en/city-opera-house.mp3',
    'You are near the City Opera House, one of the most iconic spots in downtown Ho Chi Minh City.'
FROM "POIs" poi
JOIN "Languages" lang ON lang."LanguageCode" = 'en'
WHERE poi."QRCodeId" = 'QR-ND-001'
ON CONFLICT ("PoiId", "LanguageId") DO UPDATE
SET
    "LocationName" = EXCLUDED."LocationName",
    "Description" = EXCLUDED."Description",
    "ImageUrl" = EXCLUDED."ImageUrl",
    "AudioFileUrl" = EXCLUDED."AudioFileUrl",
    "TtsScript" = EXCLUDED."TtsScript";

INSERT INTO "POITranslations" (
    "PoiId",
    "LanguageId",
    "LocationName",
    "Description",
    "ImageUrl",
    "AudioFileUrl",
    "TtsScript")
SELECT
    poi."Id",
    lang."Id",
    'City Museum',
    'A museum preserving historical and cultural collections.',
         'https://th.bing.com/th/id/OIP.OAmigQ1rgglHi4B91asl9gHaDE?w=263&h=145&c=7&r=0&o=7&pid=1.7&rm=3',
    'audio/en/city-museum.mp3',
    'You are close to the City Museum. Explore the historical stories preserved here.'
FROM "POIs" poi
JOIN "Languages" lang ON lang."LanguageCode" = 'en'
WHERE poi."QRCodeId" = 'QR-BT-002'
ON CONFLICT ("PoiId", "LanguageId") DO UPDATE
SET
    "LocationName" = EXCLUDED."LocationName",
    "Description" = EXCLUDED."Description",
    "ImageUrl" = EXCLUDED."ImageUrl",
    "AudioFileUrl" = EXCLUDED."AudioFileUrl",
    "TtsScript" = EXCLUDED."TtsScript";

INSERT INTO "POITranslations" (
    "PoiId",
    "LanguageId",
    "LocationName",
    "Description",
    "ImageUrl",
    "AudioFileUrl",
    "TtsScript")
SELECT
    poi."Id",
    lang."Id",
    'Ben Thanh Market',
    'A famous market known for local food and souvenirs.',
        'https://th.bing.com/th/id/OIP.8w4MoIrluD5V3ISNoqK7EwHaFj?w=223&h=180&c=7&r=0&o=7&pid=1.7&rm=3',
    'audio/en/ben-thanh-market.mp3',
    'You are near Ben Thanh Market, a great place to enjoy local food and shopping.'
FROM "POIs" poi
JOIN "Languages" lang ON lang."LanguageCode" = 'en'
WHERE poi."QRCodeId" = 'QR-CH-003'
ON CONFLICT ("PoiId", "LanguageId") DO UPDATE
SET
    "LocationName" = EXCLUDED."LocationName",
    "Description" = EXCLUDED."Description",
    "ImageUrl" = EXCLUDED."ImageUrl",
    "AudioFileUrl" = EXCLUDED."AudioFileUrl",
    "TtsScript" = EXCLUDED."TtsScript";

COMMIT;
