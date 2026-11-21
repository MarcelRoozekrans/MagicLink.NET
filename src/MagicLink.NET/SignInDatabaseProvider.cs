using System.Data.Common;

namespace MagicLink;

/// <summary>The ADO.NET provider of the database that says who may sign in, such as Npgsql's or SqlClient's factory.</summary>
/// <param name="Factory">The provider's factory.</param>
internal sealed record SignInDatabaseProvider(DbProviderFactory Factory);
