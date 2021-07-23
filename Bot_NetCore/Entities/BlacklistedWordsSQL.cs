using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public static class BlacklistedWordsSQL
    {
        private static readonly Dictionary<ulong, string> _words = new Dictionary<ulong, string>();

        public static Dictionary<ulong, string> WordsList
        {
            get
            {
                if (_words.Count == 0)
                    return Update();
                return _words;
            }
        }

        public static List<string> Words
        {
            get
            {
                if (_words.Count == 0)
                    return Update().Values.ToList();
                return _words.Values.ToList();
            }
        }

        public static Dictionary<ulong, string> Update()
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand();
            var statement = "SELECT * FROM blacklistedwords;";

            cmd.CommandText = statement;
            cmd.Connection = connection;
            cmd.Connection.Open();

            var reader = cmd.ExecuteReader();

            _words.Clear();
            while (reader.Read())
            {
                _words.Add(reader.GetUInt64("id"), reader.GetString("word"));
            }

            return _words;
        }

        public static bool Add(string word)
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand();
            var statement =
                "INSERT INTO blacklistedwords(word) VALUES (@word);";

            cmd.Parameters.AddWithValue("@word", word);

            cmd.CommandText = statement;
            cmd.Connection = connection;
            cmd.Connection.Open();

            bool result = cmd.ExecuteNonQuery() > 0;

            Update();
            return result;
        }

        public static bool Remove(ulong id)
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand();
            var statement = "DELETE FROM blacklistedwords WHERE id = @id;";

            cmd.Parameters.AddWithValue("@id", id);

            cmd.CommandText = statement;
            cmd.Connection = connection;
            cmd.Connection.Open();

            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
