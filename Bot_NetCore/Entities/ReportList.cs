using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Bot_NetCore.Entities
{
    public static class ReportList
    {
        public static Dictionary<ulong, MemberReport> Mutes = new Dictionary<ulong, MemberReport>();
        public static Dictionary<ulong, MemberReport> VoiceMutes = new Dictionary<ulong, MemberReport>();
        public static Dictionary<ulong, MemberReport> CodexPurges = new Dictionary<ulong, MemberReport>();
        public static Dictionary<ulong, MemberReport> FleetPurges = new Dictionary<ulong, MemberReport>();

        public static void SaveToXML(string fileName)
        {
            var doc = new XDocument();
            
            var root = new XElement("Reports");

            var rElement = new XElement("Mutes");
            foreach (var mute in Mutes)
                rElement.Add(СreateXElement(mute.Value));

            var vmElement = new XElement("VoiceMutes");
            foreach (var voiceMute in VoiceMutes)
                vmElement.Add(СreateXElement(voiceMute.Value));

            var cElement = new XElement("CodexPurges");
            foreach (var codexPurge in CodexPurges)
                cElement.Add(СreateXElement(codexPurge.Value));

            var fElement = new XElement("FleetPurges");
            foreach (var fleetPurge in FleetPurges)
                fElement.Add(СreateXElement(fleetPurge.Value));

            root.Add(rElement);
            root.Add(vmElement);
            root.Add(cElement);
            root.Add(fElement);

            doc.Add(root);
            doc.Save(fileName);
        }

        public static void ReadFromXML(string fileName)
        {
            var doc = XDocument.Load(fileName);

            var root = doc.Element("Reports");

            foreach (var mute in root.Element("Mutes").Elements("Report"))
                Mutes.Add(GetMemberFromXElement(mute).Id, GetMemberFromXElement(mute));

            foreach (var voiceMute in root.Element("VoiceMutes").Elements("Report"))
                VoiceMutes.Add(GetMemberFromXElement(voiceMute).Id, GetMemberFromXElement(voiceMute));

            foreach (var codexPurge in root.Element("CodexPurges").Elements("Report"))
                CodexPurges.Add(GetMemberFromXElement(codexPurge).Id, GetMemberFromXElement(codexPurge));

            foreach (var fleetPurge in root.Element("FleetPurges").Elements("Report"))
                FleetPurges.Add(GetMemberFromXElement(fleetPurge).Id, GetMemberFromXElement(fleetPurge));
        }

        private static XElement СreateXElement(MemberReport report)
        {
            var xElement = new XElement("Report");
            xElement.Add(new XElement("Id", report.Id));
            xElement.Add(new XElement("ReportDateTime", report.ReportDateTime));
            xElement.Add(new XElement("ReportDuration", report.ReportDuration.ToString()));
            xElement.Add(new XElement("Moderator", report.Moderator));
            xElement.Add(new XElement("Reason", report.Reason));
            return xElement;
        }

        private static MemberReport GetMemberFromXElement(XElement element)
        {
            return new MemberReport(
                        Convert.ToUInt64(element.Element("Id").Value),
                        Convert.ToDateTime(element.Element("ReportDateTime").Value),
                        TimeSpan.Parse(element.Element("ReportDuration").Value),
                        Convert.ToUInt64(element.Element("Moderator").Value),
                        element.Element("Reason").Value);
        }
    }
}
