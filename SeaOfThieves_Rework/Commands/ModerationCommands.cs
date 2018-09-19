using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using SeaOfThieves.Entities;

namespace SeaOfThieves.Commands
{
    public class ModerationCommands
    {
        [Command("warn"), Aliases("w")]
        [RequirePermissions(Permissions.BanMembers)]
        [Hidden]
        public async Task Warn(CommandContext ctx, DiscordMember member, [RemainingText] string reason = "Не указана")
        {
            if (!UserList.Users.ContainsKey(member.Id))
            {
                User.Create(member.Id);
            }
            
            UserList.Users[member.Id].AddWarning(ctx.Member.Id, DateTime.Now.ToUniversalTime(), reason);
            UserList.SaveToXML(Bot.BotSettings.WarningsXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдано предупреждение!");
            await member.SendMessageAsync($"Вы получили предупреждение от администратора " +
                                          $"**{ctx.Member.Username}#{ctx.Member.Discriminator}**. Причина: {reason}. " +
                                          $"Количество предупреждений: **{UserList.Users[member.Id].Warns.Count}**");
            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync
                ($"**Предупреждение**\n\n" +
                 $"**От:** {ctx.Member}\n" +
                 $"**Кому:** {member} ({member.Id})\n" +
                 $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
                 $"**Количество предупреждений:** {UserList.Users[member.Id].Warns.Count}\n" +
                 $"**Причина:** {reason}");
        }
    }
}