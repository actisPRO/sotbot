using System;
using System.Linq;

namespace SeaOfThieves.Misc
{
    public static class RandomString
    {
        private static Random _random = new Random();

        public static string NextString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[_random.Next(s.Length)]).ToArray());
        }
    }
}