using System.Collections.Generic;
using System.Threading.Tasks;
using Bot_NetCore.Models;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.DAL;

public static class MemberJoinsDAL
{
    public static async Task InsertAsync(MemberJoin data)
    {
        const string sql = "INSERT INTO member_joins(member_id, member_name, join_date, invite) VALUES (@memberId, @memberName, @joinDate, @invite)";
        await using var connection = new MySqlConnection(Bot.ConnectionString);
        await using var cmd = connection.CreateCommand();

        cmd.Parameters.AddWithValue("@memberId", data.MemberId);
        cmd.Parameters.AddWithValue("@memberName", data.Username);
        cmd.Parameters.AddWithValue("joinDate", data.JoinDate);
        cmd.Parameters.AddWithValue("@invite", data.Invite);

        cmd.CommandText = sql;
        cmd.Connection = connection;
        await cmd.Connection.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public static async IAsyncEnumerable<MemberJoin> GetLatestJoinsAsync(int count)
    {
        const string sql = "SELECT member_id, member_name, join_date, invite FROM member_joins ORDER BY join_date DESC LIMIT @count";
        await using var connection = new MySqlConnection(Bot.ConnectionString);
        await using var cmd = connection.CreateCommand();

        cmd.Parameters.AddWithValue("@count", count);
        cmd.CommandText = sql;
        cmd.Connection = connection;
        await cmd.Connection.OpenAsync();
        var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var entity = new MemberJoin
            {
                MemberId = reader.GetString(0),
                Username = reader.GetString(1),
                JoinDate = reader.GetDateTime(2),
                Invite = reader.GetString(3)
            };
            yield return entity;
        }
    }
}