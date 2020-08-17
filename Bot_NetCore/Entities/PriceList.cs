using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Bot_NetCore.Entities
{
    public static class PriceList
    {
        public static Dictionary<DateTime, DateServices> Prices = new Dictionary<DateTime, DateServices>();

        public static DateTime GetLastDate(DateTime forDate)
        {
            var retVal = forDate;
            foreach (var date in Prices.Keys)
            {
                if (date <= forDate) retVal = date;
                else break;
            }

            return retVal;
        }

        public static void ReadFromXML(string fileName)
        {
            var doc = XDocument.Load(fileName);
            foreach (var date in doc.Element("Prices").Elements("Date"))
            {
                int colorPrice = 0, wantedPrice = 0, roleRenamePrice = 0, friendsPrice = 0;
                foreach (var price in date.Elements("Service"))
                {
                    switch (price.Attribute("id").Value)
                    {
                        case "color":
                            colorPrice = Convert.ToInt32(price.Value);
                            break;
                        case "wanted":
                            wantedPrice = Convert.ToInt32(price.Value);
                            break;
                        case "role_rename":
                            roleRenamePrice = Convert.ToInt32(price.Value);
                            break;
                        case "friends":
                            friendsPrice = Convert.ToInt32(price.Value);
                            break;
                    }
                }

                var dateVal = Convert.ToDateTime(date.Attribute("date").Value);
                Prices[dateVal] = 
                    new DateServices(dateVal, colorPrice, wantedPrice, roleRenamePrice, friendsPrice);
            }
        }

        public static void SaveToXML(string fileName)
        {
            var doc = new XDocument();
            var root = new XElement("Prices");

            foreach (var date in Prices.Values)
            {
                var dateEl = new XElement("Date", new XAttribute("date", date.Date.ToString("dd.MM.yyyy")));
                dateEl.Add(new XElement("Service", date.ColorPrice, new XAttribute("id", "color")));
                dateEl.Add(new XElement("Service", date.WantedPrice, new XAttribute("id", "wanted")));
                dateEl.Add(new XElement("Service", date.RoleNamePrice, new XAttribute("id", "role_rename")));
                dateEl.Add(new XElement("Service", date.FriendsPrice, new XAttribute("id", "friends")));
                
                root.Add(dateEl);
            }
            
            doc.Add(root);
            doc.Save(fileName);
        }
    }
}
