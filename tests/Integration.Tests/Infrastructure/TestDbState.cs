using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Integration.Tests.Infrastructure;

/// <summary>Chia sẻ connection + transaction ambient giữa seeder và mọi DbContext trong WebApplicationFactory.</summary>
public sealed class TestDbState
{
    public SqlConnection Connection { get; set; } = null!;
    public DbTransaction? Transaction { get; set; }
}
