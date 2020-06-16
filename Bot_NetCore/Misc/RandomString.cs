using System;
using System.Linq;

namespace SeaOfThieves.Misc
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
