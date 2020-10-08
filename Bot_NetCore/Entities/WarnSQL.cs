using System;
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
                var statement = $"UPDATE warnings SET user = '{value}' WHERE id = '{Id}'";
                var cmd = Bot.Connection.CreateCommand();
                cmd.CommandText = statement;
                
                _user = value;
            }
        }

        public ulong Moderator
        {
            get => _moderator;
            set
            {
                var statement = $"UPDATE warnings SET moderator = '{value}' WHERE id = '{Id}'";
                var cmd = Bot.Connection.CreateCommand();
                cmd.CommandText = statement;
                
                _moderator = value;
            }
        }

        public string Reason
        {
            get => _reason;
            set
            {
                var statement = $"UPDATE warnings SET reason = '{value}' WHERE id = '{Id}'";
                var cmd = Bot.Connection.CreateCommand();
                cmd.CommandText = statement;
                
                _reason = value;
            }
        }

        public DateTime Date
        {
            get => _date;
            set
            {
                var statement = $"UPDATE warnings SET date = '{value:yyyy-mm-ddhh:mm:ss:ff}' WHERE id = '{Id}'";
                var cmd = Bot.Connection.CreateCommand();
                cmd.CommandText = statement;
                
                _date = value;
            }
        }

        public ulong LogMessage
        {
            get => _logMessage;
            set
            {
                var statement = $"UPDATE warnings SET logmessage = '{value}' WHERE id = '{Id}'";
                var cmd = Bot.Connection.CreateCommand();
                cmd.CommandText = statement;
                
                _logMessage = value;
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
        public static WarnSQL Create(MySqlConnection connection, string id, ulong user, ulong moderator, string reason,
            DateTime date, ulong logMessage)
        {
            var statement = $"INSERT INTO warnings(id, user, moderator, reason, date, logmessage) " +
                        $"VALUES ('{id}', {user}, {moderator}, '{reason}', '{date:yyyy-mm-ddhh:mm:ss:ff}', {logMessage});";
            var cmd = Bot.Connection.CreateCommand();
            cmd.CommandText = statement;

            cmd.ExecuteNonQuery();
            
            return new WarnSQL(id, user, moderator, reason, date, logMessage);
        }

        /// <summary>
        /// Получает предупреждение из базы данных по заданному Id
        /// </summary>
        /// <param name="connection">Соединение с БД</param>
        /// <param name="id">ID предупреждения</param>
        /// <returns>Предупреждение или null, если не существует</returns>
        public static WarnSQL Get(MySqlConnection connection, string id)
        {
            var statement = $"SELECT * FROM warnings WHERE id='{id}';";
            var cmd = Bot.Connection.CreateCommand();
            cmd.CommandText = statement;
            
            var reader = cmd.ExecuteReader();

            // отсутствуют предупреждения с указанным ID
            if (!reader.Read()) 
            {
                reader.Close();
                return null;
            }
            else
            {
                var ret = new WarnSQL(reader.GetString("id"), reader.GetUInt64("user"), reader.GetUInt64("moderator"),
                    reader.GetString("reason"), reader.GetDateTime("date"), reader.GetUInt64("logmessage"));
                reader.Close();
                return ret;
            }
        }
        
        /// <summary>
        /// Удаляет предупреждение с заданным Id
        /// </summary>
        public static void Delete(MySqlConnection connection, string id)
        {
            var statement = $"DELETE FROM warnings WHERE id = '{id}';";
            var cmd = Bot.Connection.CreateCommand();
            cmd.CommandText = statement;
            
            cmd.ExecuteNonQuery();
        }
    }
}