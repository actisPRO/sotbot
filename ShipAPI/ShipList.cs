using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
// ReSharper disable PossibleNullReferenceException
// ReSharper disable UnusedMember.Global

namespace ShipAPI
{
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
            {
                foreach (var member in ship.Members.Values)
                {
                    if (member.Id == id && member.Type == MemberType.Owner)
                    {
                        return ship;
                    }   
                }
            }

            return null;
        }

        public static void SaveToXML(string fileName)
        {
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            var fs = File.Create(fileName);
            fs.Close();
            
            var doc = new XDocument();

            var root = new XElement("ships");
            
            foreach (var ship in Ships.Values)
            {
                XElement shipE = new XElement("ship", new XAttribute("name", ship.Name), new XAttribute("status", ship.Status));
                
                shipE.Add(new XElement("role", ship.Role));
                shipE.Add(new XElement("channel", ship.Channel));

                foreach (var member in ship.Members.Values)
                {
                    shipE.Add(new XElement("member", member.Id, new XAttribute("type", 
                        member.Type.ToString().ToLower()), new XAttribute("status", member.Status)));
                }

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
                    var ship = Ship.Create(shipE.Attribute("name").Value, Convert.ToUInt64(shipE.Element("role").Value),
                        Convert.ToUInt64(shipE.Element("channel").Value));

                    ship.Status = Convert.ToBoolean(shipE.Attribute("status").Value);

                    foreach (var memberE in shipE.Elements("member"))
                    {
                        MemberType type;
                        switch (memberE.Attribute("type").Value.ToLower())
                        {
                            case "member":
                                type = MemberType.Member;
                                break;
                            case "officer":
                                type = MemberType.Officer;
                                break;
                            case "owner":
                                type = MemberType.Owner;
                                break;
                            default:
                                type = MemberType.Member;
                                break;
                        }
                        
                        ship.AddMember(Convert.ToUInt64(memberE.Value), type, Convert.ToBoolean(memberE.Attribute("status").Value));
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