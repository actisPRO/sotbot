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
        [Hidden]
        public async Task Warn(CommandContext ctx, DiscordMember member, [RemainingText] string reason = "Не указана")
        {
            if (!IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }
            
            if (!UserList.Users.ContainsKey(member.Id))
            {
                User.Create(member.Id);
            }

            var id = RandomString.NextString(12);
            
            UserList.Users[member.Id].AddWarning(ctx.Member.Id, DateTime.Now.ToUniversalTime(), reason, id);
            UserList.SaveToXML(Bot.BotSettings.WarningsXML);
            
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдано предупреждение!");
            await member.SendMessageAsync($"Вы получили предупреждение от модератора " +
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
        [Hidden]
        public async Task Unwarn(CommandContext ctx, DiscordMember member, string id)
        {
            if (!IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }
            
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

        [Command("kick")]
        public async Task Kick(CommandContext ctx, DiscordMember member, [RemainingText] string reason = "Не указана")
        {
            if (!IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            Kick(ctx.Member, ctx.Guild, member, reason);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно исключён участник!");
        }
        
        /// <summary>
        /// Исключает участника и отправляет уведомление в лог и в ЛС
        /// </summary>
        /// <param name="moderator">Модератор</param>
        /// <param name="guild">Сервер</param>
        /// <param name="member">Исключаемый</param>
        /// <param name="reason">Причина исключения</param>
        public async void Kick(DiscordMember moderator, DiscordGuild guild, DiscordMember member, string reason)
        {
            await member.SendMessageAsync(
                $"Вы были кикнуты модератором **{moderator.Username}#{moderator.Discriminator}** по причине: {reason}.");
            await guild.RemoveMemberAsync(member, reason);
            await guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync
            ($"**Кик**\n\n" +
             $"**От:** {moderator}\n" +
             $"**Кому:** {member}\n" +
             $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
             $"**Причина:** {reason}");
        }
        
        /// <summary>
        /// Проверяет, имеет ли пользователь роли из списка AdminRoles.
        /// </summary>
        /// <param name="member">Проверяемый участник</param>
        /// <returns>Является ли участник модератором</returns>
        private bool IsModerator(DiscordMember member)
        {
            foreach (var role in member.Roles)
            {
                if (Bot.GetMultiplySettingsSeparated(Bot.BotSettings.AdminRoles).Contains(role.Id))
                {
                    return true;
                }
            }

            return false;
        }
    }
}