using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Bot_NetCore.Exceptions;

// ReSharper disable PossibleNullReferenceException
// ReSharper disable UnusedMember.Global

namespace Bot_NetCore.Entities
{
    
    [Obsolete]
    public static class ShipList
    {
        public static Dictionary<string, Ship> Ships = new Dictionary<string, Ship>();

        internal static void Update(string id, Ship newShip)
        {
            Ships[id] = newShip;
        }

        internal static void Remove(string id)
        {
            Ships.Remove(id);
        }

        public static Ship GetOwnedShip(ulong id)
        {
            foreach (var ship in Ships.Values)
                foreach (var member in ship.Members.Values)
                    if (member.Id == id && member.Type == MemberType.Owner)
                        return ship;

            return null;
        }

        public static void SaveToXML(string fileName)
        {
            if (File.Exists(fileName)) File.Delete(fileName);

            var fs = File.Create(fileName);
            fs.Close();

            var doc = new XDocument();

            var root = new XElement("ships");

            foreach (var ship in Ships.Values)
            {
                if (ship == null) continue;

                var shipE = new XElement("ship", new XAttribute("name", ship.Name),
                    new XAttribute("status", ship.Status));

                shipE.Add(new XElement("channel", ship.Channel));
                shipE.Add(new XElement("creationMessage", ship.CreationMessage));
                shipE.Add(new XElement("lastUsed", ship.LastUsed));

                foreach (var member in ship.Members.Values)
                    shipE.Add(new XElement("member", member.Id, new XAttribute("type",
                        member.Type.ToString().ToLower()), new XAttribute("status", member.Status)));

                root.Add(shipE);
            }

            doc.Add(root);
            doc.Save(fileName);
        }

        public static void ReadFromXML(string filename)
        {
            var tempShip = Ships;

            try
            {
                var doc = XDocument.Load(filename);

                Ships = new Dictionary<string, Ship>();

                foreach (var shipE in doc.Element("ships").Elements("ship"))
                {
                    var creationMessage = "0";
                    try
                    {
                        creationMessage = shipE.Element("creationMessage").Value;
                    }
                    catch (NullReferenceException)
                    {
                    }

                    var lastUsed = DateTime.Now;
                    if (shipE.Element("lastUsed") != null) lastUsed = Convert.ToDateTime(shipE.Element("lastUsed").Value);

                    var ship = Ship.Create(
                        shipE.Attribute("name").Value,
                        Convert.ToUInt64(shipE.Element("channel").Value),
                        Convert.ToUInt64(creationMessage), lastUsed);

                    ship.Status = Convert.ToBoolean(shipE.Attribute("status").Value);

                    foreach (var memberE in shipE.Elements("member"))
                    {
                        var type = (memberE.Attribute("type").Value.ToLower()) switch
                        {
                            "member" => MemberType.Member,
                            "officer" => MemberType.Officer,
                            "owner" => MemberType.Owner,
                            _ => MemberType.Member,
                        };
                        ship.AddMember(Convert.ToUInt64(memberE.Value), type,
                            Convert.ToBoolean(memberE.Attribute("status").Value));
                    }
                }
            }
            catch (NullReferenceException)
            {
                Ships = tempShip;
                throw new InvalidXMLException();
            }
        }
    }
}
