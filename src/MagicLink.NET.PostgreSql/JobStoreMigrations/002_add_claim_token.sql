-- ZeroAlloc.Scheduling job table, version 2: the job store of MagicLink.NET's pruning job, as
-- ZeroAlloc.Scheduling.Orm's SchedulingOrmMigrations.Postgres defines it. Apply after the store's scripts,
-- with search_path set to magiclink, so the table lands next to the store's tables.

ALTER TABLE SchedulingJobs ADD COLUMN IF NOT EXISTS ClaimToken UUID NULL;
CREATE INDEX IF NOT EXISTS IX_SchedulingJobs_ClaimToken
    ON SchedulingJobs (ClaimToken);
