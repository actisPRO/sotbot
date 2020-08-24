using System;

namespace Bot_NetCore.Exceptions
{
    public class MemberExistsException : Exception
    {
        public MemberExistsException() : base()
        {
        }

        public MemberExistsException(string message) : base(message)
        {
        }

        public MemberExistsException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}