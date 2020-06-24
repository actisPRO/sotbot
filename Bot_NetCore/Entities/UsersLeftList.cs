using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace SeaOfThieves.Entities
{
    public static class UsersLeftList
    {
        public static Dictionary<ulong, UserLeft> Users = new Dictionary<ulong, UserLeft>();

        public static void ReadFromXML(string filename)
        {
            var doc = XDocument.Load(filename);
            foreach (var user in doc.Element("UsersLeft").Elements("User"))
            {
                var id = Convert.ToUInt64(user.Attribute("id").Value);
                var roles = new List<ulong>();
                foreach (var role in user.Elements("Role"))
                {
                    roles.Add(Convert.ToUInt64(role.Value));
                }
                
                Users.Add(id, new UserLeft(id, roles));
            }

        }

        public static void SaveToXML(string filename)
        {
            var doc = new XDocument();
            var root = new XElement("UsersLeft");
            foreach (var user in Users.Values)
            {
                var userEl = new XElement("User", new XAttribute("id", user.Id));
                foreach (var role in user.Roles)
                {
                    userEl.Add(new XElement("Role", role));
                }
                root.Add(userEl);
            }
            
            doc.Add(root);
            doc.Save(filename);
        }
    }
}
