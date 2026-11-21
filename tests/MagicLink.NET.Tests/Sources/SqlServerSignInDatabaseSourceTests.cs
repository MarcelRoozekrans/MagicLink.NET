namespace MagicLink.Tests.Sources;

public sealed class SqlServerSignInDatabaseSourceTests(SqlServerUserDatabase database) : SignInDatabaseSourceTests<SqlServerUserDatabase>(database);
