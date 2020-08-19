using System;
using System.Collections.Generic;

namespace Bot_NetCore.Entities
{
    [Obsolete]
    public class DonatorLegacy
    {
        public DonatorLegacy(ulong member, ulong colorRole, DateTime date, double balance = 0)
        {
            Member = member;
            Balance = balance;
            ColorRole = colorRole;
            Friends = new List<ulong>();
            Hidden = false;
            Date = date;

            DonatorList.Donators[Member] = this;
        }

        public ulong Member { get; }
        public double Balance { get; private set; }
        public ulong ColorRole { get; private set; }
        public List<ulong> Friends { get; }
        public bool Hidden { get; private set; }
        public DateTime Date { get; set; }

        public void AddFriend(ulong friend)
        {
            Friends.Add(friend);

            DonatorList.Donators[Member] = this;
        }

        public void RemoveFriend(ulong friend)
        {
            Friends.Remove(friend);

            DonatorList.Donators[Member] = this;
        }

        public void SetBalance(double newBalance)
        {
            Balance = newBalance;

            DonatorList.Donators[Member] = this;
        }

        public void SetRole(ulong colorRole)
        {
            ColorRole = colorRole;

            DonatorList.Donators[Member] = this;
        }

        public void UpdateHidden(bool hidden)
        {
            Hidden = hidden;

            DonatorList.Donators[Member] = this;
        }

        public void Remove()
        {
            DonatorList.Donators.Remove(Member);
        }
    }
}
