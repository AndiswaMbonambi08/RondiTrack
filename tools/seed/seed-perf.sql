-- Seeds ronditrack_perf only. Never run against the dev database. Not run on app startup.
DO $$ BEGIN
  IF current_database() <> 'ronditrack_perf' THEN
    RAISE EXCEPTION 'Refusing to seed database %', current_database();
  END IF;
END $$;

BEGIN;

CREATE TEMP TABLE seed_stokvels AS
  SELECT gen_random_uuid() AS id, g AS n FROM generate_series(1, 10) g;

INSERT INTO "Stokvels" ("Id", "ContributionAmount", "Name")
  SELECT id, 500, 'Perf Stokvel ' || n FROM seed_stokvels;

CREATE TEMP TABLE seed_users AS
  SELECT gen_random_uuid() AS id, s.id AS stokvel_id, u AS n
  FROM seed_stokvels s CROSS JOIN generate_series(1, 400) u;

INSERT INTO "Users" ("Id", "FullName", "Email", "IsActive")
  SELECT id, 'Perf User ' || n, id || '@perf.example.com', true FROM seed_users;

INSERT INTO "StokvelMembers" ("StokvelId", "UserId", "JoinedAtUtc", "Role")
  SELECT stokvel_id, id, now() - (random() * 365 || ' days')::interval, 0 FROM seed_users;

CREATE TEMP TABLE seed_cycles AS
  SELECT gen_random_uuid() AS id, s.id AS stokvel_id, c AS n
  FROM seed_stokvels s CROSS JOIN generate_series(1, 15) c;

-- ADAPT: add any other NOT NULL columns your ContributionCycles table has
INSERT INTO "ContributionCycles" ("Id", "StokvelId", "Label", "TargetAmount")
  SELECT id, stokvel_id, 'Cycle ' || n, 200000 FROM seed_cycles;

-- one contribution per member per cycle: 10 stokvels x 400 members x 15 cycles = 60,000 rows
-- ADAPT: add any other NOT NULL columns your Contributions table has
INSERT INTO "Contributions" ("Id", "StokvelId", "UserId", "ContributionCycleId", "Amount", "RecordedAt")
  SELECT gen_random_uuid(), u.stokvel_id, u.id, c.id, 500,
         timestamptz '2026-01-01' + (random() * 270 || ' days')::interval
  FROM seed_users u JOIN seed_cycles c ON c.stokvel_id = u.stokvel_id;

COMMIT;
ANALYZE;