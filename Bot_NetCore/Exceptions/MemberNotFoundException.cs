using System;

namespace Bot_NetCore.Exceptions
{
    public class MemberNotFoundException : Exception
    {
        public MemberNotFoundException() : base()
        {
        }

        public MemberNotFoundException(string message) : base(message)
        {
        }

        public MemberNotFoundException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}