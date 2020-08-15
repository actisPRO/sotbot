using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using SeaOfThieves.Misc;

namespace SeaOfThieves.Commands
{
    [Group("vote")]
    [Aliases("v")]
    [Description("Команды голосований.")]
    [RequirePermissions(Permissions.KickMembers)]
    public class VotingCommands : BaseCommandModule
    {
        [Command("start")]
        [Description("Начинает голосование за/против")]
        public async Task VoteStart(CommandContext ctx, string duration, [RemainingText] string topic)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var timespan = Utility.TimeSpanParse(duration);
            var end = DateTime.Now + timespan;
            var id = RandomString.NextString(6);

            var embed = Utility.GenerateVoteEmbed(ctx.Member, DiscordColor.Yellow, topic, end, 0, 0, 0, id);

            var message = await ctx.Guild.GetChannel(Bot.BotSettings.VotesChannel).SendMessageAsync(embed: embed);
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":no_entry:"));

            var vote = new Vote(topic, 0, 0, end, message.Id, ctx.Member.Id, id, new List<ulong>());
            Vote.Save(Bot.BotSettings.VotesXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Голосование запущено!");
        }
    }
}