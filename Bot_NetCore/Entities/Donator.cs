using System;
using System.Collections.Generic;

namespace Bot_NetCore.Entities
{
    public class Donator
    {
        public static Dictionary<ulong, Donator> DonatorList = new Dictionary<ulong, Donator>();
            
        public readonly ulong Member;

        private int _balance;
        private int _privateRole;
        private List<ulong> _friends;
        private DateTime _date;
        private bool _hidden;

        public int Balance
        {
            get => _balance;
            set
            {
                _balance = value;
                DonatorList[Member]._balance = value;
            }
        }

        public int PrivateRole
        {
            get => _privateRole;
            set
            {
                _privateRole = value;
                DonatorList[Member]._privateRole = value;
            }
        }

        public DateTime Date
        {
            get => _date;
            set
            {
                _date = value;
                DonatorList[Member]._date = value;
            }
        }

        public List<ulong> Friends
        {
            get => _friends;
            set
            {
                _friends = value;
                DonatorList[Member]._friends = value;
            }
        }

        public bool Hidden
        {
            get => _hidden;
            set
            {
                _hidden = value;
                DonatorList[Member]._hidden = value;
            }
        }
    }
}