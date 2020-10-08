using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public class BanSQL
    {
        public readonly string Id;

        private ulong _user;
        private ulong _moderator;
        private string _reason;
        private DateTime _banDateTime;
        private DateTime _unbanDateTime;

        public ulong User => _user;

        public ulong Moderator
        {
            get => _moderator;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE bans SET moderator = '{value}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();
                    }
                }
                
                _moderator = value;
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
                        var statement = $"UPDATE bans SET reason = '{value}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();
                    }
                }
                _reason = value;
            }
        }

        public DateTime BanDateTime
        {
            get => _banDateTime;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE bans SET ban = '{value:yyyy-MM-dd HH:mm:ss}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();
                    }
                }
                _banDateTime = value;
            }
        }

        public DateTime UnbanDateTime
        {
            get => _unbanDateTime;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE bans SET unban = '{value:yyyy-MM-dd HH:mm:ss}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();
                    }
                }
                _unbanDateTime = value;
            }
        }

        private BanSQL(string id, ulong user, ulong moderator, string reason, DateTime banDateTime,
            DateTime unbanDateTime)
        {
            Id = id;
            _user = user;
            _moderator = moderator;
            _reason = reason;
            _banDateTime = banDateTime;
            _unbanDateTime = unbanDateTime;
        }

        public static BanSQL Create(string id, ulong user, ulong moderator, string reason, DateTime banDateTime,
            DateTime unbanDateTime)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    var statement =
                        $"INSERT INTO bans(id, user, moderator, reason, ban, unban) VALUES ('{id}', '{user}', '{moderator}', '{reason}', '{banDateTime:yyyy-MM-dd HH:mm:ss}', '{unbanDateTime:yyyy-MM-dd HH:mm:ss}');";
                    cmd.CommandText = statement;
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    cmd.ExecuteNonQuery();

                    return new BanSQL(id, user, moderator, reason, banDateTime, unbanDateTime);
                }
            }
        }

        public static void Remove(string id)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    var statement = $"DELETE FROM bans WHERE id = '{id}';";
                    cmd.CommandText = statement;
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static BanSQL Get(string id)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"SELECT * FROM bans WHERE id='{id}';";
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        return null;
                    }
                    else
                    {
                        var ret = new BanSQL(reader.GetString("id"), reader.GetUInt64("user"),
                            reader.GetUInt64("moderator"),
                            reader.GetString("reason"), reader.GetDateTime("ban"), reader.GetDateTime("unban"));
                        return ret;
                    }
                }
            }
        }

        public static List<BanSQL> GetForUser(ulong user)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    var statement = $"SELECT * FROM bans WHERE user='{user}';";
                    cmd.CommandText = statement;
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();

                    var bans = new List<BanSQL>();
                    while (reader.Read())
                    {
                        bans.Add(new BanSQL(reader.GetString("id"), reader.GetUInt64("user"),
                            reader.GetUInt64("moderator"),
                            reader.GetString("reason"), reader.GetDateTime("ban"), reader.GetDateTime("unban")));
                    }

                    return bans;
                }
            }
        }

        public static List<BanSQL> GetExpiredBans()
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    var statement = $"SELECT * FROM bans WHERE unban <= '{DateTime.Now:yyyy-MM-dd HH:mm:ss}';";
                    cmd.CommandText = statement;
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();

                    var bans = new List<BanSQL>();
                    while (reader.Read())
                    {
                        bans.Add(new BanSQL(reader.GetString("id"), reader.GetUInt64("user"),
                            reader.GetUInt64("moderator"),
                            reader.GetString("reason"), reader.GetDateTime("ban"), reader.GetDateTime("unban")));
                    }

                    return bans;
                }
            }
        }
    }
}