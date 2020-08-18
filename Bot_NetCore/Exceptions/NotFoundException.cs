using System;

namespace SeaOfThieves.Exceptions
{
    public class NotFoundException : Exception
    {
        public NotFoundException() : base()
        {
        }

        public NotFoundException(string message) : base(message)
        {
        }

        public NotFoundException(string message, NotFoundException inner) : base(message, inner)
        {
        }
    }
}