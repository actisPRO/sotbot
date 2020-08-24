using System;

namespace Bot_NetCore.Exceptions
{
    public class InvalidXMLException : Exception
    {
        public InvalidXMLException() : base()
        {
        }

        public InvalidXMLException(string message) : base(message)
        {
        }

        public InvalidXMLException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}