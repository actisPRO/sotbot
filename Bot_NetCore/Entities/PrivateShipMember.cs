namespace Bot_NetCore.Entities
{
    public class PrivateShipMember
    {
        public readonly string Ship;
        public readonly ulong MemberId;

        private PrivateShipMemberRole _role;
        private bool _status;

        public PrivateShipMemberRole Role
        {
            get => _role;
            set
            {
                // mysql logic here
                _role = value;
            }
        }

        public bool Status
        {
            get => _status;
            set
            {
                // mysql logic here
                _status = value;
            }
        }

        private PrivateShipMember(string ship, ulong memberId, PrivateShipMemberRole role, bool status)
        {
            Ship = ship;
            MemberId = memberId;
            Role = role;
            Status = status;
        }
    }

    public enum PrivateShipMemberRole
    {
        Member,
        Officer,
        Captain
    }
}