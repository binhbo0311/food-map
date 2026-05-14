-- Run schema migration + test seed scripts in order (psql)
-- Usage example:
-- 1) psql -h <host> -p <port> -U <user> -f postgresql-init.sql
-- 2) psql -h <host> -p <port> -U <user> -d food_map -f seed-all.sql

-- Dùng script Phase 2 làm nguồn chuẩn để đảm bảo schema và dữ liệu test đồng nhất.
\i 04-phase2-schema-update.sql
\i 05-subscription-schema-update.sql
\i 06-tourlist-schema-update.sql
\i 09-listen-count-schema-update.sql
\i 08-tourlist-ownership-schema-update.sql
\i 07-tourlist-test-seed.sql
