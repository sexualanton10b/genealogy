using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

public sealed class DbSessionContextInterceptor : DbConnectionInterceptor
{
    private readonly IHttpContextAccessor _http;

    public DbSessionContextInterceptor(IHttpContextAccessor http)
    {
        _http = http;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        // если не SQL Server — выходим
        if (connection is not SqlConnection sqlConn)
            return;

        // пробуем достать userId из токена
        var user = _http.HttpContext?.User;
        var claim = user?.FindFirst("userId") ?? user?.FindFirst(ClaimTypes.NameIdentifier);

        // если запроса нет (фоновые задачи/сидинг) — чистим контекст
        object? value = null;
        if (claim != null && int.TryParse(claim.Value, out var uid))
            value = uid;

        using var cmd = sqlConn.CreateCommand();
        cmd.CommandText = "EXEC sp_set_session_context @key=N'UserId', @value=@val;";
        cmd.Parameters.Add(new SqlParameter("@val", value ?? (object)DBNull.Value));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
