using System.Collections.Generic;

namespace Bot_NetCore.Entities
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
