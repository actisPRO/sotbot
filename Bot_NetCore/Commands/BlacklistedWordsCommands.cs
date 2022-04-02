using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot_NetCore.Attributes;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;

namespace Bot_NetCore.Commands
{
    [Group("blacklistedwords")]
    [Aliases("blw")]
    [Description("Команды фильтра запрещенных слов.")]
    [RequireCustomRole(RoleType.Moderator)]
    [RequireGuild]
    public class BlacklistedWordsCommands : BaseCommandModule
    {
        [Command("add")]
        [Description("Добавить слово в список")]
        public async Task Add(CommandContext ctx, String word)
        {
            _ = BlacklistedWordsSQL.Add(word) == true ?
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно добавлена запись!") :
            await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось добавить запись!");
        }

        [Command("remove")]
        [Description("Убрать слово из списка")]
        public async Task Remove(CommandContext ctx, ulong id)
        {
            _ = BlacklistedWordsSQL.Remove(id) == true ?
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удалена запись!") :
            await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось удалить запись!");
        }

        [Command("list")]
        [Description("Выводит список запрещенных слов")]
        public async Task List(CommandContext ctx)
        {

            var words = BlacklistedWordsSQL.Update()
                .OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value)
                .Select(x => $"**id**: {x.Key} | {x.Value}")
                .ToList();

            var words_pagination = Utility.GeneratePagesInEmbeds(words, $"Список запрещенных слов.");

            var interactivity = ctx.Client.GetInteractivity();
            if (words_pagination.Count() > 1)
                //await interactivity.SendPaginatedMessageAsync(await ctx.Member.CreateDmChannelAsync(), ctx.User, words_pagination, timeoutoverride: TimeSpan.FromMinutes(5));
                await interactivity.SendPaginatedMessageAsync(
                    channel: await ctx.Member.CreateDmChannelAsync(),
                    user: ctx.User,
                    pages: words_pagination,
                    behaviour: PaginationBehaviour.Ignore,
                    deletion: ButtonPaginationBehavior.DeleteButtons,
                    token: default);
            else
                await ctx.RespondAsync(embed: words_pagination.First().Embed);
        }
    }
}
