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
            string ds = time.Days != 0 ? ToCorrectCase(time, TimeUnit.Days) + " " : "";
            string hs = time.Hours != 0 ? ToCorrectCase(time, TimeUnit.Hours) + " " : "";
            string ms = time.Minutes != 0 ? ToCorrectCase(time, TimeUnit.Minutes) + " " : "";
            string ss = time.Seconds != 0 ? ToCorrectCase(time, TimeUnit.Seconds) : "";
            return (ds + hs + ms + ss).TrimEnd(' ');
            //return string.Format("{0:%d}д {0:%h}ч {0:%m}м {0:%s}с", time);
        }

        public static string ToCorrectCase(TimeSpan time, TimeUnit unit)
        {
            /*
             * Склонение:
             * Заканчивается на 1, но не на 11 (1, 21, 101, 1121) - номинатив ед. ч.
             * Заканчивается на 2, 3, 4, но не на 12, 13, 14 (2, 23, 954) - генетив ед. ч.
             * Заканчивается на 6, 7, 8, 9, 0, 11, 12, 13, 14 - генетив мн. ч.
             */

            string cts = time.ToString();
            switch (unit)
            {
                case TimeUnit.Days:
                    cts = $"{time:%d}";
                    break;
                case TimeUnit.Hours:
                    cts = $"{time:%h}";
                    break;
                case TimeUnit.Minutes:
                    cts = $"{time:%m}";
                    break;
                case TimeUnit.Seconds:
                    cts = $"{time:%s}";
                    break;
            }
            
            if (cts.EndsWith("1") && !cts.EndsWith("11"))
            {
                switch (unit)
                {
                    case TimeUnit.Seconds:
                        return $"{cts} секунда";
                    case TimeUnit.Minutes:
                        return $"{cts} минута";
                    case TimeUnit.Hours:
                        return $"{cts} час";
                    case TimeUnit.Days:
                        return $"{cts} день";
                }
            }
            else if ((cts.EndsWith("2") || cts.EndsWith("3") || cts.EndsWith("4")) && 
                !(cts.EndsWith("12") || cts.EndsWith("13") || cts.EndsWith("14")))
            {
                switch (unit)
                {
                    case TimeUnit.Seconds:
                        return $"{cts} секунды";
                    case TimeUnit.Minutes:
                        return $"{cts} минуты";
                    case TimeUnit.Hours:
                        return $"{cts} часа";
                    case TimeUnit.Days:
                        return $"{cts} дня";
                }
            }
            else
            {
                switch (unit)
                {
                    case TimeUnit.Seconds:
                        return $"{cts} секунд";
                    case TimeUnit.Minutes:
                        return $"{cts} минут";
                    case TimeUnit.Hours:
                        return $"{cts} часов";
                    case TimeUnit.Days:
                        return $"{cts} дней";
                }
            }

            return "err";
        }

        public enum TimeUnit
        {
            Seconds,
            Minutes,
            Hours,
            Days
        }
    }
}
