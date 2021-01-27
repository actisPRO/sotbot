using System.Collections.Generic;
using System.Threading.Tasks;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace Bot_NetCore.Listeners
{
    public static class DmMessageListener
    {
        /// <summary>
        ///     Список содержащий пользователей лс которых уже обрабатывается.
        /// </summary>
        public static List<DiscordUser> DmHandled = new List<DiscordUser>();

        [AsyncListener(EventTypes.MessageCreated)]
        public static async Task MessageCreated(DiscordClient client, MessageCreateEventArgs e)
        {
            if (e.Guild == null &&
                !e.Author.IsCurrent &&
                !e.Message.Content.StartsWith(Bot.BotSettings.Prefix) &&
                !DmHandled.Contains(e.Author))
            {
                var embed = new DiscordEmbedBuilder()
                    .WithTitle("Помощь по серверу")
                    .WithDescription("Если у вас есть **вопросы по поводу сервера**, \n" +
                        "вы можете создать тикет командой **`!support`**\n\n")
                    .WithColor(new DiscordColor("#27ae60"));

                embed.AddFieldOrEmpty("Полезные каналы которые вам помогут:",
                    "• <#435486626551037963> \n" +
                    "• <#459657130786422784> \n" +
                    "• <#696668143430533190> \n" +
                    "• <#476430819011985418>, <#435445608082440213> \n" +
                    "• <#744944702415175760>, <#744944784765878324> \n" +
                    "• <#725708512121847849>, <#552407278158872590>");

                embed.AddFieldOrEmpty("", "Наиболее актуальную информацию по командам всегда можно получить через `!help`");


                await e.Channel.SendMessageAsync(embed: embed);
            }
        }
    }
}
