using System;

namespace Bot_NetCore.Entities
{
    [Obsolete]
    public class Warn
    {
        public DateTime Date;
        public string Id;
        public ulong LogMessage;
        public ulong Moderator;
        public string Reason;

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
