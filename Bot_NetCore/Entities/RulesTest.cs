using System;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public class RulesTest
    {
        /// <summary>
        ///     Check if user with the specified ID must be tested for rules knowledge. 
        /// </summary>
        public static bool IsToBeTested(ulong memberId)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "SELECT member_id FROM rules_test_users WHERE member_id = @member";
                    cmd.Parameters.AddWithValue("@member", memberId);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    if (reader.Read()) return true;
                    else return false;
                }
            }
        }

        /// <summary>
        ///     Removes user with the specified ID from the testing list
        /// </summary>
        public static void RemoveUserFromTesting(ulong memberId)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "DELETE FROM rules_test_users WHERE member_id = @member";
                    cmd.Parameters.AddWithValue("@member", memberId);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}