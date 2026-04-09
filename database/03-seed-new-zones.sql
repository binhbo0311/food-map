-- Seed thêm 3 khu mới và thông tin liên quan cho FOOD_MAP (PostgreSQL)
-- Safe to run multiple times.

BEGIN;

-- Đảm bảo có ngôn ngữ cơ bản trước khi thêm translation.
INSERT INTO "Languages" ("LanguageCode", "LanguageName")
VALUES
    ('vi', 'Tiếng Việt'),
    ('en', 'English')
ON CONFLICT ("LanguageCode") DO UPDATE
SET "LanguageName" = EXCLUDED."LanguageName";

-- 1) Phố đi bộ Nguyễn Huệ
INSERT INTO "POIs" ("Latitude", "Longitude", "ActivationRadius", "Priority", "QRCodeId")
SELECT 10.773150, 106.703634, 130, 4, 'QR-PDB-004'
WHERE NOT EXISTS (
    SELECT 1 FROM "POIs" WHERE "QRCodeId" = 'QR-PDB-004'
);

-- 2) Bưu điện Trung tâm Sài Gòn
INSERT INTO "POIs" ("Latitude", "Longitude", "ActivationRadius", "Priority", "QRCodeId")
SELECT 10.780017, 106.699203, 110, 5, 'QR-BD-005'
WHERE NOT EXISTS (
    SELECT 1 FROM "POIs" WHERE "QRCodeId" = 'QR-BD-005'
);

-- 3) Dinh Độc Lập
INSERT INTO "POIs" ("Latitude", "Longitude", "ActivationRadius", "Priority", "QRCodeId")
SELECT 10.777153, 106.695313, 140, 6, 'QR-DDL-006'
WHERE NOT EXISTS (
    SELECT 1 FROM "POIs" WHERE "QRCodeId" = 'QR-DDL-006'
);

-- VI translations
INSERT INTO "POITranslations" (
    "PoiId", "LanguageId", "LocationName", "Description", "ImageUrl", "AudioFileUrl", "TtsScript")
SELECT
    poi."Id",
    lang."Id",
    'Phố đi bộ Nguyễn Huệ',
    'Phố đi bộ Nguyễn Huệ được phát triển từ trục đại lộ trung tâm của Sài Gòn xưa và chính thức chỉnh trang thành không gian đi bộ từ năm 2015. Khu vực này được hình thành với mục đích tạo quảng trường công cộng, nơi người dân và du khách có thể tham gia lễ hội, hoạt động văn hóa đường phố, và kết nối với các công trình biểu tượng quanh trung tâm thành phố.',
    'https://picsum.photos/seed/pho_di_bo_nguyen_hue/1200/800',
    'audio/vi/pho-di-bo-nguyen-hue.mp3',
    'Bạn đang ở khu Phố đi bộ Nguyễn Huệ, một điểm hẹn sôi động của trung tâm thành phố.'
FROM "POIs" poi
JOIN "Languages" lang ON lang."LanguageCode" = 'vi'
WHERE poi."QRCodeId" = 'QR-PDB-004'
ON CONFLICT ("PoiId", "LanguageId") DO UPDATE
SET
    "LocationName" = EXCLUDED."LocationName",
    "Description" = EXCLUDED."Description",
    "ImageUrl" = EXCLUDED."ImageUrl",
    "AudioFileUrl" = EXCLUDED."AudioFileUrl",
    "TtsScript" = EXCLUDED."TtsScript";

INSERT INTO "POITranslations" (
    "PoiId", "LanguageId", "LocationName", "Description", "ImageUrl", "AudioFileUrl", "TtsScript")
SELECT
    poi."Id",
    lang."Id",
    'Bưu điện Trung tâm Sài Gòn',
    'Bưu điện Trung tâm Sài Gòn được xây dựng vào cuối thế kỷ 19 trong thời kỳ thuộc địa, là đầu mối liên lạc quan trọng kết nối Sài Gòn với các tỉnh và quốc tế. Công trình tồn tại với mục đích phục vụ giao tiếp bưu chính, đồng thời ngày nay còn là điểm tham quan kiến trúc và lịch sử, giúp du khách hiểu về quá trình đô thị hóa của thành phố.',
    'https://picsum.photos/seed/buu_dien_trung_tam_sai_gon/1200/800',
    'audio/vi/buu-dien-trung-tam-sai-gon.mp3',
    'Bạn đang ở gần Bưu điện Trung tâm Sài Gòn, công trình lịch sử nổi bật của thành phố.'
FROM "POIs" poi
JOIN "Languages" lang ON lang."LanguageCode" = 'vi'
WHERE poi."QRCodeId" = 'QR-BD-005'
ON CONFLICT ("PoiId", "LanguageId") DO UPDATE
SET
    "LocationName" = EXCLUDED."LocationName",
    "Description" = EXCLUDED."Description",
    "ImageUrl" = EXCLUDED."ImageUrl",
    "AudioFileUrl" = EXCLUDED."AudioFileUrl",
    "TtsScript" = EXCLUDED."TtsScript";

INSERT INTO "POITranslations" (
    "PoiId", "LanguageId", "LocationName", "Description", "ImageUrl", "AudioFileUrl", "TtsScript")
SELECT
    poi."Id",
    lang."Id",
    'Dinh Độc Lập',
    'Dinh Độc Lập, trước đây kế thừa vai trò từ khu dinh thự hành chính trung tâm, được xây dựng lại trong thập niên 1960 và trở thành nơi làm việc của chính quyền miền Nam Việt Nam trước năm 1975. Địa điểm này tồn tại như một chứng tích lịch sử quan trọng, giúp du khách tìm hiểu các bước ngoặt chính trị và ý nghĩa hòa bình, thống nhất của Việt Nam hiện đại.',
    'https://picsum.photos/seed/dinh_doc_lap/1200/800',
    'audio/vi/dinh-doc-lap.mp3',
    'Bạn đang ở gần Dinh Độc Lập, một địa điểm lịch sử quan trọng tại Thành phố Hồ Chí Minh.'
FROM "POIs" poi
JOIN "Languages" lang ON lang."LanguageCode" = 'vi'
WHERE poi."QRCodeId" = 'QR-DDL-006'
ON CONFLICT ("PoiId", "LanguageId") DO UPDATE
SET
    "LocationName" = EXCLUDED."LocationName",
    "Description" = EXCLUDED."Description",
    "ImageUrl" = EXCLUDED."ImageUrl",
    "AudioFileUrl" = EXCLUDED."AudioFileUrl",
    "TtsScript" = EXCLUDED."TtsScript";

-- EN translations
INSERT INTO "POITranslations" (
    "PoiId", "LanguageId", "LocationName", "Description", "ImageUrl", "AudioFileUrl", "TtsScript")
SELECT
    poi."Id",
    lang."Id",
    'Nguyen Hue Walking Street',
    'Nguyen Hue Walking Street was transformed from a historic central boulevard into a pedestrian public square in 2015. It was created to provide an open civic space where residents and visitors can gather for festivals, cultural performances, and major city events while experiencing the symbolic core of Ho Chi Minh City.',
    'https://picsum.photos/seed/nguyen_hue_walking_street/1200/800',
    'audio/en/nguyen-hue-walking-street.mp3',
    'You are near Nguyen Hue Walking Street, one of the most lively public spaces in the city center.'
FROM "POIs" poi
JOIN "Languages" lang ON lang."LanguageCode" = 'en'
WHERE poi."QRCodeId" = 'QR-PDB-004'
ON CONFLICT ("PoiId", "LanguageId") DO UPDATE
SET
    "LocationName" = EXCLUDED."LocationName",
    "Description" = EXCLUDED."Description",
    "ImageUrl" = EXCLUDED."ImageUrl",
    "AudioFileUrl" = EXCLUDED."AudioFileUrl",
    "TtsScript" = EXCLUDED."TtsScript";

INSERT INTO "POITranslations" (
    "PoiId", "LanguageId", "LocationName", "Description", "ImageUrl", "AudioFileUrl", "TtsScript")
SELECT
    poi."Id",
    lang."Id",
    'Saigon Central Post Office',
    'Saigon Central Post Office was built in the late nineteenth century during the French colonial era as a key communication hub connecting Saigon with regional and international routes. Its original purpose was postal and telegraph services, and today it also serves as a heritage destination where visitors can explore architectural history and urban memory.',
    'https://picsum.photos/seed/saigon_central_post_office/1200/800',
    'audio/en/saigon-central-post-office.mp3',
    'You are close to Saigon Central Post Office, a well-preserved architectural icon of the city.'
FROM "POIs" poi
JOIN "Languages" lang ON lang."LanguageCode" = 'en'
WHERE poi."QRCodeId" = 'QR-BD-005'
ON CONFLICT ("PoiId", "LanguageId") DO UPDATE
SET
    "LocationName" = EXCLUDED."LocationName",
    "Description" = EXCLUDED."Description",
    "ImageUrl" = EXCLUDED."ImageUrl",
    "AudioFileUrl" = EXCLUDED."AudioFileUrl",
    "TtsScript" = EXCLUDED."TtsScript";

INSERT INTO "POITranslations" (
    "PoiId", "LanguageId", "LocationName", "Description", "ImageUrl", "AudioFileUrl", "TtsScript")
SELECT
    poi."Id",
    lang."Id",
    'Independence Palace',
    'Independence Palace, rebuilt in the 1960s on the site of an earlier administrative residence, served as a central government headquarters before 1975. It now exists as a preserved historical monument and museum, helping visitors understand major turning points in modern Vietnamese history and the country''s path toward reunification.',
    'https://picsum.photos/seed/independence_palace/1200/800',
    'audio/en/independence-palace.mp3',
    'You are near Independence Palace, an important historical site in Ho Chi Minh City.'
FROM "POIs" poi
JOIN "Languages" lang ON lang."LanguageCode" = 'en'
WHERE poi."QRCodeId" = 'QR-DDL-006'
ON CONFLICT ("PoiId", "LanguageId") DO UPDATE
SET
    "LocationName" = EXCLUDED."LocationName",
    "Description" = EXCLUDED."Description",
    "ImageUrl" = EXCLUDED."ImageUrl",
    "AudioFileUrl" = EXCLUDED."AudioFileUrl",
    "TtsScript" = EXCLUDED."TtsScript";

-- MediaAssets liên quan (ảnh + audio) theo từng bản dịch.
INSERT INTO "MediaAssets" (
    "PoiTranslationId", "AssetType", "RemoteUrl", "LocalPath", "FileName", "ContentHash", "IsDownloaded", "LastDownloadedUtc")
SELECT
    t."Id",
    'image',
    'https://cdn.foodmap.local/images/pho-di-bo-nguyen-hue.jpg',
    'images/pho-di-bo-nguyen-hue.jpg',
    'pho-di-bo-nguyen-hue.jpg',
    NULL,
    FALSE,
    NULL
FROM "POITranslations" t
JOIN "POIs" p ON p."Id" = t."PoiId"
JOIN "Languages" l ON l."Id" = t."LanguageId"
WHERE p."QRCodeId" = 'QR-PDB-004' AND l."LanguageCode" = 'vi'
ON CONFLICT ("PoiTranslationId", "AssetType") DO UPDATE
SET
    "RemoteUrl" = EXCLUDED."RemoteUrl",
    "LocalPath" = EXCLUDED."LocalPath",
    "FileName" = EXCLUDED."FileName",
    "IsDownloaded" = FALSE,
    "LastDownloadedUtc" = NULL;

INSERT INTO "MediaAssets" (
    "PoiTranslationId", "AssetType", "RemoteUrl", "LocalPath", "FileName", "ContentHash", "IsDownloaded", "LastDownloadedUtc")
SELECT
    t."Id",
    'audio',
    'https://cdn.foodmap.local/audio/vi/pho-di-bo-nguyen-hue.mp3',
    'audio/vi/pho-di-bo-nguyen-hue.mp3',
    'pho-di-bo-nguyen-hue.mp3',
    NULL,
    FALSE,
    NULL
FROM "POITranslations" t
JOIN "POIs" p ON p."Id" = t."PoiId"
JOIN "Languages" l ON l."Id" = t."LanguageId"
WHERE p."QRCodeId" = 'QR-PDB-004' AND l."LanguageCode" = 'vi'
ON CONFLICT ("PoiTranslationId", "AssetType") DO UPDATE
SET
    "RemoteUrl" = EXCLUDED."RemoteUrl",
    "LocalPath" = EXCLUDED."LocalPath",
    "FileName" = EXCLUDED."FileName",
    "IsDownloaded" = FALSE,
    "LastDownloadedUtc" = NULL;

INSERT INTO "MediaAssets" (
    "PoiTranslationId", "AssetType", "RemoteUrl", "LocalPath", "FileName", "ContentHash", "IsDownloaded", "LastDownloadedUtc")
SELECT
    t."Id",
    'image',
    'https://cdn.foodmap.local/images/buu-dien-trung-tam-sai-gon.jpg',
    'images/buu-dien-trung-tam-sai-gon.jpg',
    'buu-dien-trung-tam-sai-gon.jpg',
    NULL,
    FALSE,
    NULL
FROM "POITranslations" t
JOIN "POIs" p ON p."Id" = t."PoiId"
JOIN "Languages" l ON l."Id" = t."LanguageId"
WHERE p."QRCodeId" = 'QR-BD-005' AND l."LanguageCode" = 'vi'
ON CONFLICT ("PoiTranslationId", "AssetType") DO UPDATE
SET
    "RemoteUrl" = EXCLUDED."RemoteUrl",
    "LocalPath" = EXCLUDED."LocalPath",
    "FileName" = EXCLUDED."FileName",
    "IsDownloaded" = FALSE,
    "LastDownloadedUtc" = NULL;

INSERT INTO "MediaAssets" (
    "PoiTranslationId", "AssetType", "RemoteUrl", "LocalPath", "FileName", "ContentHash", "IsDownloaded", "LastDownloadedUtc")
SELECT
    t."Id",
    'audio',
    'https://cdn.foodmap.local/audio/vi/buu-dien-trung-tam-sai-gon.mp3',
    'audio/vi/buu-dien-trung-tam-sai-gon.mp3',
    'buu-dien-trung-tam-sai-gon.mp3',
    NULL,
    FALSE,
    NULL
FROM "POITranslations" t
JOIN "POIs" p ON p."Id" = t."PoiId"
JOIN "Languages" l ON l."Id" = t."LanguageId"
WHERE p."QRCodeId" = 'QR-BD-005' AND l."LanguageCode" = 'vi'
ON CONFLICT ("PoiTranslationId", "AssetType") DO UPDATE
SET
    "RemoteUrl" = EXCLUDED."RemoteUrl",
    "LocalPath" = EXCLUDED."LocalPath",
    "FileName" = EXCLUDED."FileName",
    "IsDownloaded" = FALSE,
    "LastDownloadedUtc" = NULL;

INSERT INTO "MediaAssets" (
    "PoiTranslationId", "AssetType", "RemoteUrl", "LocalPath", "FileName", "ContentHash", "IsDownloaded", "LastDownloadedUtc")
SELECT
    t."Id",
    'image',
    'https://cdn.foodmap.local/images/dinh-doc-lap.jpg',
    'images/dinh-doc-lap.jpg',
    'dinh-doc-lap.jpg',
    NULL,
    FALSE,
    NULL
FROM "POITranslations" t
JOIN "POIs" p ON p."Id" = t."PoiId"
JOIN "Languages" l ON l."Id" = t."LanguageId"
WHERE p."QRCodeId" = 'QR-DDL-006' AND l."LanguageCode" = 'vi'
ON CONFLICT ("PoiTranslationId", "AssetType") DO UPDATE
SET
    "RemoteUrl" = EXCLUDED."RemoteUrl",
    "LocalPath" = EXCLUDED."LocalPath",
    "FileName" = EXCLUDED."FileName",
    "IsDownloaded" = FALSE,
    "LastDownloadedUtc" = NULL;

INSERT INTO "MediaAssets" (
    "PoiTranslationId", "AssetType", "RemoteUrl", "LocalPath", "FileName", "ContentHash", "IsDownloaded", "LastDownloadedUtc")
SELECT
    t."Id",
    'audio',
    'https://cdn.foodmap.local/audio/vi/dinh-doc-lap.mp3',
    'audio/vi/dinh-doc-lap.mp3',
    'dinh-doc-lap.mp3',
    NULL,
    FALSE,
    NULL
FROM "POITranslations" t
JOIN "POIs" p ON p."Id" = t."PoiId"
JOIN "Languages" l ON l."Id" = t."LanguageId"
WHERE p."QRCodeId" = 'QR-DDL-006' AND l."LanguageCode" = 'vi'
ON CONFLICT ("PoiTranslationId", "AssetType") DO UPDATE
SET
    "RemoteUrl" = EXCLUDED."RemoteUrl",
    "LocalPath" = EXCLUDED."LocalPath",
    "FileName" = EXCLUDED."FileName",
    "IsDownloaded" = FALSE,
    "LastDownloadedUtc" = NULL;

COMMIT;
