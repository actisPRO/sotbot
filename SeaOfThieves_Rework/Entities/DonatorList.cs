using System;
using System.Collections.Generic;
using System.Xml.Linq;
using DSharpPlus.Entities;

namespace SeaOfThieves.Entities
{
    public static class DonatorList
    {
        public static Dictionary<ulong, Donator> Donators = new Dictionary<ulong, Donator>();

        public static void SaveToXML(string fileName)
        {
            var doc = new XDocument();
            var root = new XElement("donators");
            
            foreach (var donator in Donators.Values)
            {
                root.Add(new XElement("donator", donator.Member, new XAttribute("balance", donator.Balance), 
                    new XAttribute("color", donator.ColorRole)));
            }
            
            doc.Add(root);
            doc.Save(fileName);
        }

        public static void ReadFromXML(string fileName)
        {
            var doc = XDocument.Load(fileName);
            foreach (var donator in doc.Element("donators").Elements("donator"))
            {
                var created = 
                    new Donator(Convert.ToUInt64(donator.Value), 
                        Convert.ToUInt64(donator.Attribute("color").Value), 
                        Convert.ToDouble(donator.Attribute("balance").Value));
            }
        }
    }
}