using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace SeaOfThieves.Entities
{
    public static class PriceList
    {
        public static Dictionary<DateTime, DateServices> Prices;

        public static void ReadFromXML(string fileName)
        {
            var doc = XDocument.Load(fileName);
            foreach (var date in doc.Element("Prices").Elements("Date"))
            {
                var prices = new Dictionary<string, int>();
                foreach (var price in date.Elements("Service"))
                {
                    prices[price.Attribute("id").Value] = Convert.ToInt32(price.Value);
                }
                Prices[Convert.ToDateTime(date.Attribute("date").Value)] = 
                    new DateServices(Convert.ToDateTime(date.Attribute("date").Value), prices);
            }
        }

        public static void SaveToXML(string fileName)
        {
            var doc = new XDocument();
            var root = new XElement("Prices");

            foreach (var date in Prices.Values)
            {
                var dateEl = new XElement("Date", new XAttribute("date", date.Date.ToString("dd.MM.yyyy")));
                foreach (var service in date.Services)
                {
                    dateEl.Add(new XElement("Service", new XAttribute("id", service.Key), service.Value));
                }
                root.Add(dateEl);
            }
            
            doc.Add(root);
            doc.Save(fileName);
        }
    }
}