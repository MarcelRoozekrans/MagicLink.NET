using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MagicLink;

/// <summary>
/// Runs the query of who may sign in once, with a made-up address, when MagicLink.NET starts while the database is the
/// source in use, and refuses to start when it fails, with the reason (ADR-014).
/// </summary>
/// <remarks>The source is looked up while validating, not injected, as <see cref="SmtpSettingsCheck"/> does with the sender.</remarks>
internal sealed class SignInDatabaseCheck(IServiceProvider services) : IValidateOptions<MagicLinkSignInDatabaseOptions>
{
    /// <summary>The address the query runs with at startup: the <c>.invalid</c> top-level domain never exists.</summary>
    internal const string MadeUpAddress = "startup-check@magiclink.invalid";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, MagicLinkSignInDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return services.GetService<ISignInSource>() is SignInDatabaseSource
            ? Check(services.GetRequiredService<SignInDatabaseProvider>().Factory, options)
            : ValidateOptionsResult.Skip;
    }

    /// <summary>
    /// Whether the query in <paramref name="options"/> runs on the database for the made-up address, returns one text
    /// column and does not let that address sign in.
    /// </summary>
    internal static ValidateOptionsResult Check(DbProviderFactory factory, MagicLinkSignInDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("MagicLink.NET reads who may sign in from a database, so MagicLinkSignInDatabaseOptions.ConnectionString must be set.");
        }

        if (string.IsNullOrWhiteSpace(options.Query))
        {
            return ValidateOptionsResult.Fail("MagicLink.NET reads who may sign in from a database, so MagicLinkSignInDatabaseOptions.Query must be set.");
        }

        try
        {
            var problem = Run(factory, options.ConnectionString, options.Query);
            return problem is null ? ValidateOptionsResult.Success : Fail(problem);
        }
        catch (Exception exception) when (exception is DbException or ArgumentException)
        {
            return Fail("MagicLink.NET cannot run its query: " + exception.Message);
        }
    }

    /// <summary>Runs the query for the made-up address in a transaction that is rolled back; returns what is wrong, if anything.</summary>
    private static string? Run(DbProviderFactory factory, string connectionString, string query)
    {
        using var dataSource = factory.CreateDataSource(connectionString);
        using var connection = dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();
        string? problem;
        using (var command = SignInDatabaseSource.Command(connection, transaction, query, MadeUpAddress))
        using (var reader = command.ExecuteReader())
        {
            problem = reader.FieldCount != 1
                ? $"it returns {reader.FieldCount} columns, but must return one column: the group."
                : reader.GetFieldType(0) != typeof(string)
                    ? $"its column is of type {reader.GetDataTypeName(0)}, but must be text: the name of the group."
                    : reader.Read()
                        ? $"it lets the made-up address {MadeUpAddress} sign in, so it would let anyone in. It must look up the address in {SignInDatabaseSource.EmailParameter}."
                        : null;
        }

        transaction.Rollback();
        return problem;
    }

    private static ValidateOptionsResult Fail(string problem) =>
        ValidateOptionsResult.Fail($"The query of who may sign in, MagicLinkSignInDatabaseOptions.Query, does not work: {problem}");
}
