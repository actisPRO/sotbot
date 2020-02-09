using System;

namespace SeaOfThieves.Entities
{
    public class Warn
    {
        public ulong Moderator;
        public DateTime Date;
        public string Reason;
        public string Id;
        public ulong LogMessage;

        internal Warn(ulong moderator, DateTime date, string reason, string id, ulong logMessage)
        {
            Moderator = moderator;
            Date = date;
            Reason = reason;
            Id = id;
            LogMessage = logMessage;
        }
    }
}