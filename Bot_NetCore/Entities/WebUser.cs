using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using Org.BouncyCastle.Cms;

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
        private string _lastXbox;
        private List<string> _xboxes;
        private string _lastIp;
        private List<string> _ips;
        private string _accessToken;
        private string _refreshToken;
        private DateTime _accessTokenExpiration;

        private WebUser(ulong userId, DateTime registeredOn, DateTime lastLogin, string username, string avatarUrl, string lastXbox, string lastIp, string accessToken, string refreshToken, DateTime accessTokenExpiration, List<string> ips, List<string> xboxes)
        {
            _userId = userId;
            _registeredOn = registeredOn;
            _lastLogin = lastLogin;
            _username = username;
            _avatarUrl = avatarUrl;
            _lastXbox = lastXbox;
            _lastIp = lastIp;
            _accessToken = accessToken;
            _refreshToken = refreshToken;
            _accessTokenExpiration = accessTokenExpiration;
            _ips = ips;
            _xboxes = xboxes;
        }

        /// <summary>
        /// Gets a user from the 'users' table with the specified Discord ID
        /// </summary>
        /// <param name="discordId">Discord ID of the user</param>
        /// <returns>WebUser entity or null if there is no user with the specified Discord ID</returns>
        public static WebUser GetByDiscordId(ulong discordId)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "SELECT * FROM users WHERE userid = @discordId;";
                    cmd.Parameters.AddWithValue("@discordId", discordId);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        var userid = reader.GetUInt64("userid");
                        var registeredOn = reader.GetDateTime("registered_on");
                        var lastLogin = reader.GetDateTime("last_login");
                        var username = reader.GetString("username");
                        var avatarUrl = reader.GetString("avatarurl");
                        var xbox = reader.GetString("xbox");
                        var lastIp = reader.GetString("ip");
                        var accessToken = reader.GetString("access_token");
                        var refreshToken = reader.GetString("refresh_token");
                        var accessTokenExpiration = reader.GetDateTime("access_token_expiration");

                        var ips = GetIpsByUid(discordId);
                        if (!ips.Contains(lastIp)) ips.Add(lastIp);
                        var xboxes = GetXboxesByUid(discordId);
                        if (!xboxes.Contains(xbox)) xboxes.Add(xbox);
                        
                        return new WebUser(userid, registeredOn, lastLogin, username, avatarUrl, xbox, lastIp, 
                            accessToken, refreshToken, accessTokenExpiration, ips, xboxes);
                    }
                }
            }

            return null;
        }

        public static List<WebUser> GetAll()
        {
            var result = new List<WebUser>();
            
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "SELECT * FROM users;";
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var userid = reader.GetUInt64("userid");
                        var registeredOn = reader.GetDateTime("registered_on");
                        var lastLogin = reader.GetDateTime("last_login");
                        var username = reader.GetString("username");
                        var avatarUrl = reader.GetString("avatarurl");
                        var xbox = reader.GetString("xbox");
                        var lastIp = reader.GetString("ip");
                        var accessToken = reader.GetString("access_token");
                        var refreshToken = reader.GetString("refresh_token");
                        var accessTokenExpiration = reader.GetDateTime("access_token_expiration");

                        var ips = GetIpsByUid(userid);
                        var xboxes = GetXboxesByUid(userid);
                        
                        result.Add(new WebUser(userid, registeredOn, lastLogin, username, avatarUrl, xbox, lastIp, 
                            accessToken, refreshToken, accessTokenExpiration, ips, xboxes));
                    }
                }
            }

            return result;
        }

        public static List<string> GetIpsByUid(ulong userid)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "SELECT ip FROM ips WHERE userid = @userid";
                    cmd.Parameters.AddWithValue("@userid", userid);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    var ips = new List<string>();
                    while (reader.Read())
                    {
                        ips.Add(reader.GetString("ip"));
                    }

                    return ips;
                }
            }
        }

        public static List<WebUser> GetUsersByIp(string ip)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "SELECT userid FROM ips WHERE ip = @ip";
                    cmd.Parameters.AddWithValue("@ip", ip);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    var result = new List<WebUser>();
                    while (reader.Read())
                    {
                        var userid = reader.GetUInt64("userid");
                        var user = WebUser.GetByDiscordId(userid);
                        result.Add(user);
                    }

                    return result;
                }
            }
        }
        
        public static List<WebUser> GetUsersByXbox(string xbox)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "SELECT userid FROM xboxes WHERE xbox = @xbox";
                    cmd.Parameters.AddWithValue("@xbox", xbox);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    var result = new List<WebUser>();
                    while (reader.Read())
                    {
                        var userid = reader.GetUInt64("userid");
                        var user = WebUser.GetByDiscordId(userid);
                        result.Add(user);
                    }

                    return result;
                }
            }
        }

        public static List<string> GetXboxesByUid(ulong userid)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = "SELECT xbox FROM xboxes WHERE userid = @userid";
                    cmd.Parameters.AddWithValue("@userid", userid);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    var xboxes = new List<string>();
                    while (reader.Read())
                    {
                        xboxes.Add(reader.GetString("xbox"));
                    }

                    return xboxes;
                }
            }
        }

        public ulong UserId => _userId;

        public DateTime RegisteredOn => _registeredOn;

        public DateTime LastLogin => _lastLogin;

        public string Username => _username;

        public string AvatarUrl => _avatarUrl;

        public string LastXbox => _lastXbox;

        public string LastIp => _lastIp;

        public string AccessToken => _accessToken;

        public string RefreshToken => _refreshToken;

        public DateTime AccessTokenExpiration => _accessTokenExpiration;

        public List<string> Ips => _ips;

        public List<string> Xboxes => _xboxes;
    }
}