using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public class TicketSQL
    {
        private ulong _messageId;
        private string _category;
        private TicketStatus _status;

        public readonly ulong ChannelId; //Key
        public readonly ulong UserId;
        public readonly ulong DmChannelId;
        public readonly ulong DmMessageId;
        public readonly string Text;
        public readonly DateTime CreatedAt;
        public readonly DateTime LastUpdated;

        public ulong MessageId
        {
            get => _messageId;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE tickets SET message = '{value}' WHERE channel = '{ChannelId}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        cmd.ExecuteNonQuery();

                        _messageId = value;
                    }
                }
            }
        }

        public string Category
        { 
            get => _category;
            set 
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE tickets SET category = '{value}' WHERE channel = '{ChannelId}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        cmd.ExecuteNonQuery();

                        _category = value;
                    }
                }
            }
        }
        public TicketStatus Status
        {
            get => _status;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE tickets SET status = '{value}' WHERE channel = '{ChannelId}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        cmd.ExecuteNonQuery();

                        _status = value;
                    }
                }
            }
        }

        public TicketSQL(ulong channelId, ulong userId, ulong dmChannelId, ulong dmMessageId, string text, DateTime createdAt, string category, ulong messageId, TicketStatus status, DateTime lastUpdated)
        {
            ChannelId = channelId;
            UserId = userId;
            DmChannelId = dmChannelId;
            DmMessageId = dmMessageId;
            Text = text;
            CreatedAt = createdAt;
            Category = category;
            MessageId = messageId;
            Status = status;
            LastUpdated = lastUpdated;
        }


        /// <summary>
        ///     Создаёт тикет с заданными параметрами
        /// </summary>
        public static TicketSQL Create(ulong channelId, ulong userId, ulong dmChannelId, ulong dmMessageId, string text, DateTime createdAt, string category, ulong messageId = 0, TicketStatus status = TicketStatus.Open)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    text = text.Replace("'", "''");

                    var statement = $"INSERT INTO tickets(channel, user, dm_channel, dm_message, message, text, created_at, category, status) " +
                                    $"VALUES ('{channelId}', '{userId}', '{dmChannelId}', '{dmMessageId}', '{messageId}', '{text}', '{createdAt:yyyy-MM-dd HH:mm:ss}', '{category}', '{status}');";
                    cmd.CommandText = statement;
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    cmd.ExecuteNonQuery();

                    return new TicketSQL(channelId, userId, dmChannelId, dmMessageId, text, createdAt, category, messageId, status, DateTime.Now);
                }
            }
        }

        /// <summary>
        ///     Получает тикет из базы данных по заданному Id
        /// </summary>
        /// <param name="connection">Соединение с БД</param>
        /// <param name="id">ID канала</param>
        /// <returns>Тикет или null, если не существует</returns>
        public static TicketSQL Get(ulong id)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"SELECT * FROM tickets WHERE channel='{id}';";
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        return null;
                    }
                    else
                    {
                        //ulong channelId, ulong userId, ulong dmChannelId, ulong dmMessageId, string text, DateTime createdAt, string category, ulong messageId, TicketStatus status)
                        var ret = new TicketSQL(
                            reader.GetUInt64("channel"),
                            reader.GetUInt64("user"),
                            reader.GetUInt64("dm_channel"),
                            reader.GetUInt64("dm_message"),
                            reader.GetString("text"),
                            reader.GetDateTime("created_at"),
                            reader.GetString("category"),
                            reader.GetUInt64("message"),
                            reader.GetString("status").ToLower() switch 
                            {
                                "open" => TicketStatus.Open,
                                "closed" => TicketStatus.Closed,
                                "deleted" => TicketStatus.Deleted,
                                _ => throw new Exception("Unable to get TicketStatus")
                            },
                            reader.GetDateTime("last_updated"));
                        return ret;
                    }
                }
            }
        }

        public static List<TicketSQL> GetClosedFor(TimeSpan time)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    var statement = $"SELECT * FROM tickets WHERE status = 'Closed' AND TIMEDIFF(current_timestamp, last_updated) > '{time}';";
                    cmd.CommandText = statement;
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();

                    var tickets = new List<TicketSQL>();
                    while (reader.Read())
                    {
                        tickets.Add(new TicketSQL(
                            reader.GetUInt64("channel"),
                            reader.GetUInt64("user"),
                            reader.GetUInt64("dm_channel"),
                            reader.GetUInt64("dm_message"),
                            reader.GetString("text"),
                            reader.GetDateTime("created_at"),
                            reader.GetString("category"),
                            reader.GetUInt64("message"),
                            reader.GetString("status").ToLower() switch
                            {
                                "open" => TicketStatus.Open,
                                "closed" => TicketStatus.Closed,
                                "deleted" => TicketStatus.Deleted,
                                _ => throw new Exception("Unable to get TicketStatus")
                            },
                            reader.GetDateTime("last_updated")));
                    }

                    return tickets;
                }
            }
        }

        /// <summary>
        /// Удаляет тикет с заданным Id
        /// </summary>
        public static void Delete(ulong id)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    var statement = $"DELETE FROM tickets WHERE channel = '{id}';";
                    cmd.CommandText = statement;
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public enum TicketStatus
        {
            Open,
            Closed,
            Deleted
        }
    }
}
