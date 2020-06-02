using SeaOfThieves.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bot_NetCore.Entities
{
    public class Inviter
    {
        public Inviter(ulong id, bool active = true)
        {
            InviterId = id;
            Active = active;
            Referrals = new Dictionary<ulong, Referral>();

            InviterList.Inviters[InviterId] = this;
        }

        public ulong InviterId { get; }
        public Dictionary<ulong, Referral> Referrals { get; }
        public bool Active { get; private set; }
        public int ActiveCount { get; private set; }
        public int CurrentMonthActiveCount { get; private set; }

        public static Inviter Create(ulong inviterId)
        {
            InviterList.Update(new Inviter(inviterId));

            return new Inviter(inviterId);
        }

        public void AddReferral(ulong referralId, bool state = true, DateTime? date = null)
        {
            if (Referrals.ContainsKey(referralId)) return;

            Referrals.Add(referralId, new Referral(referralId, state: state, date: date));

            InviterList.Inviters[InviterId] = this;

            UpdateActiveReferrals();
        }

        public void RemoveReferral(ulong referralId)
        {
            if (!Referrals.ContainsKey(referralId)) return;

            Referrals.Remove(referralId);

            InviterList.Inviters[InviterId] = this;

            UpdateActiveReferrals();
        }

        public void UpdateReferral(ulong referralId, bool state)
        {
            if (!Referrals.ContainsKey(referralId)) return;

            Referrals[referralId].Active = state;

            InviterList.Inviters[InviterId] = this;

            UpdateActiveReferrals();
        }

        private void UpdateActiveReferrals()
        {
            ActiveCount = Referrals.Where(x => x.Value.Active == true).ToList().Count;
            CurrentMonthActiveCount = Referrals.Where(x => x.Value.Active == true && x.Value.Date.Month == DateTime.UtcNow.Month).ToList().Count;
        }

        public void UpdateState(bool state)
        {
            Active = state;

            InviterList.Inviters[InviterId] = this;
        }

        public void Remove()
        {
            InviterList.Inviters.Remove(this.InviterId);
        }
    }

    public class Referral
    {
        public Referral(ulong id, bool state = true, DateTime? date = null)
        {
            Id = id;
            Active = state;
            Date = date ?? DateTime.UtcNow.Date;
        }

        public ulong Id { get; }
        public DateTime Date { get; }
        public bool Active { get; set; }
    }
}
