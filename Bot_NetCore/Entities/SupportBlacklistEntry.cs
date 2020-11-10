using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public class SupportBlacklistEntry
    {
        public ulong UserId { get; }
        public DateTime BanDate { get; }
        public ulong ModeratorId { get; }
        public string Reason { get; }

        private SupportBlacklistEntry(ulong userId, DateTime banDate, ulong moderatorId, string reason)
        {
            UserId = userId;
            BanDate = banDate;
            ModeratorId = moderatorId;
            Reason = reason;
        }

        public static SupportBlacklistEntry Create(ulong userId, DateTime banDate, ulong moderatorId, string reason)
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand
            {
                CommandText = $"INSERT INTO support_blacklist(user_id, ban_date, moderator_id, reason) " +
                              $"VALUES ('{userId}', '{banDate:yyyy-MM-dd}', '{moderatorId}', '{reason}');",
                Connection = connection
            };
            cmd.Connection.Open();

            cmd.ExecuteNonQuery();

            return new SupportBlacklistEntry(userId, banDate, moderatorId, reason);
        }

        public static void Remove(ulong userId)
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand
            {
                CommandText = $"DELETE FROM support_blacklist WHERE user_id = '{userId}';",
                Connection = connection
            };
            cmd.Connection.Open();

            cmd.ExecuteNonQuery();
        }

        public static bool IsBlacklisted(ulong userId)
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand
            {
                CommandText = $"SELECT * FROM support_blacklist WHERE user_id='{userId}';",
                Connection = connection
            };
            cmd.Connection.Open();

            var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return false;
            }

            return true;
        }

        public static List<SupportBlacklistEntry> GetBlacklisted(ulong userId = 0)
        {
            var result = new List<SupportBlacklistEntry>();

            string command;
            if (userId != 0)
                command = $"SELECT * FROM support_blacklist WHERE user_id='{userId}';";
            else
                command = $"SELECT * FROM support_blacklist;";

            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand
            {
                CommandText = command,
                Connection = connection
            };
            cmd.Connection.Open();

            var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new SupportBlacklistEntry(
                    reader.GetUInt64("user_id"), 
                    reader.GetDateTime("ban_date"),
                    reader.GetUInt64("moderator_id"),
                    reader.GetString("reason")
                    ));
            }

            return result;
        }
    }
}