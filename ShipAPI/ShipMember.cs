namespace ShipAPI
{
    public class ShipMember
    {
        public ulong Id { get; internal set; }
        public MemberType Type { get; internal set; }
        public bool Status { get; internal set; }

        internal ShipMember(ulong id, MemberType type, bool status)
        {
            Id = id;
            Type = type;
            Status = status;
        }
    }
}