using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Bot_NetCore.Misc
{
    class Utility
    {
        public static IEnumerable<Page> GeneratePagesInEmbeds(List<string> input)
        {
            if (input.Count == 0)
                throw new InvalidOperationException("You must provide a list of strings that is not null or empty!");

            List<Page> result = new List<Page>();
            List<string> split = new List<string>();

            int row = 1;
            string msg = "";
            foreach (string s in input)
            {
                if (msg.Length + s.Length >= 2000)
                {
                    split.Add(msg);
                    msg = "";
                }
                msg += $"{row}. {s} \n";
                if (row >= input.Count)
                    split.Add(msg);
                row++;
            }

            int page = 1;
            foreach (string s in split)
            {
                result.Add(new Page()
                {
                    Embed = new DiscordEmbedBuilder()
                    {
                        Title = $"Страница {page} / {split.Count}. Всего {input.Count}",
                        Description = s
                    }
                });
                page++;
            }
            return result;
        }

        //Парсит формат даты на подобии 1d2h30m, 1d, 30m10s
        //Добавлены дни, взято отсюда: 
        //https://stackoverflow.com/questions/47702094/parse-the-string-26h44m3s-to-timespan-in-c-sharp
        public static TimeSpan TimeSpanParse(string input)
        {
            var m = Regex.Match(input, @"^((?<days>\d+)d)?((?<hours>\d+)h)?((?<minutes>\d+)m)?((?<seconds>\d+)s)?$", 
                RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.RightToLeft);

            int ds = m.Groups["days"].Success ? int.Parse(m.Groups["days"].Value) : 0;
            int hs = m.Groups["hours"].Success ? int.Parse(m.Groups["hours"].Value) : 0;
            int ms = m.Groups["minutes"].Success ? int.Parse(m.Groups["minutes"].Value) : 0;
            int ss = m.Groups["seconds"].Success ? int.Parse(m.Groups["seconds"].Value) : 0;

            return TimeSpan.FromSeconds(ds * 24 * 60 * 60 + hs * 60 * 60 + ms * 60 + ss);
        }

        public static string FormatTimespan(TimeSpan time)
        {
            string ds = time.Days != 0 ? string.Format("{0:%d}дней ", time) : "";
            string hs = time.Hours != 0 ? string.Format("{0:%h}часов ", time) : "";
            string ms = time.Minutes != 0 ? string.Format("{0:%m}мин ", time) : "";
            string ss = time.Seconds != 0 ? string.Format(" {0:%s}сек ", time) : "";
            return (ds + hs + ms + ss).TrimEnd(' ');
            //return string.Format("{0:%d}д {0:%h}ч {0:%m}м {0:%s}с", time);
        }
    }
}
