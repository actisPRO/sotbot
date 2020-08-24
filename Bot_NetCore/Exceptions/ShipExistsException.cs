using System;

namespace Bot_NetCore.Exceptions
{
    public class ShipExistsException : Exception
    {
        public ShipExistsException() : base()
        {
        }

        public ShipExistsException(string message) : base(message)
        {
        }

        public ShipExistsException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}