using System;
using System.IO;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using SeaOfThieves.Misc;

namespace SeaOfThieves.Commands
{
    [Group("vote")]
    [Aliases("v")]
    [Description("Команды голосований\n" +
                 "!help [Команда] для описания команды")]
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
            
            var embed = new DiscordEmbedBuilder();
            embed.Title = topic;
            embed.AddField("Участники", "0", true);
            embed.AddField("За", "0", true);
            embed.AddField("Против", "0", true);
            embed.WithFooter($"Голосование будет завершено {end.ToString("HH:mm:ss dd.MM.yyyy")}. ID: {id}");

            var message = await ctx.Guild.GetChannel(Bot.BotSettings.VotesChannel).SendMessageAsync(embed: embed);
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":no_entry:"));
            
            var vote = new Vote(topic, end, message.Id, ctx.Member.Id, id);
            Vote.Save(Bot.BotSettings.VotesXML);
        }
    }
}