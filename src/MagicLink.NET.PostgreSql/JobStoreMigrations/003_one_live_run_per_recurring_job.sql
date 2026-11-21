-- ZeroAlloc.Scheduling job table, version 3: the job store of MagicLink.NET's pruning job, as
-- ZeroAlloc.Scheduling.Orm's SchedulingOrmMigrations.Postgres defines it. Apply after the store's scripts,
-- with search_path set to magiclink, so the table lands next to the store's tables.

DELETE FROM SchedulingJobs
WHERE CronExpression IS NOT NULL AND Status IN (0, 1, 3)
  AND EXISTS (
      SELECT 1 FROM SchedulingJobs AS Kept
      WHERE Kept.TypeName = SchedulingJobs.TypeName
        AND Kept.CronExpression IS NOT NULL AND Kept.Status IN (0, 1, 3)
        AND (Kept.ScheduledAt < SchedulingJobs.ScheduledAt
             OR (Kept.ScheduledAt = SchedulingJobs.ScheduledAt AND Kept.Id < SchedulingJobs.Id)));
CREATE UNIQUE INDEX IF NOT EXISTS UX_SchedulingJobs_LiveRecurring
    ON SchedulingJobs (TypeName)
    WHERE CronExpression IS NOT NULL AND Status IN (0, 1, 3);
