using System.Collections.Generic;
using System.Linq;

namespace SeaOfThieves.Entities
{
    public class UserLeft
    {
        public ulong Id;
        public List<ulong> Roles;

        public UserLeft(ulong id, List<ulong> roles)
        {
            Id = id;
            Roles = roles;
        }
    }
}
