using System;

namespace ModeratorAPI
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