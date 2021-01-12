using System;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public class VoiceTimeSQL
    {
        public static TimeSpan GetForUser(ulong userId)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"SELECT time_seconds FROM voice_times WHERE user_id = {userId}";
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        return TimeSpan.FromSeconds(reader.GetInt64("time_seconds"));
                    }
                    return TimeSpan.Zero;
                }
            }
        }

        public static void AddForUser(ulong userId, TimeSpan time)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"INSERT INTO voice_times(user_id, time_seconds) VALUES('{userId}', '{(long)time.TotalSeconds}')\n" +
                                      $"ON DUPLICATE KEY UPDATE time_seconds = time_seconds + '{(long)time.TotalSeconds}'";
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
