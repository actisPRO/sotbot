using DSharpPlus.Entities;

namespace Bot_NetCore.Misc
{
    public static class ExtendedEmbedBuilder
    {
        /// <summary>
        /// Добавляет пустое поле 
        /// </summary>
        public static DiscordEmbedBuilder AddEmptyField(this DiscordEmbedBuilder embed)
        {
             return embed.AddField("\u200B", "\u200B", true);
        }

        /// <summary>
        /// Добавляет в конец строки пустые ячейки, для заполнения строки
        /// </summary>
        public static DiscordEmbedBuilder NewInlineRow(this DiscordEmbedBuilder embed)
        {
            for(int i = 0; i < embed.Fields.Count % 3; i++)
            {
                embed.AddField("\u200B", "\u200B", true);
            }
            return embed;
        }

        /// <summary>
        /// Возможность добавить пустую строку в название или описание поля
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="inline"></param>
        public static DiscordEmbedBuilder AddFieldOrEmpty(this DiscordEmbedBuilder embed, string name, string value, bool inline = false)
        {
            return embed.AddField(string.IsNullOrEmpty(name) ? "\u200B" : name, string.IsNullOrEmpty(value) ? "\u200B" : value, inline);
        }

        /// <summary>
        /// Возможность добавить пустое поле если один из параметров отсутствует.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="inline"></param>
        public static DiscordEmbedBuilder AddFieldOrEmptyField(this DiscordEmbedBuilder embed, string name, string value, bool inline = false)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
                embed.AddField("\u200B", "\u200B", true);
            return embed;
        }

        /// <summary>
        /// Если оба параметра не содержат текс, ничего не добавляем
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="inline"></param>
        public static DiscordEmbedBuilder AddFieldOrDefault(this DiscordEmbedBuilder embed, string name, string value, bool inline = false)
        {
            if(!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                embed.AddField(string.IsNullOrEmpty(name) ? "\u200B" : name, string.IsNullOrEmpty(value) ? "\u200B" : value, inline);
            return embed;
        }

        /// <summary>
        /// Если описание поля пустое, то заменяем на параметр replace
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="replace"></param>
        /// <param name="inline"></param>
        public static DiscordEmbedBuilder AddFieldOrReplace(this DiscordEmbedBuilder embed, string name, string value, string replace, bool inline = false)
        {
            if (!string.IsNullOrEmpty(name))
                embed.AddField(string.IsNullOrEmpty(name) ? "\u200B" : name, string.IsNullOrEmpty(value) ? replace : value, inline);
            return embed;
        }
    }
}
