using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    /// <summary>
    /// This class represents an entity from the 'users' table
    /// </summary>
    public class WebUser
    {
        private ulong _userId;
        private DateTime _registeredOn;
        private DateTime _lastLogin;
        private string _username;
        private string _avatarUrl;
        private string _xbox;
        private string _ip;
        private string _accessToken;
        private string _refreshToken;
        private DateTime _accessTokenExpiration;

        private WebUser(ulong userId, DateTime registeredOn, DateTime lastLogin, string username, string avatarUrl, string xbox, string ip, string accessToken, string refreshToken, DateTime accessTokenExpiration)
        {
            _userId = userId;
            _registeredOn = registeredOn;
            _lastLogin = lastLogin;
            _username = username;
            _avatarUrl = avatarUrl;
            _xbox = xbox;
            _ip = ip;
            _accessToken = accessToken;
            _refreshToken = refreshToken;
            _accessTokenExpiration = accessTokenExpiration;
        }

        /// <summary>
        /// Gets a user from the 'users' table with the specified Discord ID
        /// </summary>
        /// <param name="discordId">Discord ID of the user</param>
        /// <returns>WebUser entity or null if there is no user with the specified Discord ID</returns>
        public WebUser GetByDiscordId(ulong discordId)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"SELECT * FROM users WHERE userid = @discordId;";
                    cmd.Parameters.AddWithValue("@discordId", discordId);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        return new WebUser(reader.GetUInt64("userid"), 
                            reader.GetDateTime("registered_on"),
                            reader.GetDateTime("last_login"),
                            reader.GetString("username"),
                            reader.GetString("avatarurl"),
                            reader.GetString("xbox"),
                            reader.GetString("ip"),
                            reader.GetString("access_token"),
                            reader.GetString("refresh_token"),
                            reader.GetDateTime("access_token_expiration"));
                    }
                }
            }

            return null;
        }

        public List<WebUser> GetAll()
        {
            var result = new List<WebUser>();
            
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"SELECT * FROM users;";
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        result.Add(new WebUser(reader.GetUInt64("userid"), 
                            reader.GetDateTime("registered_on"),
                            reader.GetDateTime("last_login"),
                            reader.GetString("username"),
                            reader.GetString("avatarurl"),
                            reader.GetString("xbox"),
                            reader.GetString("ip"),
                            reader.GetString("access_token"),
                            reader.GetString("refresh_token"),
                            reader.GetDateTime("access_token_expiration")));
                    }
                }
            }

            return result;
        }

        public ulong UserId => _userId;

        public DateTime RegisteredOn => _registeredOn;

        public DateTime LastLogin => _lastLogin;

        public string Username => _username;

        public string AvatarUrl => _avatarUrl;

        public string Xbox => _xbox;

        public string Ip => _ip;

        public string AccessToken => _accessToken;

        public string RefreshToken => _refreshToken;

        public DateTime AccessTokenExpiration => _accessTokenExpiration;
    }
}