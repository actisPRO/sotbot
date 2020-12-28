using DSharpPlus.Entities;

namespace Bot_NetCore.Misc
{
    public static class ExtendedEmbedBuilder
    {
        /// <summary>
        /// Добавляет пустое поле 
        /// </summary>
        public static void AddEmptyField(this DiscordEmbedBuilder embed)
        {
             embed.AddField("\u200B", "\u200B", true);
        }

        /// <summary>
        /// Добавляет в конец строки пустые ячейки, для заполнения строки
        /// </summary>
        public static void NewInlineRow(this DiscordEmbedBuilder embed)
        {
            for(int i = 0; i < embed.Fields.Count % 3; i++)
            {
                embed.AddField("\u200B", "\u200B", true);
            }
        }

        /// <summary>
        /// Возможность добавить пустую строку в название или описание поля
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="inline"></param>
        public static void AddFieldOrEmpty(this DiscordEmbedBuilder embed, string name, string value, bool inline = false)
        {
            embed.AddField(string.IsNullOrEmpty(name) ? "\u200B" : name, string.IsNullOrEmpty(value) ? "\u200B" : value, inline);
        }

        /// <summary>
        /// Возможность добавить пустое поле если один из параметров отсутствует.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="inline"></param>
        public static void AddFieldOrEmptyField(this DiscordEmbedBuilder embed, string name, string value, bool inline = false)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
                embed.AddField("\u200B", "\u200B", true);
        }

        /// <summary>
        /// Если оба параметра не содержат текс, ничего не добавляем
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="inline"></param>
        public static void AddFieldOrDefault(this DiscordEmbedBuilder embed, string name, string value, bool inline = false)
        {
            if(!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                embed.AddField(string.IsNullOrEmpty(name) ? "\u200B" : name, string.IsNullOrEmpty(value) ? "\u200B" : value, inline);
        }

        /// <summary>
        /// Если описание поля пустое, то заменяем на параметр replace
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="replace"></param>
        /// <param name="inline"></param>
        public static void AddFieldOrReplace(this DiscordEmbedBuilder embed, string name, string value, string replace, bool inline = false)
        {
            if (!string.IsNullOrEmpty(name))
                embed.AddField(string.IsNullOrEmpty(name) ? "\u200B" : name, string.IsNullOrEmpty(value) ? replace : value, inline);
        }
    }
}
