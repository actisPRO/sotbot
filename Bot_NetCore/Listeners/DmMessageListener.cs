using System;
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
        public static async Task MessageCreated(MessageCreateEventArgs e)
        {
            if (e.Guild == null && 
                !e.Author.IsCurrent && 
                !e.Message.Content.StartsWith("!") &&
                !DmHandled.Contains(e.Author))
                await e.Channel.SendMessageAsync("Если у вас есть вопросы по поводу сервера, вы можете создать тикет командой **`!support`**\n" +
                    "\nВы можете найти ответ на свой вопрос в данных каналах:" +
                    "\n• Правила сервера: <#435486626551037963>" +
                    "\n• Описание доната: <#459657130786422784>" +
                    "\n• Гайд по боту: <#476430819011985418>; создание каналов: <#435445608082440213>" +
                    "\n• Запросы роли: <#696668143430533190> (из тех что перечислены в канале)" +
                    "\n• Правила рейда и получение доступа к нему: <#744944702415175760> <#744944784765878324>" +
                    "\n• Вопросы по игре: <#552407278158872590> <#725708512121847849> <#573865939976585226>");
        }
    }
}
