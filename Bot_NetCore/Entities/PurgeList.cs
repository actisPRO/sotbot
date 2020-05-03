using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Bot_NetCore.Entities
{
    public static class PurgeList
    {
        public static Dictionary<ulong, PurgeMember> PurgeMembers = new Dictionary<ulong, PurgeMember>();

        public static void SaveToXML(string fileName)
        {
            var doc = new XDocument();
            var root = new XElement("purges");

            foreach (var purge in PurgeMembers.Values)
            {
                var dElement = new XElement("purge");
                dElement.Add(new XElement("Id", purge.Id));
                dElement.Add(new XElement("PurgeDateTime", purge.PurgeDateTime));
                dElement.Add(new XElement("PurgeDuration", purge.PurgeDuration.ToString()));
                dElement.Add(new XElement("Moderator", purge.Moderator));
                dElement.Add(new XElement("Reason", purge.Reason));

                root.Add(dElement);
            }

            doc.Add(root);
            doc.Save(fileName);
        }

        public static void ReadFromXML(string fileName)
        {
            var doc = XDocument.Load(fileName);
            foreach (var purge in doc.Element("purges").Elements("purge"))
            {
                var purgeMember =
                    new PurgeMember(
                        Convert.ToUInt64(purge.Element("Id").Value),
                        Convert.ToDateTime(purge.Element("PurgeDateTime").Value),
                        TimeSpan.Parse(purge.Element("PurgeDuration").Value),
                        Convert.ToUInt64(purge.Element("Moderator").Value),
                        purge.Element("Reason").Value);
                PurgeMembers.Add(purgeMember.Id, purgeMember);
            }
        }
    }

    public class PurgeMember
    {
        public PurgeMember(ulong id, DateTime purgeDateTime, TimeSpan purgeDuration, ulong moderator, string reason)
        {
            Id = id;
            PurgeDateTime = purgeDateTime;
            PurgeDuration = purgeDuration;
            Moderator = moderator;
            Reason = reason;
        }

        public ulong Id { get; }
        public DateTime PurgeDateTime { get; private set; }
        public TimeSpan PurgeDuration { get; private set; }
        public ulong Moderator { get; private set; }
        public string Reason { get; private set; }

        public void UpdatePurge(DateTime purgeDateTime, TimeSpan purgeDuration, ulong moderator, string reason)
        {
            PurgeDateTime = purgeDateTime;
            PurgeDuration = purgeDuration;
            Moderator = moderator;
            Reason = reason;
        }

        public bool Expired()
        {
            return (PurgeDateTime.Add(PurgeDuration) - DateTime.Now).TotalSeconds <= 0;
        }

        public DateTime getExpirationDateTime()
        {
            return PurgeDateTime.Add(PurgeDuration);
        }

        public TimeSpan getRemainingTime()
        {
            return PurgeDateTime.Add(PurgeDuration) - DateTime.Now;
        }
    }
}
