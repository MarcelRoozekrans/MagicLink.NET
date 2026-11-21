namespace MagicLink.Tests.Sources;

// Settings checked before the database is asked anything.
public sealed class SignInDatabaseSettingsTests
{
    [Theory]
    [InlineData(null, "SELECT 1", "MagicLinkSignInDatabaseOptions.ConnectionString")]
    [InlineData("Host=localhost", " ", "MagicLinkSignInDatabaseOptions.Query")]
    public void The_start_check_refuses_missing_settings(string? connectionString, string? query, string setting)
    {
        var result = SignInDatabaseCheck.Check(Npgsql.NpgsqlFactory.Instance, new MagicLinkSignInDatabaseOptions { ConnectionString = connectionString, Query = query });

        Assert.True(result.Failed);
        Assert.Contains(setting, result.FailureMessage, StringComparison.Ordinal);
    }
}
