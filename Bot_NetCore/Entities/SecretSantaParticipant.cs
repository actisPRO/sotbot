using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public class SecretSantaParticipant
    {
        private ulong _id;
        private string _address;
        private ulong _sendingTo;

        private SecretSantaParticipant(ulong id, string address, ulong sendingTo)
        {
            _id = id;
            _address = address;
            _sendingTo = sendingTo;
        }

        public static SecretSantaParticipant Create(ulong id, string address, ulong sendingTo = 0)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "INSERT INTO secret_santa(id, address, sending_to) VALUES(@id, @address, @sending_to);";
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@address", address);
                    cmd.Parameters.AddWithValue("@sending_to", sendingTo);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    cmd.ExecuteNonQuery();

                    return new SecretSantaParticipant(id, address, sendingTo);
                }
            }
        }

        public static SecretSantaParticipant Get(ulong id)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "SELECT * FROM secret_santa WHERE id = @id;";
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        return new SecretSantaParticipant(id, reader.GetString("address"), reader.GetUInt64("sending_to"));
                    }

                    return null;
                }
            } 
        }

        public static List<SecretSantaParticipant> GetAll()
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    var result = new List<SecretSantaParticipant>();
                    
                    cmd.CommandText = "SELECT * FROM secret_santa";
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        result.Add(new SecretSantaParticipant(reader.GetUInt64("id"), reader.GetString("address"),
                            reader.GetUInt64("sending_to")));
                    }

                    return result;
                }
            } 
        }

        public static void Delete(ulong id)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "DELETE FROM secret_santa WHERE id = @id;";
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public ulong Id => _id;

        public string Address
        {
            get => _address;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        cmd.CommandText = "UPDATE secret_santa SET address = @address WHERE id = @id;";
                        cmd.Parameters.AddWithValue("@id", _id);
                        cmd.Parameters.AddWithValue("@address", value);
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        cmd.ExecuteNonQuery();
                        _address = value;
                    }
                }
            }
        }

        public ulong SendingTo
        {
            get => _sendingTo;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        cmd.CommandText = "UPDATE secret_santa SET sending_to = @sending_to WHERE id = @id;";
                        cmd.Parameters.AddWithValue("@id", _id);
                        cmd.Parameters.AddWithValue("@sending_to", value);
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        cmd.ExecuteNonQuery();
                        _sendingTo = value;
                    }
                }
            }
        }
    }
}