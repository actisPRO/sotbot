using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public class PrivateShip
    {
        private string _name;
        private ulong _channel;
        private ulong _requestMessage;
        private DateTime _createdAt;
        private DateTime _lastUsed;

        public string Name
        {
            get => _name;
            set
            {
                // mysql logic here
                _name = value;
            }
        }

        public ulong Channel
        {
            get => _channel;
            set
            {
                // mysql logic here
                _channel = value;
            }
        }

        public ulong RequestMessage
        {
            get => _requestMessage;
            set
            {
                // mysql logic here
                _requestMessage = value;
            }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set
            {
                // mysql
                _createdAt = value;
            }
        }

        public DateTime LastUsed
        {
            get => _lastUsed;
            set
            {
                // mysql
                _lastUsed = value;
            }
        }

        private PrivateShip(string name, ulong channel, DateTime createdAt, DateTime lastUsed, ulong requestMessage)
        {
            _name = name;
            _channel = channel;
            _createdAt = createdAt;
            _lastUsed = lastUsed;
        }

        /// <summary>
        ///     Creates a new private ship and adds it to the database.
        /// </summary>
        public static PrivateShip Create(string name, DateTime createdAt, ulong requestMessage)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"INSERT INTO private_ship(ship_name, ship_channel, request_message) VALUES (@name, 0, @message)";
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@message", requestMessage);
                    cmd.Connection = connection;
                    cmd.Connection.Open();
                    
                    cmd.ExecuteNonQuery();
                    
                    return new PrivateShip(name, 0, createdAt, createdAt, requestMessage);
                }
            }
        }
        
        /// <summary>
        ///     Returns a ship where the specified member is a captain or null if there is no ship, owned by the member.
        /// </summary>
        /// <returns>Owned ship or null</returns>
        public static PrivateShip GetOwnedShip(ulong memberId)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = @"SELECT
                                            s.ship_name, s.ship_channel, s.request_message
                                        FROM
                                            private_ship s
                                            JOIN private_ship_members psm ON s.ship_name = psm.ship_name
                                        WHERE psm.member_id = @memberId AND psm.member_type = 'Captain';
                                        ";
                    cmd.Parameters.AddWithValue("@memberId", memberId);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                        return null;
                    else
                        return new PrivateShip(reader.GetString(0), 
                            reader.GetUInt64(1), reader.GetDateTime(2), reader.GetDateTime(3),
                            reader.GetUInt64(4));
                }
            }
        }

        /// <summary>
        ///     Returns a ship with the specified name or null, if nothing is found.
        /// </summary>
        public static PrivateShip Get(string name)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "SELECT * FROM private_ship WHERE ship_name = @name";
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                        return null;
                    else
                        return new PrivateShip(reader.GetString(0), 
                            reader.GetUInt64(1), reader.GetDateTime(2), reader.GetDateTime(3),
                            reader.GetUInt64(4));
                }
            }
        }

        /// <summary>
        ///     Returns a ship with the specified request message ID or null, if nothing is found.
        /// </summary>
        public static PrivateShip GetByRequest(ulong request)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "SELECT * FROM private_ship WHERE request_message = @request";
                    cmd.Parameters.AddWithValue("@request", request);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                        return null;
                    else
                        return new PrivateShip(reader.GetString(0), 
                            reader.GetUInt64(1), reader.GetDateTime(2), reader.GetDateTime(3),
                            reader.GetUInt64(4));
                }
            }
        }

        /// <summary>
        ///     Deletes a ship with the specified name.
        /// </summary>
        public static void Delete(string name)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"DELETE FROM private_ship WHERE name = @name";
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Connection = connection;
                    cmd.Connection.Open();
                    
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        ///     Adds a new member to a private ship and saves him to the database.
        /// </summary>
        /// <param name="id">ID of a member</param>
        /// <param name="role">Type of a member</param>
        /// <param name="status">True will provide access to the private ship commands</param>
        /// <returns></returns>
        public PrivateShipMember AddMember(ulong id, PrivateShipMemberRole role, bool status)
        {
            return PrivateShipMember.Create(_name, id, role, status);
        }

        /// <summary>
        ///     Returns a list of members of the current ship
        /// </summary>
        public List<PrivateShipMember> GetMembers()
        {
            return PrivateShipMember.GetShipMembers(_name);
        }
    }
}