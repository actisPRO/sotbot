using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeaOfThieves.Entities
{
    public class Inviter
    {
        public Inviter(ulong id)
        {
            InviterId = id;
            Referrals = new List<ulong>();

            InviterList.Inviters[InviterId] = this;
        }

        public ulong InviterId { get; }
        public List<ulong> Referrals { get; }

        public static Inviter Create(ulong inviterId)
        {
            InviterList.Update(new Inviter(inviterId));
            return new Inviter(inviterId);
        }

        public void AddReferral(ulong friend)
        {
            Referrals.Add(friend);

            InviterList.Inviters[InviterId] = this;
        }

        public void RemoveReferral(ulong friend)
        {
            Referrals.Remove(friend);

            InviterList.Inviters[InviterId] = this;
        }

        public void Remove()
        {
            InviterList.Inviters.Remove(InviterId);
        }
    }
}
