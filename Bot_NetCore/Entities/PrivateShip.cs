namespace Bot_NetCore.Entities
{
    public class PrivateShip
    {
        private string _name;
        private ulong _channel;

        public string Name
        {
            get => _name;
            set
            {
                // mysql logic here
                _name = value;
            }
        }

        public ulong Channel
        {
            get => _channel;
            set
            {
                // mysql logic here
                _channel = value;
            }
        }

        private PrivateShip(string name, ulong channel)
        {
            _name = name;
            _channel = channel;
        }
    }
}