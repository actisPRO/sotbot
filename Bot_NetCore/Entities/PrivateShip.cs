using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public class PrivateShip
    {
        private string _name;
        private ulong _channel;

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

        private PrivateShip(string name, ulong channel)
        {
            _name = name;
            _channel = channel;
        }

        public static PrivateShip Create(string name)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"INSERT INTO INSERT INTO private_ship(ship_name, ship_channel) VALUES (@name, 0)";
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Connection = connection;
                    cmd.Connection.Open();
                    
                    cmd.ExecuteNonQuery();
                    
                    return new PrivateShip(name, 0);
                }
            }
        }
    }
}