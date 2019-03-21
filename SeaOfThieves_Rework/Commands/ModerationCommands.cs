using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using SeaOfThieves.Entities;
using SeaOfThieves.Misc;

namespace SeaOfThieves.Commands
{
    public class ModerationCommands
    {
        [Command("warn"), Aliases("w")]
        [RequirePermissions(Permissions.KickMembers)]
        [Hidden]
        public async Task Warn(CommandContext ctx, DiscordMember member, [RemainingText] string reason = "Не указана")
        {
            if (!UserList.Users.ContainsKey(member.Id))
            {
                User.Create(member.Id);
            }

            var id = RandomString.NextString(12);
            
            UserList.Users[member.Id].AddWarning(ctx.Member.Id, DateTime.Now.ToUniversalTime(), reason, id);
            UserList.SaveToXML(Bot.BotSettings.WarningsXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдано предупреждение!");
            await member.SendMessageAsync($"Вы получили предупреждение от администратора " +
                                          $"**{ctx.Member.Username}#{ctx.Member.Discriminator}**. Причина: {reason}. " +
                                          $"Количество предупреждений: **{UserList.Users[member.Id].Warns.Count}**. ID предупреждения: `{id}`");
            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync
                ($"**Предупреждение**\n\n" +
                 $"**От:** {ctx.Member}\n" +
                 $"**Кому:** {member}\n" +
                 $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
                 $"**ID предупреждения:** {id}\n" +
                 $"**Количество предупреждений:** {UserList.Users[member.Id].Warns.Count}\n" +
                 $"**Причина:** {reason}");
        }

        [Command("unwarn"), Aliases("uw")]
        [RequirePermissions(Permissions.BanMembers)]
        [Hidden]
        public async Task Unwarn(CommandContext ctx, DiscordMember member, string id)
        {
            if (!UserList.Users.ContainsKey(member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У этого участника нет предупреждений!");
                return;
            }

            if (UserList.Users[member.Id].Warns.Count == 0)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У этого участника нет предупреждений!");
                return;
            }

            bool warnFound = false;
            for (int i = 0; i < UserList.Users[member.Id].Warns.Count; ++i)
            {
                if (UserList.Users[member.Id].Warns[i].Id == id)
                {
                    warnFound = true;
                    break;
                }
            }

            if (!warnFound)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Не найдено предупреждение с таким идентификатором.");
                return;
            }
            
            UserList.Users[member.Id].RemoveWarning(id);
            UserList.SaveToXML(Bot.BotSettings.WarningsXML);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удалено предупреждение!");
            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                $"**Снятие предупреждения**\n\n" +
                $"**Администратор:** {ctx.Member}\n" +
                $"**Пользователь:** {member}\n" +
                $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
                $"**ID предупреждения:** {id}\n" +
                $"**Количество предупреждений:** {UserList.Users[member.Id].Warns.Count}\n");
            await member.SendMessageAsync(
                $"Администратор **{ctx.Member.Username}** снял ваше предупреждение с ID `{id}`");
        }
    }
}