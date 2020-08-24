using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Bot_NetCore.Entities
{
    public class Subscriber
    {
        public static Dictionary<ulong, Subscriber> Subscribers = new Dictionary<ulong, Subscriber>();

        public ulong Member;

        private SubscriptionType _type;
        private DateTime _subscriptionStart;
        private DateTime _subscriptionEnd;
        private ulong _privateRole;
        private List<ulong> _friends;

        public SubscriptionType Type
        {
            get => _type;
            set
            {
                _type = value;
                Subscribers[Member]._type = value;
            }
        }

        public DateTime SubscriptionStart
        {
            get => _subscriptionStart;
            set
            {
                _subscriptionStart = value;
                Subscribers[Member]._subscriptionStart = value;
            }
        }

        public DateTime SubscriptionEnd
        {
            get => _subscriptionEnd;
            set
            {
                _subscriptionEnd = value;
                Subscribers[Member]._subscriptionEnd = value;
            }
        }

        public ulong PrivateRole
        {
            get => _privateRole;
            set
            {
                _privateRole = value;
                Subscribers[Member]._privateRole = value;
            }
        }

        public List<ulong> Friends
        {
            get => _friends;
            set
            {
                _friends = value;
                Subscribers[Member]._friends = value;
            }
        }

        public Subscriber(ulong member, SubscriptionType type, DateTime subscriptionStart, DateTime subscriptionEnd, ulong privateRole, List<ulong> friends)
        {
            Member = member;

            _type = type;
            _subscriptionStart = subscriptionStart;
            _subscriptionEnd = subscriptionEnd;
            _privateRole = privateRole;
            _friends = friends;

            Subscribers[Member] = this;
        }

        public static void Save(string fileName)
        {
            var doc = new XDocument();
            var root = new XElement("Subscribers");

            foreach (var sub in Subscribers.Values)
            {
                var subEl = new XElement("Subscriber", new XAttribute("member", sub.Member));
                switch (sub._type)
                {
                    case SubscriptionType.Premium:
                        subEl.Add(new XElement("Type", "Premium"));
                        break;
                }
                subEl.Add(new XElement("Start", sub.SubscriptionStart));
                subEl.Add(new XElement("End", sub.SubscriptionEnd));
                subEl.Add(new XElement("PrivateRole", sub.PrivateRole));
                if (sub.Friends.Count > 0)
                {
                    var friendsEl = new XElement("Friends");
                    foreach (var friend in sub.Friends)
                    {
                        friendsEl.Add(new XElement("Friend", friend));
                    }
                    subEl.Add(friendsEl);
                }
                
                root.Add(subEl);
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

            foreach (var subEl in root.Elements())
            {
                var member = Convert.ToUInt64(subEl.Attribute("member").Value);
                
                var typeStr = subEl.Element("Type").Value;
                var type = SubscriptionType.Premium;
                var start = Convert.ToDateTime(subEl.Element("Start").Value);
                var end = Convert.ToDateTime(subEl.Element("End").Value);
                var privateRole = Convert.ToUInt64(subEl.Element("PrivateRole").Value);
                var friends = new List<ulong>();
                if (subEl.Element("Friends") != null)
                {
                    foreach (var friendEl in subEl.Element("Friends").Elements())
                    {
                        friends.Add(Convert.ToUInt64(friendEl.Value));
                    }
                }
                
                var sub = new Subscriber(member, type, start, end, privateRole, friends);
            }
        }
    }

    public enum SubscriptionType
    {
        Premium
    }
}