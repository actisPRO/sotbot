using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        [Obsolete("ReadFromXMLMigration is deprecated, please use ReadFromXML instead.")]
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
                    foreach (var referral in inviter.Elements("referral")) elem.AddReferral(Convert.ToUInt64(referral.Value), date: DateTime.UtcNow.AddMonths(-1));
                }
                SaveToXML(fileName);
            }
        }

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

    public class Inviter
    {
        public Inviter(ulong id, bool active = true)
        {
            InviterId = id;
            Active = active;
            Referrals = new Dictionary<ulong, Referral>();

            InviterList.Inviters[InviterId] = this;
        }

        public ulong InviterId { get; }
        public Dictionary<ulong, Referral> Referrals { get; }
        public bool Active { get; private set; }
        public int ActiveCount { get; private set; }
        public int CurrentMonthActiveCount { get; private set; }

        public static Inviter Create(ulong inviterId)
        {
            InviterList.Update(new Inviter(inviterId));

            return new Inviter(inviterId);
        }

        public void AddReferral(ulong referralId, bool state = true, DateTime? date = null)
        {
            if (Referrals.ContainsKey(referralId)) return;

            Referrals.Add(referralId, new Referral(referralId, state: state, date: date));

            InviterList.Inviters[InviterId] = this;

            UpdateActiveReferrals();
        }

        public void RemoveReferral(ulong referralId)
        {
            if (!Referrals.ContainsKey(referralId)) return;

            Referrals.Remove(referralId);

            InviterList.Inviters[InviterId] = this;

            UpdateActiveReferrals();
        }

        public void UpdateReferral(ulong referralId, bool state)
        {
            if (!Referrals.ContainsKey(referralId)) return;

            Referrals[referralId].Active = state;

            InviterList.Inviters[InviterId] = this;

            UpdateActiveReferrals();
        }

        private void UpdateActiveReferrals()
        {
            ActiveCount = Referrals.Where(x => x.Value.Active == true).ToList().Count;
            CurrentMonthActiveCount = Referrals.Where(x => x.Value.Active == true && x.Value.Date.Month == DateTime.UtcNow.Month).ToList().Count;
        }

        public void UpdateState(bool state)
        {
            Active = state;

            InviterList.Inviters[InviterId] = this;
        }

        public void Remove()
        {
            InviterList.Inviters.Remove(this.InviterId);
        }
    }
    public class Referral
    {
        public Referral(ulong id, bool state = true, DateTime? date = null)
        {
            Id = id;
            Active = state;
            Date = date ?? DateTime.UtcNow.Date;
        }

        public ulong Id { get; }
        public DateTime Date { get; }
        public bool Active { get; set; }
    }
}
