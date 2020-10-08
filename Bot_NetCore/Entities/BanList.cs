using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Bot_NetCore.Entities
{
    [Obsolete] //todo delete after migration
    public static class BanList
    {
        public static Dictionary<ulong, BannedUser> BannedMembers = new Dictionary<ulong, BannedUser>();

        public static void SaveToXML(string fileName)
        {
            var doc = new XDocument();
            var root = new XElement("Bans");

            foreach (var banned in BannedMembers.Values)
            {
                var dElement = new XElement("Ban");
                dElement.Add(new XElement("Id", banned.Id));
                dElement.Add(new XElement("UnbanDateTime", banned.UnbanDateTime));
                dElement.Add(new XElement("BanDateTime", banned.BanDateTime));
                dElement.Add(new XElement("Moderator", banned.Moderator));
                dElement.Add(new XElement("Reason", banned.Reason));
                dElement.Add(new XElement("BanId", banned.BanId));

                root.Add(dElement);
            }

            doc.Add(root);
            doc.Save(fileName);
        }

        public static void ReadFromXML(string fileName)
        {
            var doc = XDocument.Load(fileName);
            foreach (var banned in doc.Element("Bans").Elements("Ban"))
            {
                var created =
                    new BannedUser(
                        Convert.ToUInt64(banned.Element("Id").Value),
                        Convert.ToDateTime(banned.Element("UnbanDateTime").Value),
                        Convert.ToDateTime(banned.Element("BanDateTime").Value),
                        Convert.ToUInt64(banned.Element("Moderator").Value),
                        banned.Element("Reason").Value,
                        banned.Element("BanId").Value);
            }
        }
    }
}
