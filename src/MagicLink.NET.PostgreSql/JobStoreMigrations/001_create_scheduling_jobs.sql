-- ZeroAlloc.Scheduling job table, version 1: the job store of MagicLink.NET's pruning job, as
-- ZeroAlloc.Scheduling.Orm's SchedulingOrmMigrations.Postgres defines it. Apply after the store's scripts,
-- with search_path set to magiclink, so the table lands next to the store's tables.

CREATE TABLE IF NOT EXISTS SchedulingJobs (
    Id             UUID NOT NULL PRIMARY KEY,
    TypeName       VARCHAR(512) NOT NULL,
    Payload        BYTEA NOT NULL,
    Status         INTEGER NOT NULL,
    Attempts       INTEGER NOT NULL,
    MaxAttempts    INTEGER NOT NULL,
    ScheduledAt    TIMESTAMPTZ NOT NULL,
    StartedAt      TIMESTAMPTZ NULL,
    CompletedAt    TIMESTAMPTZ NULL,
    NextRunAt      TIMESTAMPTZ NULL,
    CronExpression TEXT NULL,
    Error          TEXT NULL
);
CREATE INDEX IF NOT EXISTS IX_SchedulingJobs_Status_ScheduledAt
    ON SchedulingJobs (Status, ScheduledAt);
