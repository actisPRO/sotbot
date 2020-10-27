using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public class ReportSQL
    {
        public readonly string Id;

        private ulong _user;
        private ulong _moderator;
        private string _reason;
        private DateTime _reportStart;
        private DateTime _reportEnd;
        private TimeSpan _reportDuration;
        private ReportType _reportType;

        public ulong User
        {
            get => _user;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE reports SET userid = '{value}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        cmd.ExecuteNonQuery();

                        _user = value;
                    }
                }
            }
        }

        public ulong Moderator
        {
            get => _moderator;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE reports SET moderator = '{value}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        cmd.ExecuteNonQuery();

                        _moderator = value;
                    }
                }
            }
        }

        public string Reason
        {
            get => _reason;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE reports SET reason = '{value}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        cmd.ExecuteNonQuery();

                        _reason = value;
                    }
                }
            }
        }

        public DateTime ReportStart
        {
            get => _reportStart;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE reports SET report_start = '{value:yyyy-MM-dd HH:mm:ss}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        cmd.ExecuteNonQuery();

                        _reportStart = value;
                    }
                }
            }
        }

        public DateTime ReportEnd
        {
            get => _reportEnd;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var statement = $"UPDATE reports SET report_end = '{value:yyyy-MM-dd HH:mm:ss}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        cmd.ExecuteNonQuery();

                        _reportEnd = value;
                    }
                }
            }
        }

        public TimeSpan ReportDuration
        {
            get => _reportEnd - _reportStart;
            private set => _reportDuration = value;
        }

        public ReportType ReportType
        {
            get => _reportType;
            set
            {
                using (var connection = new MySqlConnection(Bot.ConnectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        var type = value switch
                        {
                            ReportType.Mute => "mute",
                            ReportType.VoiceMute => "voicemute",
                            ReportType.FleetPurge => "fleetpurge",
                            ReportType.CodexPurge => "codexpurge",
                            _ => ""
                        };
                        
                        var statement = $"UPDATE reports SET report_type = '{type}' WHERE id = '{Id}'";
                        cmd.CommandText = statement;
                        cmd.Connection = connection;
                        cmd.Connection.Open();

                        cmd.ExecuteNonQuery();

                        _reportType = value;
                    }
                }
            }
        }

        private ReportSQL(string id, ulong user, ulong moderator, string reason, DateTime reportStart,
            DateTime reportEnd, ReportType reportType)
        {
            Id = id;

            _user = user;
            _moderator = moderator;
            _reason = reason;
            _reportStart = reportStart;
            _reportEnd = reportEnd;
            _reportType = reportType;
        }

        public static ReportSQL Create(string id, ulong user, ulong moderator, string reason, DateTime reportStart,
            DateTime reportEnd, ReportType reportType)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    var type = reportType switch
                    {
                        ReportType.Mute => "mute",
                        ReportType.VoiceMute => "voicemute",
                        ReportType.FleetPurge => "fleetpurge",
                        ReportType.CodexPurge => "codexpurge",
                        _ => ""
                    };
                    var statement =
                        $"INSERT INTO reports(id, userid, moderator, reason, report_start, report_end, report_type) VALUES ('{id}', '{user}', '{moderator}', '{reason}', " +
                        $"'{reportStart:yyyy-MM-dd HH:mm:ss}', '{reportEnd:yyyy-MM-dd HH:mm:ss}', '{type}');";
                    
                    cmd.CommandText = statement;
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    cmd.ExecuteNonQuery();
                    
                    return new ReportSQL(id, user, moderator, reason, reportStart, reportEnd, reportType);
                }
            }
        }

        public static void Delete(string id)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                   
                    var statement = $"DELETE FROM reports WHERE id = '{id}'";
                    cmd.CommandText = statement;
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static ReportSQL Get(string id)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"SELECT * FROM reports WHERE id='{id}';";
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        return null;
                    }
                    else
                    {
                        var user = reader.GetUInt64("userid");
                        var moderator = reader.GetUInt64("moderator");
                        var reason = reader.GetString("reason");
                        var reportStart = reader.GetDateTime("report_start");
                        var reportEnd = reader.GetDateTime("report_end");
                        var type = reader.GetString("report_type");
                        var reportType = type switch
                        {
                            "mute" => ReportType.Mute,
                            "voicemute" => ReportType.VoiceMute,
                            "fleetpurge" => ReportType.FleetPurge,
                            "codexpurge" => ReportType.CodexPurge,
                            _ => throw new Exception("Unable to get ReportType")
                        };
                        
                        return new ReportSQL(id, user, moderator, reason, reportStart, reportEnd, reportType);
                    }
                }
            }
        }

        public static List<ReportSQL> GetForUser(ulong userid, ReportType filter = ReportType.All)
        {
            var reports = new List<ReportSQL>();
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    if (filter == ReportType.All)
                    {
                        cmd.CommandText = $"SELECT * FROM reports WHERE userid='{userid}';";
                    }
                    else
                    {
                        var filterStr = filter switch
                        {
                            ReportType.Mute => "mute",
                            ReportType.CodexPurge => "codexpurge",
                            ReportType.FleetPurge => "fleetpurge",
                            ReportType.VoiceMute => "voicemute",
                            _ => throw new Exception("That exception won't be thrown. Well, I hope so.")
                        };
                        cmd.CommandText = $"SELECT * FROM reports WHERE userid='{userid}' AND report_type='{filterStr}';";
                    }
                    
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader(); 
                    while (reader.Read())
                    {
                        var id = reader.GetString("id");
                        var user = reader.GetUInt64("userid");
                        var moderator = reader.GetUInt64("moderator");
                        var reason = reader.GetString("reason");
                        var reportStart = reader.GetDateTime("report_start");
                        var reportEnd = reader.GetDateTime("report_end");
                        var type = reader.GetString("report_type");
                        var reportType = type switch
                        {
                            "mute" => ReportType.Mute,
                            "voicemute" => ReportType.VoiceMute,
                            "fleetpurge" => ReportType.FleetPurge,
                            "codexpurge" => ReportType.CodexPurge,
                            _ => throw new Exception("Unable to get ReportType")
                        };
                        
                        reports.Add(new ReportSQL(id, user, moderator, reason, reportStart, reportEnd, reportType));
                    }

                    return reports;
                }
            }
        }

        public static List<ReportSQL> GetExpiredReports()
        {
            var reports = new List<ReportSQL>();
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    //Проверяет только самое последнее предупреждение на 
                    cmd.CommandText = $"SELECT * FROM reports " +
                        $"INNER JOIN(SELECT userid, report_type, MAX(report_start) as max_report_start FROM reports GROUP BY userid, report_type) groupedR " +
                        $"ON reports.userid = groupedR.userid AND reports.report_type = groupedR.report_type AND report_start = max_report_start " +
                        $"WHERE report_end <= '{DateTime.Now:yyyy-MM-dd HH:mm:ss}'";
                    
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader(); 
                    while (reader.Read())
                    {
                        var id = reader.GetString("id");
                        var user = reader.GetUInt64("userid");
                        var moderator = reader.GetUInt64("moderator");
                        var reason = reader.GetString("reason");
                        var reportStart = reader.GetDateTime("report_start");
                        var reportEnd = reader.GetDateTime("report_end");
                        var type = reader.GetString("report_type");
                        var reportType = type switch
                        {
                            "mute" => ReportType.Mute,
                            "voicemute" => ReportType.VoiceMute,
                            "fleetpurge" => ReportType.FleetPurge,
                            "codexpurge" => ReportType.CodexPurge,
                            _ => throw new Exception("Unable to get ReportType")
                        };
                        
                        reports.Add(new ReportSQL(id, user, moderator, reason, reportStart, reportEnd, reportType));
                    }

                    return reports;
                }
            }
        }
    }

    public enum ReportType
    {
        Mute,
        VoiceMute,
        CodexPurge,
        FleetPurge,
        All
    }
}