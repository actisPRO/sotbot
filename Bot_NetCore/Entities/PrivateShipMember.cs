using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public class PrivateShipMember
    {
        public readonly string Ship;
        public readonly ulong MemberId;

        private PrivateShipMemberRole _role;
        private bool _status;

        public PrivateShipMemberRole Role
        {
            get => _role;
            set
            {
                // mysql logic here
                _role = value;
            }
        }

        public bool Status
        {
            get => _status;
            set
            {
                // mysql logic here
                _status = value;
            }
        }

        private PrivateShipMember(string ship, ulong memberId, PrivateShipMemberRole role, bool status)
        {
            Ship = ship;
            MemberId = memberId;
            Role = role;
            Status = status;
        }

        /// <summary>
        /// Creates a new ship member and adds it to the database. Please use PrivateShip.AddMember() to avoid errors (eg. incorrect ship name).
        /// </summary>
        public static PrivateShipMember Create(string ship, ulong memberId, PrivateShipMemberRole role, bool status)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "INSERT INTO INSERT INTO private_ship_members(ship_name, member_id, member_type, " +
                                      "member_status) VALUES (@ship_name, @member_id, @member_type, @member_status)";
                    cmd.Parameters.AddWithValue("@ship_name", ship);
                    cmd.Parameters.AddWithValue("@member_id", memberId);
                    cmd.Parameters.AddWithValue("@member_type", RoleEnumToString(role));
                    cmd.Parameters.AddWithValue("@member_status", status ? "Active" : "Invited");
                    
                    cmd.Connection = connection;
                    cmd.Connection.Open();
                    
                    cmd.ExecuteNonQuery();
                    
                    return new PrivateShipMember(ship, memberId, role, status);
                }
            }
        }

        /// <summary>
        ///     Converts PrivateShipMemberRole to a correct database enum (field member_type) string
        /// </summary>
        private static string RoleEnumToString(PrivateShipMemberRole role)
        {
            switch (role)
            {
                case PrivateShipMemberRole.Member:
                    return "Member";
                case PrivateShipMemberRole.Officer:
                    return "Officer";
                case PrivateShipMemberRole.Captain:
                    return "Captain";
            }

            return "Member";
        }
    }

    public enum PrivateShipMemberRole
    {
        Member,
        Officer,
        Captain
    }
}