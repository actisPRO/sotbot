using System;
using System.Collections.Generic;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global

namespace SeaOfThieves.Entities
{
    public class Ship
    {
        public string Name { get; internal set; }
        public bool Status { get; internal set; }
        public ulong Role { get; internal set; }
        public ulong Channel { get; internal set; }
        public Dictionary<ulong, ShipMember> Members { get; private set; }

        private Ship(string name, ulong role, ulong channel)
        {
            Name = name;
            Status = false;
            Role = role;
            Channel = channel;
            Members = new Dictionary<ulong, ShipMember>();
        }

        public static Ship Create(string name, ulong role, ulong channel)
        {
            if (ShipList.Ships.ContainsKey(name))
            {
                throw new ShipExistsException();
            }
            else
            {
                var created = new Ship(name, role, channel);
            
                ShipList.Update(name, created);
                return ShipList.Ships[name]; 
            }
        }
        
        public void Delete()
        {
            ShipList.Remove(Name);
        }

        public void AddMember(ulong id, MemberType type = MemberType.Member, bool status = false)
        {
            if (Members.ContainsKey(id))
            {
                Console.WriteLine(id);
                
                throw new MemberExistsException();
            }
            else
            {
                Members[id] = new ShipMember(id, type, status); 
            }
            
            ShipList.Update(Name, this); //updates an element in collection
        }

        public void RemoveMember(ulong id)
        {
            if (Members.ContainsKey(id))
            {
                Members.Remove(id);
            }
            else
            {
                throw new MemberNotFoundException();
            }
            
            ShipList.Update(Name, this);
        }

        public void SetMemberStatus(ulong id, bool status)
        {
            if (Members.ContainsKey(id))
            {
                Members[id].Status = status;
            }
            else
            {
                throw new MemberNotFoundException();
            }
            
            ShipList.Update(Name, this);
        }

        public void SetMemberType(ulong id, MemberType type)
        {
            if (Members.ContainsKey(id))
            {
                Members[id].Type = type;
            }
            else
            {
                throw new MemberNotFoundException();
            }
            
            ShipList.Update(Name, this);
        }

        public void SetRole(ulong id)
        {
            Role = id;
            ShipList.Update(Name, this);
        }

        public void SetChannel(ulong id)
        {
            Channel = id;
            ShipList.Update(Name, this);
        }

        public void SetStatus(bool status)
        {
            Status = status;
            ShipList.Update(Name, this);
        }

        public bool IsInvited(ulong member)
        {
            if (!Members.ContainsKey(member))
            {
                return false;
            }

            return !Members[member].Status;
        }
    }
}