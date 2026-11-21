using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Options;

namespace MagicLink;

/// <summary>
/// Who may sign in and with which groups, from an existing PostgreSQL or SQL Server database (ADR-014); see
/// <see cref="MagicLinkSignInDatabaseOptions"/>. MagicLink.NET only reads from this database; that it cannot change it
/// rests on the account having read permission only. Each look-up runs the query to its end in a transaction that is
/// always rolled back, which cleans up after a query that writes by mistake. When the database cannot answer, or the
/// query fails in any of its statements, a look-up throws and nobody signs in.
/// </summary>
internal sealed class SignInDatabaseSource(IOptions<MagicLinkSignInDatabaseOptions> options, SignInDatabaseProvider provider) : ISignInSource, IAsyncDisposable, IDisposable
{
    /// <summary>The parameter the query gets the email address in.</summary>
    internal const string EmailParameter = "@email";

    /// <summary>Why a query that returns a second result is refused.</summary>
    internal const string MoreThanOneResult = "The query of who may sign in, MagicLinkSignInDatabaseOptions.Query, returns more than one result, but must return one result: the groups of the address.";

    // Created on the first look-up, not in the constructor: the start check of these options looks this source up.
    private readonly Lazy<DbDataSource> _dataSource = new(() => provider.Factory.CreateDataSource(options.Value.ConnectionString!));

    /// <inheritdoc />
    public async ValueTask<SignInAccess> LookUpAsync(string email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        var connection = await _dataSource.Value.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                var found = false;
                List<string>? groups = null;
                var command = Command(connection, transaction, options.Value.Query!, email);
                await using (command.ConfigureAwait(false))
                {
                    var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    await using (reader.ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            found = true;
                            if (!await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
                            {
                                (groups ??= []).Add(Group(reader.GetString(0)));
                            }
                        }

                        // Run the query to its end: an error in a later statement must keep everyone out, not be lost
                        // when the reader closes.
                        if (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
                        {
                            throw new InvalidOperationException(MoreThanOneResult);
                        }
                    }
                }

                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return found ? SignInAccess.Allowed(groups ?? []) : SignInAccess.Refused;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_dataSource.IsValueCreated)
        {
            await _dataSource.Value.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_dataSource.IsValueCreated)
        {
            _dataSource.Value.Dispose();
        }
    }

    /// <summary>The configured query, with <paramref name="email"/> in <see cref="EmailParameter"/>.</summary>
    internal static DbCommand Command(DbConnection connection, DbTransaction transaction, string query, string email)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = query;
        var parameter = command.CreateParameter();
        parameter.ParameterName = EmailParameter;
        parameter.DbType = DbType.String;
        parameter.Value = email;
        command.Parameters.Add(parameter);
        return command;
    }

    /// <summary>A group the query returned; an empty name is a fault in the database, and nobody signs in on it.</summary>
    private static string Group(string name) => string.IsNullOrWhiteSpace(name)
        ? throw new InvalidOperationException("The query of who may sign in, MagicLinkSignInDatabaseOptions.Query, returned a group with an empty name. Leave the group NULL for a person without groups.")
        : name;
}
