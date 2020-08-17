using System;

namespace Bot_NetCore.Entities
{
    public class BannedUser
    {
        public BannedUser(ulong id, DateTime unbanDateTime, DateTime banDateTime, ulong moderator, string reason,
            string banId)
        {
            Id = id;
            UnbanDateTime = unbanDateTime;
            BanDateTime = banDateTime;
            Moderator = moderator;
            Reason = reason;
            BanId = banId;

            BanList.BannedMembers[Id] = this;
        }

        public ulong Id { get; }
        public DateTime UnbanDateTime { get; }
        public DateTime BanDateTime { get; }
        public ulong Moderator { get; }
        public string Reason { get; }
        public string BanId { get; }

        public void Unban()
        {
            BanList.BannedMembers.Remove(Id);
        }
    }
}
