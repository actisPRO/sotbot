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
        private ReportType _reportType;

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

        public static List<ReportSQL> GetForUser(ulong user)
        {
            var reports = new List<ReportSQL>();
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"SELECT * FROM reports WHERE id='{id}';";
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader(); 
                    while (reader.Read())
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
        FleetPurge
    }
}