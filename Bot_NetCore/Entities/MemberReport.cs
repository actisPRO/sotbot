using System;

namespace Bot_NetCore.Entities
{
    public class MemberReport
    {
        public MemberReport(ulong id, DateTime reportDateTime, TimeSpan reportDuration, ulong moderator, string reason)
        {
            Id = id;
            ReportDateTime = reportDateTime;
            ReportDuration = reportDuration;
            Moderator = moderator;
            Reason = reason;
        }

        public ulong Id { get; }
        public DateTime ReportDateTime { get; private set; }
        public TimeSpan ReportDuration { get; private set; }
        public ulong Moderator { get; private set; }
        public string Reason { get; private set; }

        public void UpdateReport(DateTime reportDateTime, TimeSpan reportDuration, ulong moderator, string reason)
        {
            ReportDateTime = reportDateTime;
            ReportDuration = reportDuration;
            Moderator = moderator;
            Reason = reason;
        }

        public bool Expired()
        {
            return (ReportDateTime.Add(ReportDuration) - DateTime.Now).TotalSeconds <= 0;
        }

        public DateTime getExpirationDateTime()
        {
            return ReportDateTime.Add(ReportDuration);
        }

        public TimeSpan getRemainingTime()
        {
            return ReportDateTime.Add(ReportDuration) - DateTime.Now;
        }
    }
}
