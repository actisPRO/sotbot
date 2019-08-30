using System;
using SeaOfThieves.Misc;

namespace SeaOfThieves.Entities
{
    public class BannedUser
    {
        public ulong Id { get; }
        public DateTime UnbanDateTime { get; }
        public DateTime BanDateTime { get; }
        public ulong Moderator { get; }
        public string Reason { get; }
        public string BanId { get; }

        public BannedUser(ulong id, DateTime unbanDateTime, DateTime banDateTime, ulong moderator, string reason, string banId)
        {
            Id = id;
            UnbanDateTime = unbanDateTime;
            BanDateTime = banDateTime;
            Moderator = moderator;
            Reason = reason;
            BanId = banId;

            BanList.BannedMembers[Id] = this;
        }

        public void Unban()
        {
            BanList.BannedMembers.Remove(Id);
        }
    }
}