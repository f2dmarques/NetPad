using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace NetPad.Data.EntityFrameworkCore.DataConnections;

public sealed class SQLiteDatabaseConnection : EntityFrameworkRelationalDatabaseConnection
{
    public SQLiteDatabaseConnection(Guid id, string name)
        : base(id, name, DataConnectionType.SQLite, "Microsoft.EntityFrameworkCore.Sqlite")
    {
    }

    public override string GetConnectionString(IDataConnectionPasswordProtector passwordProtector)
    {
        var connectionString = new StringBuilder();

        connectionString.Append($"Data Source={DatabaseName}");

        if (Password != null)
        {
            connectionString.Append($";Password={passwordProtector.Unprotect(Password)}");
        }

        return connectionString.ToString();
    }

    public override Task ConfigureDbContextOptionsAsync(DbContextOptionsBuilder builder, IDataConnectionPasswordProtector passwordProtector)
    {
        builder.UseSqlite(GetConnectionString(passwordProtector));
        return Task.CompletedTask;
    }

    public override Task<IEnumerable<string>> GetDatabasesAsync(IDataConnectionPasswordProtector passwordProtector)
    {
        return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
    }
}
