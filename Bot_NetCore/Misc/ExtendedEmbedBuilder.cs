using DSharpPlus.Entities;

namespace Bot_NetCore.Misc
{
    public static class ExtendedEmbedBuilder
    {
        //Добавляет пустое поле 
        public static void AddEmptyField(this DiscordEmbedBuilder embed)
        {
             embed.AddField("\u200B", "\u200B", true);
        }

        //Добавляет в конец строки пустые ячейки, для заполнения
        public static void NewInlineRow(this DiscordEmbedBuilder embed)
        {
            for(int i = 0; i < embed.Fields.Count % 3; i++)
            {
                embed.AddField("\u200B", "\u200B", true);
            }
        }

        //Возможность добавить пустую строку в название или описание поля
        public static void AddFieldOrEmpty(this DiscordEmbedBuilder embed, string name, string value, bool inline = false)
        {
            embed.AddField(string.IsNullOrEmpty(name) ? "\u200B" : name, string.IsNullOrEmpty(value) ? "\u200B" : value, inline);
        }

        //Возможность добавить пустую строку в название или описание поля
        public static void AddFieldOrEmptyField(this DiscordEmbedBuilder embed, string name, string value, bool inline = false)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
                embed.AddField("\u200B", "\u200B", true);
        }

        //Если оба параметра не содержат текс, ничего не добавляем
        public static void AddFieldOrDefault(this DiscordEmbedBuilder embed, string name, string value, bool inline = false)
        {
            if(!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                embed.AddField(string.IsNullOrEmpty(name) ? "\u200B" : name, string.IsNullOrEmpty(value) ? "\u200B" : value, inline);
        }

        //Если оба параметра не содержат текс, ничего не добавляем
        public static void AddFieldOrReplace(this DiscordEmbedBuilder embed, string name, string value, string replace, bool inline = false)
        {
            if (!string.IsNullOrEmpty(name))
                embed.AddField(string.IsNullOrEmpty(name) ? "\u200B" : name, string.IsNullOrEmpty(value) ? replace : value, inline);
        }
    }
}
