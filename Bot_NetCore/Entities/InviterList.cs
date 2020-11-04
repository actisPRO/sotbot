using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Bot_NetCore.Entities
{
    public static class InviterList
    {
        public static Dictionary<ulong, Inviter> Inviters = new Dictionary<ulong, Inviter>();

        public static void Update(Inviter inviter)
        {
            Inviters[inviter.InviterId] = inviter;
        }

        public static void SaveToXML(string fileName)
        {
            var doc = new XDocument();
            var root = new XElement("inviters");

            foreach (var inviter in Inviters.Values)
            {
                var iElement = new XElement(
                    "inviter",
                    new XAttribute("id", inviter.InviterId),
                    new XAttribute("active", inviter.Active),
                    new XAttribute("ignored", inviter.Ignored)
                );
                foreach (var referral in inviter.Referrals)
                    iElement.Add(new XElement(
                                    "referral",
                                    new XAttribute("id", referral.Value.Id),
                                    new XAttribute("active", referral.Value.Active),
                                    new XAttribute("date", referral.Value.Date.ToShortDateString())
                                ));
                root.Add(iElement);
            }

            doc.Add(root);
            doc.Save(fileName);
        }

        public static void ReadFromXML(string fileName)
        {
            var doc = XDocument.Load(fileName);
            foreach (var inviter in doc.Element("inviters").Elements("inviter"))
            {
                var elem = new Inviter(Convert.ToUInt64(inviter.Attribute("id").Value),
                       Convert.ToBoolean(inviter.Attribute("active").Value),
                       Convert.ToBoolean(inviter.Attribute("ignored").Value));

                foreach (var referral in inviter.Elements("referral"))
                    elem.AddReferral(
                        Convert.ToUInt64(referral.Attribute("id").Value),
                        Convert.ToBoolean(referral.Attribute("active").Value),
                        Convert.ToDateTime(referral.Attribute("date").Value)
                    );
            }
        }
    }
}
