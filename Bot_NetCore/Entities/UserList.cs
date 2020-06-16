using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using SeaOfThieves.Misc;

namespace SeaOfThieves.Entities
{
    public static class UserList
    {
        public static Dictionary<ulong, User> Users = new Dictionary<ulong, User>();

        public static void Update(User user)
        {
            Users[user.Id] = user;
        }

        public static void SaveToXML(string xml)
        {
            if (File.Exists(xml)) File.Delete(xml);

            var fs = File.Create(xml);
            fs.Close();

            var doc = new XDocument();
            var root = new XElement("users");

            foreach (var user in Users.Values)
            {
                var userEl = new XElement("user", new XAttribute("id", user.Id));
                foreach (var warn in user.Warns)
                    userEl.Add(new XElement("warn", new XAttribute("moderator", warn.Moderator),
                        new XAttribute("date", warn.Date),
                        new XAttribute("reason", warn.Reason),
                        new XAttribute("id", warn.Id),
                        new XAttribute("logMessage", warn.LogMessage)));
                root.Add(userEl);
            }

            doc.Add(root);
            doc.Save(xml);
        }

        public static void ReadFromXML(string xml)
        {
            Users = new Dictionary<ulong, User>();

            var doc = XDocument.Load(xml);
            foreach (var user in doc.Element("users").Elements("user"))
            {
                var created = User.Create(Convert.ToUInt64(user.Attribute("id").Value));
                foreach (var warnEl in user.Elements("warn"))
                {
                    var id = "";
                    if (warnEl.Attribute("id") == null)
                        id = RandomString.NextString(12);
                    else
                        id = warnEl.Attribute("id").Value;

                    var logMessage = "0";
                    try
                    {
                        logMessage = warnEl.Attribute("logMessage").Value;
                    }
                    catch (NullReferenceException)
                    {
                    }

                    created.AddWarning(Convert.ToUInt64(warnEl.Attribute("moderator").Value),
                        Convert.ToDateTime(warnEl.Attribute("date").Value), warnEl.Attribute("reason").Value, id,
                        Convert.ToUInt64(logMessage));
                }
            }
        }
    }
}
