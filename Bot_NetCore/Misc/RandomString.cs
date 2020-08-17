using System;
using System.Linq;

namespace Bot_NetCore.Misc
{
    public static class RandomString
    {
        private static readonly Random _random = new Random();

        public static string NextString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[_random.Next(s.Length)]).ToArray());
        }
    }
}
