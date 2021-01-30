using System;

namespace Bot_NetCore.Entities
{
    [Obsolete]
    public class ShipMember
    {
        internal ShipMember(ulong id, MemberType type, bool status)
        {
            Id = id;
            Type = type;
            Status = status;
        }

        public ulong Id { get; internal set; }
        public MemberType Type { get; internal set; }
        public bool Status { get; internal set; }
    }
}
