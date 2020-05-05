using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Bot_NetCore.Entities
{
    public static class ReportList
    {
        public static Dictionary<ulong, MemberReport> Mutes = new Dictionary<ulong, MemberReport>();
        public static Dictionary<ulong, MemberReport> CodexPurges = new Dictionary<ulong, MemberReport>();
        public static Dictionary<ulong, MemberReport> FleetPurges = new Dictionary<ulong, MemberReport>();

        public static void SaveToXML(string fileName)
        {
            var doc = new XDocument();
            
            var root = new XElement("Reports");

            var rElement = new XElement("Mutes");
            foreach (var mute in Mutes)
                rElement.Add(СreateXElement(mute.Value));

            var cElement = new XElement("CodexPurges");
            foreach (var codexPurge in CodexPurges)
                cElement.Add(СreateXElement(codexPurge.Value));

            var fElement = new XElement("FleetPurges");
            foreach (var fleetPurge in FleetPurges)
                fElement.Add(СreateXElement(fleetPurge.Value));

            root.Add(rElement);
            root.Add(cElement);
            root.Add(fElement);

            doc.Add(root);
            doc.Save(fileName);
        }

        public static void ReadFromXML(string fileName)
        {
            var doc = XDocument.Load(fileName);

            foreach (var mute in doc.Element("Reports").Element("Mutes").Elements("Report"))
                Mutes.Add(GetMemberFromXElement(mute).Id, GetMemberFromXElement(mute));

            foreach (var codexPurge in doc.Element("Reports").Element("CodexPurges").Elements("Report"))
                CodexPurges.Add(GetMemberFromXElement(codexPurge).Id, GetMemberFromXElement(codexPurge));

            foreach (var fleetPurge in doc.Element("Reports").Element("FleetPurges").Elements("Report"))
                FleetPurges.Add(GetMemberFromXElement(fleetPurge).Id, GetMemberFromXElement(fleetPurge));
        }

        private static XElement СreateXElement(MemberReport report)
        {
            var xElement = new XElement("Report");
            xElement.Add(new XElement("Id", report.Id));
            xElement.Add(new XElement("PurgeDateTime", report.PurgeDateTime));
            xElement.Add(new XElement("PurgeDuration", report.PurgeDuration.ToString()));
            xElement.Add(new XElement("Moderator", report.Moderator));
            xElement.Add(new XElement("Reason", report.Reason));
            return xElement;
        }

        private static MemberReport GetMemberFromXElement(XElement element)
        {
            return new MemberReport(
                        Convert.ToUInt64(element.Element("Id").Value),
                        Convert.ToDateTime(element.Element("PurgeDateTime").Value),
                        TimeSpan.Parse(element.Element("PurgeDuration").Value),
                        Convert.ToUInt64(element.Element("Moderator").Value),
                        element.Element("Reason").Value);
        }
    }

    public class MemberReport
    {
        public MemberReport(ulong id, DateTime purgeDateTime, TimeSpan purgeDuration, ulong moderator, string reason)
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
