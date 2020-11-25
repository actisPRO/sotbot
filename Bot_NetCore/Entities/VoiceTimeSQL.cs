using System;
using System.Collections.Generic;
using System.Text;
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
                    cmd.CommandText = $"SELECT time FROM voice_times WHERE user_id = {userId}";
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        return reader.GetTimeSpan("time");
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
                    cmd.CommandText = $"INSERT INTO voice_times(user_id, time) VALUES('{userId}', '{time}')" +
                                      $"ON DUPLICATE KEY UPDATE time = ADDTIME(time,'{time}')";
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
