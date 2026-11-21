using System.Data.Async;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Scheduling;

namespace MagicLink.PostgreSql.Tests;

/// <summary>What <c>UsePostgreSql</c> registers for the pruning job, read from the service collection.</summary>
public sealed class PruningRegistrationTests
{
    private const string ConnectionString = "Host=localhost;Database=unused";

    private static ServiceCollection Register(Action<IServiceCollection>? before = null, bool prune = true)
    {
        var services = new ServiceCollection();
        before?.Invoke(services);
        services.AddOpenIddict().AddCore(options => options.UsePostgreSql(store =>
        {
            store.ConnectionString = ConnectionString;
            store.PruneExpiredData = prune;
        }));
        return services;
    }

    private static int Count<T>(IServiceCollection services) => services.Count(descriptor => descriptor.ServiceType == typeof(T));

    private static bool HasJobStoreConnection(IServiceCollection services) => services.Any(descriptor =>
        descriptor.ServiceType == typeof(IAsyncDbConnection) && Equals(descriptor.ServiceKey, StoreDatabase.JobStoreConnectionKey));

    [Fact]
    public void Pruning_is_on_by_default()
    {
        Assert.True(new MagicLinkPostgreSqlOptions().PruneExpiredData);
    }

    [Fact]
    public void Pruning_off_registers_no_job_and_no_scheduler()
    {
        var services = Register(prune: false);

        Assert.Equal(0, Count<IJobTypeExecutor>(services));
        Assert.Equal(0, Count<SchedulingWorkerService>(services));
        Assert.Equal(0, Count<IJobStore>(services));
        Assert.False(HasJobStoreConnection(services));
    }

    [Fact]
    public void Without_the_hosts_scheduler_the_store_adds_one_with_its_job_store_on_the_stores_database()
    {
        var services = Register();

        Assert.Equal(1, Count<IJobTypeExecutor>(services));
        Assert.Equal(1, Count<SchedulingWorkerService>(services));
        Assert.Equal(1, Count<IJobStore>(services));
        Assert.True(HasJobStoreConnection(services));
    }

    [Fact]
    public void With_the_hosts_scheduler_the_store_only_adds_its_job()
    {
        var services = Register(before: static services => services.AddScheduling());

        Assert.Equal(1, Count<IJobTypeExecutor>(services));
        Assert.Equal(1, Count<SchedulingWorkerService>(services));
        Assert.Equal(0, Count<IJobStore>(services));
        Assert.False(HasJobStoreConnection(services));
    }

    [Fact]
    public void The_job_store_scripts_in_the_package_match_the_migrations_that_run()
    {
        var migrations = ZeroAlloc.Scheduling.Orm.SchedulingOrmMigrations.Postgres.GetMigrations();
        var scripts = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "JobStoreMigrations"), "*.sql").Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(migrations.Count, scripts.Length);
        for (var i = 0; i < scripts.Length; i++)
        {
            Assert.StartsWith($"{migrations[i].Version:000}_{migrations[i].Name}.sql", Path.GetFileName(scripts[i]), StringComparison.Ordinal);
            var body = string.Join('\n', File.ReadAllLines(scripts[i]).SkipWhile(static line => line.StartsWith("--", StringComparison.Ordinal) || line.Length == 0));
            Assert.Equal(migrations[i].Sql.ReplaceLineEndings("\n").Trim(), body.Trim());
        }
    }
}
