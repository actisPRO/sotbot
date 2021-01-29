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

        /// <summary>
        ///     Creates a new private ship and adds it to the database.
        /// </summary>
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
                                            s.ship_name, s.ship_channel
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
                        return new PrivateShip(reader.GetString(0), reader.GetUInt64(1));
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
    }
}