using Bot_NetCore.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace SeaOfThieves.Entities
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
                    new XAttribute("active", inviter.Active)
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

        /*[Obsolete("ReadFromXMLMigration is deprecated, please use ReadFromXML instead.")]
        public static void ReadFromXMLMigration(string fileName)
        {
            //If old file exist do nothing
            FileInfo fi = new FileInfo("old_" + fileName);
            if (!fi.Exists)
            {
                //Rename old file
                fi = new FileInfo(fileName);
                if (fi.Exists)
                {
                    fi.MoveTo("old_" + fileName);
                }

                var doc = XDocument.Load("old_" + fileName);
                foreach (var inviter in doc.Element("inviters").Elements("inviter"))
                {
                    var elem = new Inviter(Convert.ToUInt64(inviter.Element("inviterId").Value),
                                              Convert.ToBoolean(inviter.Element("active").Value));
                    foreach (var referral in inviter.Elements("referral")) elem.AddReferral(Convert.ToUInt64(referral.Value));
                }
                SaveToXML(fileName);
            }
        }*/

        public static void ReadFromXML(string fileName)
        {
            var doc = XDocument.Load(fileName);
            foreach (var inviter in doc.Element("inviters").Elements("inviter"))
            {
                var elem = new Inviter(Convert.ToUInt64(inviter.Attribute("id").Value),
                                          Convert.ToBoolean(inviter.Attribute("active").Value));

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
