using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Bot_NetCore.Entities
{
    public class Donator
    {
        public static Dictionary<ulong, Donator> Donators = new Dictionary<ulong, Donator>();
            
        public readonly ulong Member;

        private int _balance;
        private ulong _privateRole;
        private List<ulong> _friends;
        private DateTime _date;
        private bool _hidden;

        public int Balance
        {
            get => _balance;
            set
            {
                _balance = value;
                Donators[Member]._balance = value;
            }
        }

        public ulong PrivateRole
        {
            get => _privateRole;
            set
            {
                _privateRole = value;
                Donators[Member]._privateRole = value;
            }
        }

        public DateTime Date
        {
            get => _date;
            set
            {
                _date = value;
                Donators[Member]._date = value;
            }
        }

        public List<ulong> Friends
        {
            get => _friends;
            set
            {
                _friends = value;
                Donators[Member]._friends = value;
            }
        }

        public bool Hidden
        {
            get => _hidden;
            set
            {
                _hidden = value;
                Donators[Member]._hidden = value;
            }
        }

        public Donator(ulong member, int balance, ulong privateRole, DateTime date, List<ulong> friends, bool hidden)
        {
            Member = member;

            _balance = balance;
            _privateRole = privateRole;
            _date = date;
            _friends = friends;
            _hidden = hidden;

            Donators[Member] = this;
        }

        public static void Save(string fileName)
        {
            var doc = new XDocument();
            var root = new XElement("Donators");

            foreach (var donator in Donators.Values)
            {
                var donatorEl = new XElement("Donator", new XAttribute("member", donator));
                
                donatorEl.Add(new XElement("Balance"), donator.Balance);
                donatorEl.Add(new XElement("Date"), donator.Date);
                if (donator.PrivateRole != 0) donatorEl.Add(new XElement("PrivateRole"), donator.PrivateRole);
                if (donator.Friends.Count != 0)
                {
                    var friendEl = new XElement("Friends");
                    foreach (var friend in donator.Friends)
                        friendEl.Add(new XElement("Friend"), friend);
                    donatorEl.Add(friendEl);
                }
                donatorEl.Add(new XElement("Hidden"), donator.Hidden);
            
                root.Add(donatorEl);
            }
            
            doc.Add(root);
            doc.Save(fileName);
        }
        
        public static void Read(string fileName)
        {
            if (!File.Exists(fileName))
            {
                Save(fileName);
                return;
            }

            var doc = XDocument.Load(fileName);
            var root = doc.Root;

            foreach (var donatorEl in root.Elements())
            {
                var member = Convert.ToUInt64(donatorEl.Attribute("member").Value);
                var balance = Convert.ToInt32(donatorEl.Element("Balance").Value);
                var date = Convert.ToDateTime(donatorEl.Element("Date").Value);
                var privateRole = donatorEl.Element("PrivateRole") == null
                    ? 0
                    : Convert.ToUInt64(donatorEl.Element("PrivateRole").Value);
                List<ulong> friends = new List<ulong>();
                if (donatorEl.Element("Friends") != null && donatorEl.Element("Friends").HasElements)
                    foreach (var friendEl in donatorEl.Element("Friends").Elements())
                        friends.Add(Convert.ToUInt64(friendEl.Value));
                var hidden = Convert.ToBoolean(donatorEl.Element("Hidden").Value);
                
                var donator = new Donator(member, balance, privateRole, date, friends, hidden);
            }
        }
    }
}