using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public class DonatorSQL
    {
        public readonly ulong UserId;

        public int Balance { get; set; }
        public ulong PrivateRole { get; set; }
        public DateTime Date { get; set; }
        public bool IsHidden { get; set; }

        private List<ulong> _friends = new List<ulong>();

        public DonatorSQL(ulong userId, int balance, ulong privateRole, DateTime date, bool isHidden = false)
        {
            UserId = userId;
            Balance = balance;
            PrivateRole = privateRole;
            Date = date;
            IsHidden = isHidden;
        }

        public List<ulong> GetFriends()
        {
            if (_friends.Count != 0)
                return _friends;

            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand
            {
                CommandText = "SELECT * FROM donator_friends WHERE user_id = @userId",
                Connection = connection
            };

            cmd.Parameters.AddWithValue("?userId", UserId);

            cmd.Connection.Open();

            var reader = cmd.ExecuteReader();

            _friends = new List<ulong>();
            while (reader.Read())
            {
                _friends.Add(reader.GetUInt64("friend_id"));
            }
            return _friends;
        }

        public bool AddFriend(ulong friendId)
        {
            if (!_friends.Contains(friendId))
            {
                _friends.Add(friendId);

                using var connection = new MySqlConnection(Bot.ConnectionString);
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "INSERT IGNORE INTO donator_friends(user_id, friend_id) VALUES(@UserId, @friendId);";

                    cmd.Parameters.AddWithValue("@UserId", UserId);
                    cmd.Parameters.AddWithValue("@friendId", friendId);

                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteNonQuery();
                }
                return true;
            }
            return false;
        }

        public bool RemoveFriend(ulong friendId)
        {
            if (_friends.Contains(friendId))
            {
                _friends.Remove(friendId);

                using var connection = new MySqlConnection(Bot.ConnectionString);
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "DELETE FROM donator_friends WHERE user_id = @UserId AND friend_id = @friendId;";

                    cmd.Parameters.AddWithValue("@UserId", UserId);
                    cmd.Parameters.AddWithValue("@friendId", friendId);

                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteNonQuery();
                }
                return true;
            }
            return false;
        }

        public DonatorSQL SaveAndUpdate()
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using (var cmd = new MySqlCommand())
            {
                cmd.CommandText = "INSERT INTO donators(user_id, balance, private_role, date, is_hidden) " +
                    "VALUES(@UserId, @Balance, @PrivateRole, @Date, @IsHidden)" +
                    "ON DUPLICATE KEY UPDATE balance = @Balance, private_role = @PrivateRole, is_hidden = @IsHidden;";

                cmd.Parameters.AddWithValue("@UserId", UserId);
                cmd.Parameters.AddWithValue("@Balance", Balance);
                cmd.Parameters.AddWithValue("@PrivateRole", PrivateRole);
                cmd.Parameters.AddWithValue("@Date", Date.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@IsHidden", IsHidden);

                cmd.Connection = connection;
                cmd.Connection.Open();

                var reader = cmd.ExecuteNonQuery();
            }

            return GetById(UserId);
        }

        public static void RemoveDonator(ulong userId)
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand
            {
                CommandText = "DELETE FROM donators WHERE user_id = @userId;",
                Connection = connection
            };

            cmd.Parameters.AddWithValue("?userId", userId);

            cmd.Connection.Open();

            var reader = cmd.ExecuteNonQuery();
        }

        public static DonatorSQL GetById(ulong userId)
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand
            {
                CommandText = $"SELECT * FROM donators WHERE user_id = @userId;",
                Connection = connection
            };

            cmd.Parameters.AddWithValue("@userId", userId);

            cmd.Connection.Open();

            var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new DonatorSQL(
                    reader.GetUInt64("user_id"),
                    reader.GetInt32("balance"),
                    reader.GetUInt64("private_role"),
                    reader.GetDateTime("date"),
                    reader.GetBoolean("is_hidden"));
            }
            return null;
        }

        public static List<DonatorSQL> GetAllDonators()
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand
            {
                CommandText = $"SELECT * FROM donators WHERE true;",
                Connection = connection
            };
            cmd.Connection.Open();

            var reader = cmd.ExecuteReader();

            List<DonatorSQL> donators = new List<DonatorSQL>();

            while (reader.Read())
            {
                donators.Add(new DonatorSQL(
                    reader.GetUInt64("user_id"),
                    reader.GetInt32("balance"),
                    reader.GetUInt64("private_role"),
                    reader.GetDateTime("date"),
                    reader.GetBoolean("is_hidden")));
            }

            return donators;
        }
    }
}