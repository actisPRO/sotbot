using System;
using System.Collections.Generic;
using Microsoft.VisualBasic.FileIO;

namespace Bot_NetCore.Misc
{
    public static class FastShipStats
    {
        /// <summary>
        ///     Loads fast ship name generation stats from the specified file.
        /// </summary>
        /// <param name="to"></param>
        /// <param name="filename"></param>
        public static Dictionary<string, int[]> LoadFromFile(string filename)
        {
            var result = new Dictionary<string, int[]>();
            using (var parser = new TextFieldParser(filename))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                while (!parser.EndOfData)
                {
                    var fields = parser.ReadFields();
                    
                    try
                    {
                        var ret = Convert.ToInt32(fields[1]);
                    }
                    catch (FormatException e)
                    {
                        // fields[1] cant be parsed correctly, so the line is a header line and we can skip it
                        continue;
                    }
                    
                    result[fields[0]] = new[]
                    {
                        Convert.ToInt32(fields[1]), // sloops
                        Convert.ToInt32(fields[2]), // brigs
                        Convert.ToInt32(fields[3]) // galleons
                    };
                }
            }

            return result;
        }

        public static void WriteToFile(Dictionary<string, int[]> stats, string filename)
        {
            var export = new CsvExport();
            foreach (var element in stats)
            {
                export.AddRow();
                export["Name"] = element.Key;
                export["Sloops"] = element.Value[0];
                export["Brigantines"] = element.Value[1];
                export["Galleons"] = element.Value[2];
            }
            
            export.ExportToFile(filename);
        }
    }
}