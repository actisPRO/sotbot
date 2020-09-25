using System;
using System.Collections.Generic;
using System.Linq;

namespace Bot_NetCore.Entities
{
    public class Inviter
    {
        public Inviter(ulong id, bool active = true, bool ignored = false)
        {
            InviterId = id;
            Active = active;
            Ignored = ignored;
            Referrals = new Dictionary<ulong, Referral>();

            UpdateActiveReferrals();

            InviterList.Inviters[InviterId] = this;
        }

        public ulong InviterId { get; }
        public Dictionary<ulong, Referral> Referrals { get; }
        public bool Active { get; private set; }
        public bool Ignored { get; private set; }
        public int ActiveCount { get; private set; }
        public int CurrentMonthActiveCount { get; private set; }
        public int LastMonthActiveCount { get; private set; }

        public static Inviter Create(ulong inviterId, bool isBot = false)
        {
            var inviter = new Inviter(inviterId, ignored: isBot);

            InviterList.Update(inviter);

            return inviter;
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
            DateTime date = DateTime.Now.Date;
            ActiveCount = Referrals.Where(x => x.Value.Active == true).ToList().Count;
            CurrentMonthActiveCount = Referrals.Where(x => x.Value.Active == true && x.Value.Date.Month == date.Month && x.Value.Date.Year == date.Year).ToList().Count;
            LastMonthActiveCount = Referrals.Where(x => x.Value.Active == true && x.Value.Date.Month == date.AddMonths(-1).Month && x.Value.Date.Year == date.AddMonths(-1).Year).ToList().Count;
        }

        public void UpdateState(bool state)
        {
            Active = state;

            InviterList.Inviters[InviterId] = this;
        }

        public void UpdateIgnored(bool ignored)
        {
            Ignored = ignored;

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
            Date = date ?? DateTime.Now.Date;
        }

        public ulong Id { get; }
        public DateTime Date { get; }
        public bool Active { get; set; }
    }
}
