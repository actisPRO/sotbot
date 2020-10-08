using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public class WarnSQL
    {
        public readonly string Id;

        private ulong _user;
        private ulong _moderator;
        private string _reason;
        private DateTime _date;
        private ulong _logMessage;

        public ulong User
        {
            get => _user;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE warnings SET user = '{value}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        _user = value;
                    }
                }
            }
        }

        public ulong Moderator
        {
            get => _moderator;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE warnings SET moderator = '{value}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        _moderator = value;
                    }
                }
            }
        }

        public string Reason
        {
            get => _reason;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE warnings SET reason = '{value}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        _reason = value;
                    }
                }
            }
        }

        public DateTime Date
        {
            get => _date;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE warnings SET date = '{value:yyyy-MM-dd HH:mm:ss}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        _date = value;
                    }
                }
            }
        }

        public ulong LogMessage
        {
            get => _logMessage;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE warnings SET logmessage = '{value}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        _logMessage = value;
                    }
                }
            }
        }

        private WarnSQL(string id, ulong user, ulong moderator, string reason, DateTime date, ulong logMessage)
        {
            Id = id;
            _user = user;
            _moderator = moderator;
            _reason = reason;
            _date = date;
            _logMessage = logMessage;
        }

        /// <summary>
        /// Создаёт предупреждение с заданными параметрами
        /// </summary>
        public static WarnSQL Create(string id, ulong user, ulong moderator, string reason,
            DateTime date, ulong logMessage)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    var statement = $"INSERT INTO warnings(id, user, moderator, reason, date, logmessage) " +
                                    $"VALUES ('{id}', {user}, {moderator}, '{reason}', '{date:yyyy-MM-dd HH:mm:ss}', {logMessage});";
                    cmd.CommandText = statement;
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    cmd.ExecuteNonQuery();

                    return new WarnSQL(id, user, moderator, reason, date, logMessage);
                }
            }
        }

        /// <summary>
        /// Получает предупреждение из базы данных по заданному Id
        /// </summary>
        /// <param name="connection">Соединение с БД</param>
        /// <param name="id">ID предупреждения</param>
        /// <returns>Предупреждение или null, если не существует</returns>
        public static WarnSQL Get(string id)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"SELECT * FROM warnings WHERE id='{id}';";
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        return null;
                    }
                    else
                    {
                        var ret = new WarnSQL(reader.GetString("id"), reader.GetUInt64("user"),
                            reader.GetUInt64("moderator"),
                            reader.GetString("reason"), reader.GetDateTime("date"), reader.GetUInt64("logmessage"));
                        return ret;
                    }
                }
            }
        }

        /// <summary>
        /// Удаляет предупреждение с заданным Id
        /// </summary>
        public static void Delete(string id)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    var statement = $"DELETE FROM warnings WHERE id = '{id}';";
                    cmd.CommandText = statement;
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static List<WarnSQL> GetForUser(ulong user)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    var statement = $"SELECT * FROM warnings WHERE user='{user}';";
                    cmd.CommandText = statement;
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();

                    var warns = new List<WarnSQL>();
                    while (reader.Read())
                    {
                        warns.Add(new WarnSQL(reader.GetString("id"), reader.GetUInt64("user"),
                            reader.GetUInt64("moderator"),
                            reader.GetString("reason"), reader.GetDateTime("date"), reader.GetUInt64("logmessage")));
                    }

                    return warns;
                }
            }
        }
    }
}