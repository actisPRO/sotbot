using System;

// ReSharper disable UnusedMember.Global
// ReSharper disable RedundantBaseConstructorCall

namespace Bot_NetCore.Entities
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
