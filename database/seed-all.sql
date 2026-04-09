-- Run all seed scripts in order (psql)
-- Usage example:
-- psql -h <host> -p <port> -U <user> -d food_map -f seed-all.sql

\i 01-seed-poi-data.sql
\i 02-seed-user-data.sql
\i 03-seed-new-zones.sql
