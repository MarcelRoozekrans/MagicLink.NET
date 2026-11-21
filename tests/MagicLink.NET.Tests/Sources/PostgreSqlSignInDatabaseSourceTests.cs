namespace MagicLink.Tests.Sources;

public sealed class PostgreSqlSignInDatabaseSourceTests(PostgreSqlUserDatabase database) : SignInDatabaseSourceTests<PostgreSqlUserDatabase>(database);
