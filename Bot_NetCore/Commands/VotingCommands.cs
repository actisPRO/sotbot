using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;

namespace Bot_NetCore.Commands
{
    [Group("vote")]
    [Aliases("v")]
    [Description("Команды голосований.")]
    [RequirePermissions(Permissions.KickMembers)]
    public class VotingCommands : BaseCommandModule
    {
        [Command("start")]
        [Description("Начинает голосование за/против")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task VoteStart(CommandContext ctx, [Description("Продолжительность голосования")] string duration, [Description("Тема голосования"), RemainingText] string topic)
        {
            var timespan = Utility.TimeSpanParse(duration);
            var end = DateTime.Now + timespan;
            var id = RandomString.NextString(6);

            var embed = Utility.GenerateVoteEmbed(ctx.Member, DiscordColor.Yellow, topic, end, 0, 0, 0, id);

            var message = await ctx.Guild.GetChannel(Bot.BotSettings.VotesChannel).SendMessageAsync(embed: embed);
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":no_entry:"));

            var vote = new Vote(topic, 0, 0, end, message.Id, ctx.Member.Id, id, new List<ulong>());
            vote.Message = message.Id;
            Vote.Save(Bot.BotSettings.VotesXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Голосование запущено!");
        }
        
        [Command("starteveryone")]
        [Aliases("starte")]
        [Description("Начинает голосование за/против")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task VoteStartEveryone(CommandContext ctx, [Description("Продолжительность голосования")] string duration, [Description("Тема голосования"), RemainingText] string topic)
        {
            var timespan = Utility.TimeSpanParse(duration);
            var end = DateTime.Now + timespan;
            var id = RandomString.NextString(6);

            var embed = Utility.GenerateVoteEmbed(ctx.Member, DiscordColor.Yellow, topic, end, 0, 0, 0, id);

            var message = await ctx.Guild.GetChannel(Bot.BotSettings.VotesChannel).SendMessageAsync("@everyone", embed: embed);
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":no_entry:"));

            var vote = new Vote(topic, 0, 0, end, message.Id, ctx.Member.Id, id, new List<ulong>());
            Vote.Save(Bot.BotSettings.VotesXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Голосование запущено!");
        }

        [Command("end")]
        [Description("Прекращает голосование")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task VoteEnd(CommandContext ctx, [Description("ID голосования")] string id)
        {
            foreach (var vote in Vote.Votes.Values)
            {
                if (vote.Id == id)
                {
                    if (vote.End < DateTime.Now)
                    {
                        await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Данное голосование уже завершено!");
                        return;
                    }
                    
                    Vote.Votes[vote.Message].End = DateTime.Now;
                    Vote.Save(Bot.BotSettings.VotesXML);

                    await ctx.RespondAsync(
                        $"{Bot.BotSettings.OkEmoji} Голосование будет остановлено в течение минуты!");
                    return;
                }
            }

            await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не было найдено голосование с указанным ID!");
        }

        [Command("delete")]
        [Description("Безопасно удаляет голосование")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task VoteDelete(CommandContext ctx, [Description("ID голосования")] string id)
        {
            foreach (var vote in Vote.Votes.Values)
            {
                if (vote.Id == id)
                {
                    try
                    {
                        var message = await ctx.Guild.GetChannel(Bot.BotSettings.VotesChannel)
                            .GetMessageAsync(vote.Message);
                        await message.DeleteAsync();
                    }
                    catch (NotFoundException)
                    {
                        
                    }
                    Vote.Votes.Remove(vote.Message);
                    Vote.Save(Bot.BotSettings.VotesXML);

                    await ctx.RespondAsync(
                        $"{Bot.BotSettings.OkEmoji} Голосование было успешно удалено!");
                    return;
                }
            }

            await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не было найдено голосование с указанным ID!");
        }
    }
}