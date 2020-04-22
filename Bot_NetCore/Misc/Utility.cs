using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using System;
using System.Collections.Generic;

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
                /*if (row % groupBy == 0 || row >= input.Count)
                {
                    split.Add(msg);
                    msg = "";
                }*/
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
    }
}
