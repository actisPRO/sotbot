using System;

namespace SeaOfThieves.Entities
{
    public class Warn
    {
        public ulong Moderator;
        public DateTime Date;
        public string Reason;

        internal Warn(ulong moderator, DateTime date, string reason)
        {
            Moderator = moderator;
            Date = date;
            Reason = reason;
        }
    }
}