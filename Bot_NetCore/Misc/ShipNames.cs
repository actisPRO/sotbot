using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualBasic.FileIO;

namespace Bot_NetCore.Misc
{
    public static class ShipNames
    {
        private static List<string> MOne = new List<string>();
        private static List<string> MTwo = new List<string>();
        private static List<string> FOne = new List<string>();
        private static List<string> FTwo = new List<string>();
        private static List<string> COne = new List<string>();
        private static List<string> CTwo = new List<string>();

        public static void Read(string file)
        {
            using (TextFieldParser parser = new TextFieldParser(file))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                while (!parser.EndOfData)
                {
                    string[] fields = parser.ReadFields();
                    if (fields[0] != "")
                        MOne.Add(fields[0]);
                    if (fields[1] != "")
                        MTwo.Add(fields[1]);
                    if (fields[2] != "")
                        FOne.Add(fields[2]);
                    if (fields[3] != "")
                        FTwo.Add(fields[3]);
                    if (fields[4] != "")
                        COne.Add(fields[4]);
                    if (fields[5] != "")
                        CTwo.Add(fields[5]);
                }
            }
        }

        public static string GenerateChannelName(string[] usedNames)
        {
            var possibleM = MOne.Count * MTwo.Count;
            var possibleF = FOne.Count * FTwo.Count;
            var possibleC = COne.Count * CTwo.Count;

            string result;

            Random rnd = new Random();
            var number = rnd.Next(possibleM + possibleF + possibleC);
            if (number < possibleM)
                result = $"{MOne[rnd.Next(MOne.Count)]} {MTwo[rnd.Next(MTwo.Count)]}";
            else if (number < possibleM + possibleF)
                result = $"{FOne[rnd.Next(FOne.Count)]} {FTwo[rnd.Next(FTwo.Count)]}";
            else
                result = $"{COne[rnd.Next(COne.Count)]} {CTwo[rnd.Next(CTwo.Count)]}";

            if (usedNames.Contains(result))
                return GenerateChannelName(usedNames);
            else
                return result;
        }
    }
}
