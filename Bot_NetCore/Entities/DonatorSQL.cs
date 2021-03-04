using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public class DonatorSQL
    {
        public readonly ulong UserId;

        private DateTime _date;
        private int _balance;
        private DateTime _subEnd;
        private ulong _privateRole;
        private bool _isHidden;

        private List<ulong> _friends = new List<ulong>();

        public DateTime Date {
            get { return _date; }
            set 
            {
                if(value != _date)
                {
                    UpdateDbColumn("date", value);
                    _date = value;
                }
            }
        }

        public int Balance {
            get { return _balance; }
            set
            {
                if (value != _balance)
                {
                    UpdateDbColumn("balance", value, true);
                    _balance = value;
                }
            }
        }

        public DateTime subEnd {
            get { return _subEnd; }
            set
            {
                if (value != _subEnd)
                {
                    UpdateDbColumn("sub_end", value, true);
                    _subEnd = value;
                }
            }
        }

        public ulong PrivateRole {
            get { return _privateRole; }
            set
            {
                if (value != _privateRole)
                {
                    UpdateDbColumn("private_role", value);
                    _privateRole = value;
                }
            }
        }
        public bool IsHidden {
            get { return _isHidden; }
            set
            {
                if (value != _isHidden)
                {
                    UpdateDbColumn("is_hidden", value);
                    _isHidden = value;
                }
            }
        }

        public DonatorSQL(ulong userId, DateTime date, int balance, DateTime subEnd, ulong privateRole, bool isHidden = false)
        {
            UserId = userId;
            _date = date;
            _balance = balance;
            _subEnd = subEnd;
            _privateRole = privateRole;
            _isHidden = isHidden;
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

                _friends.Add(friendId);
                return true;
            }
            return false;
        }

        public bool RemoveFriend(ulong friendId)
        {
            if (_friends.Contains(friendId))
            {
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

                _friends.Remove(friendId);
                return true;
            }
            return false;
        }

        public bool IsSubscriber()
        {
            return _subEnd > DateTime.Now;
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

        private void UpdateDbColumn(string columnName, object value, bool updateDate = false)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using var cmd = new MySqlCommand();

                var statement = $"UPDATE donators SET {columnName} = @value WHERE id = @userId";

                //Обновляем время доната, в случае обновления баланса или подписки
                if (updateDate)
                {
                    statement = $"UPDATE donators SET {columnName} = @value, date = @date WHERE id = @userId";
                    cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    _date = DateTime.Now;
                }                    

                cmd.Parameters.AddWithValue("@value", value);
                cmd.Parameters.AddWithValue("@userId", UserId);

                cmd.CommandText = statement;
                cmd.Connection = connection;
                cmd.Connection.Open();

                cmd.ExecuteNonQuery();
            }
        }

        private static DonatorSQL GetOrCreate(ulong userId, DateTime date, int balance = 0, DateTime subEnd = new DateTime(), ulong privateRole = 0, bool isHidden = false)
        {
            var donator = GetById(userId);
            if (donator != null)
                return donator;

            using var connection = new MySqlConnection(Bot.ConnectionString);
            using (var cmd = new MySqlCommand())
            {
                cmd.CommandText = "INSERT INTO donators(user_id, date, balance, sub_end, private_role, is_hidden) " +
                    "VALUES(@userId, @date, @balance, @subEnd, @privateRole, @isHidden);";

                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@balance", balance);
                cmd.Parameters.AddWithValue("@subEnd", subEnd);
                cmd.Parameters.AddWithValue("@privateRole", privateRole);
                cmd.Parameters.AddWithValue("@isHidden", isHidden);

                cmd.Connection = connection;
                cmd.Connection.Open();
                var reader = cmd.ExecuteNonQuery();

                return new DonatorSQL(userId, date, balance, subEnd, privateRole, isHidden);
            }
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
                    reader.GetDateTime("date"),
                    reader.GetInt32("balance"),
                    reader.GetDateTime("sub_end"),
                    reader.GetUInt64("private_role"),
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
                    reader.GetDateTime("date"),
                    reader.GetInt32("balance"),
                    reader.GetDateTime("sub_end"),
                    reader.GetUInt64("private_role"),
                    reader.GetBoolean("is_hidden")));
            }

            return donators;
        }
    }
}