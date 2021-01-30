using System;
using System.Collections.Generic;
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
        ///     Creates a new ship member and adds it to the database. Use PrivateShip.AddMember() to avoid errors (eg. incorrect ship name).
        /// </summary>
        public static PrivateShipMember Create(string ship, ulong memberId, PrivateShipMemberRole role, bool status)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "INSERT INTO private_ship_members(ship_name, member_id, member_type, " +
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
        ///     Gets members of the specified ship. Use PrivateShip.GetMembers() to avoid errors.
        /// </summary>
        /// <returns>Members of the specified ship</returns>
        public static List<PrivateShipMember> GetShipMembers(string ship)
        {
            var result = new List<PrivateShipMember>();
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "SELECT * FROM private_ship_members WHERE ship_name = @ship";
                    cmd.Parameters.AddWithValue("@ship", ship);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        result.Add(new PrivateShipMember(ship, reader.GetUInt64(1),
                            StringEnumToRoleEnum(reader.GetString(2)), reader.GetString(3) == "Active" ? true : false));
                    }

                    return result;
                }
            }
        }

        /// <summary>
        ///     Removes the specified member from the specified ship.
        /// </summary>
        public static void Delete(string ship, ulong member)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"DELETE FROM private_ship_members WHERE ship_name = @ship AND member_id = @member";
                    cmd.Parameters.AddWithValue("@ship", ship);
                    cmd.Parameters.AddWithValue("@member", member);
                    cmd.Connection = connection;
                    cmd.Connection.Open();
                    
                    cmd.ExecuteNonQuery();
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

        /// <summary>
        ///     Converts an enum string to PrivateShipMemberRole.
        /// </summary>
        /// <exception cref="InvalidCastException">Invalid enum string</exception>
        private static PrivateShipMemberRole StringEnumToRoleEnum(string role)
        {
            switch (role)
            {
                default:
                    throw new InvalidCastException(role + "is an unknown enum value.");
                case "Member":
                    return PrivateShipMemberRole.Member;
                case "Officer":
                    return PrivateShipMemberRole.Officer;
                case "Captain":
                    return PrivateShipMemberRole.Captain;
            }
        }
    }

    /// <summary>
    ///     Member - basic permissions,
    ///     Officer - not implemented, permissions to invite and kick members
    ///     Captain - owner. Full permissions.
    /// </summary>
    public enum PrivateShipMemberRole
    {
        Member,
        Officer,
        Captain
    }
}