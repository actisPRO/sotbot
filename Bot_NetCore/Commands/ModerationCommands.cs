using System;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using SeaOfThieves.Entities;
using SeaOfThieves.Misc;

namespace SeaOfThieves.Commands
{
    public class ModerationCommands
    {
        [Command("clearchannel")]
        [Aliases("cc")]
        [Hidden]
        public async Task ClearChannel(CommandContext ctx, int messages)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            if (messages > 100 || messages < 1)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Допустимое количество сообщений: **от 1 до 100**.");
                return;
            }

            var messagesToDelete = await ctx.Channel.GetMessagesAsync(messages, ctx.Message.Id);
            await ctx.Channel.DeleteMessagesAsync(messagesToDelete);
            await ctx.Channel.DeleteMessageAsync(ctx.Message);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удалено {messages} сообщений из канала!");
        }

        [Command("clearchannelstartfrom")]
        [Aliases("ccsf")]
        [Hidden]
        public async Task ClearChannelStartFrom(CommandContext ctx, ulong startFrom, int messages)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            if (messages > 100 || messages < 1)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Допустимое количество сообщений: **от 1 до 100**.");
                return;
            }

            var messagesToDelete = await ctx.Channel.GetMessagesAsync(messages, startFrom);
            foreach (var message in messagesToDelete) await ctx.Channel.DeleteMessageAsync(message);
            await ctx.Channel.DeleteMessagesAsync(messagesToDelete);
            await ctx.Channel.DeleteMessageAsync(ctx.Message);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удалено {messages} сообщений из канала!");
        }

        [Command("purge")]
        [Aliases("p")]
        [Hidden]
        public async Task Purge(CommandContext ctx, DiscordMember user, string duration, [RemainingText] string reason = "Не указана") //Блокирует возможность принять правила на время
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }
            var purge = new MemberReport(user.Id,
                DateTime.Now,
                Utility.TimeSpanParse(duration),
                ctx.Member.Id,
                reason);

            //Возможна только одна блокировка, если уже существует то перезаписываем
            if (!ReportList.CodexPurges.ContainsKey(user.Id))
                ReportList.CodexPurges.Add(user.Id, purge);
            else
                ReportList.CodexPurges[user.Id].UpdatePurge(DateTime.Now,
                    Utility.TimeSpanParse(duration),
                    ctx.Member.Id,
                    reason);

            //Сохраняем в файл
            ReportList.SaveToXML(Bot.BotSettings.ReportsXML);

            //Убираем роль правил
            await user.RevokeRoleAsync(ctx.Channel.Guild.GetRole(Bot.BotSettings.CodexRole));

            //Отправка сообщения в лс
            try
            {
                await user.SendMessageAsync(
                    $"**Еще раз внимательно прочитайте правила сервера**\n\n" +
                    $"**Возможность принять правила заблокирована**\n" +
                    $"**Снятие через:** {Utility.FormatTimespan(purge.getRemainingTime())}\n" +
                    $"**Модератор:** {ctx.Member.Username}#{ctx.Member.Discriminator}\n" +
                    $"**Причина:** {purge.Reason}");
            }
            catch (UnauthorizedException)
            {
                //user can block the bot
            }

            //Отправка в журнал
            await ctx.Channel.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Блокировка принятия правил**\n\n" +
                 $"**От:** {ctx.Member}\n" +
                 $"**Кому:** {user}\n" +
                 $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(purge.getRemainingTime())}\n" +
                 $"**Причина:** {reason}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно отобрано право принять правила. " +
                                   $"Снятие через: {Utility.FormatTimespan(purge.PurgeDuration)}!");
        }

        //TODO
        [Command("mute")]
        [Aliases("m")]
        [Hidden]
        public async Task Mute(CommandContext ctx, DiscordMember member, string time, [RemainingText] string reason = "Не указана")
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Команда в разработке!");
        }

        [Command("warn")]
        [Aliases("w")]
        [Hidden]
        public async Task Warn(CommandContext ctx, DiscordMember member, [RemainingText] string reason = "Не указана")
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            Warn(ctx.Client, ctx.Member, ctx.Guild, member, reason);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдано предупреждение!");
        }

        [Command("wlist")]
        [Aliases("wl")]
        [Hidden]
        public async Task WList(CommandContext ctx, DiscordUser member)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var count = 0;
            if (!UserList.Users.ContainsKey(member.Id)) count = 0;
            else count = UserList.Users[member.Id].Warns.Count;

            if (count == 0)
            {
                await ctx.RespondAsync("*Предупреждения отсутствуют.*");
                return;
            }

            var response = "*" + member + "*\n";
            for (var i = 1; i <= count; ++i)
            {
                var warn = UserList.Users[member.Id].Warns[i - 1];
                response +=
                    $"**{i}.** {warn.Reason}. **Выдан:** {await ctx.Client.GetUserAsync(warn.Moderator)} {warn.Date}. **ID:** {warn.Id}.\n";
            }

            await ctx.RespondAsync(response);
        }

        [Command("unwarn")]
        [Aliases("uw")]
        [Hidden]
        public async Task Unwarn(CommandContext ctx, DiscordMember member, string id)
        {
            if (!Bot.IsModerator(ctx.Member))
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

            var warnFound = false;
            for (var i = 0; i < UserList.Users[member.Id].Warns.Count; ++i)
                if (UserList.Users[member.Id].Warns[i].Id == id)
                {
                    warnFound = true;
                    break;
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
                "**Снятие предупреждения**\n\n" +
                $"**Администратор:** {ctx.Member}\n" +
                $"**Пользователь:** {member}\n" +
                $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
                $"**ID предупреждения:** {id}\n" +
                $"**Количество предупреждений:** {UserList.Users[member.Id].Warns.Count}\n");

            try
            {
                await member.SendMessageAsync(
                    $"Администратор **{ctx.Member.Username}** снял ваше предупреждение с ID `{id}`");
            }
            catch (UnauthorizedException)
            {
                //user can block the bot
            }
        }

        [Command("kick")]
        [Hidden]
        public async Task Kick(CommandContext ctx, DiscordMember member, [RemainingText] string reason = "Не указана")
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            Kick(ctx.Member, ctx.Guild, member, reason);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно исключён участник!");
        }

        [Command("ban")]
        [Hidden]
        public async Task Ban(CommandContext ctx, DiscordUser member, string duration = "1d", [RemainingText] string reason = "Не указана")
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var durationTimeSpan = Utility.TimeSpanParse(duration);

            var unbanDate = DateTime.Now.ToUniversalTime().Add(durationTimeSpan);

            var banId = RandomString.NextString(6);

            var banned = new BannedUser(member.Id, unbanDate, DateTime.Now.ToUniversalTime(), ctx.Member.Id, reason, banId);
            BanList.SaveToXML(Bot.BotSettings.BanXML);

            try
            {
                var guildMember = await ctx.Guild.GetMemberAsync(member.Id);
                await guildMember.SendMessageAsync(
                    $"Вы были заблокированы на сервере **{ctx.Guild.Name}** на **{Utility.FormatTimespan(durationTimeSpan)}** до **{unbanDate} UTC **. " +
                    $"Модератор: **{ctx.Member.Username}#{ctx.Member.Discriminator}**. **Причина:** {reason}.");
                await guildMember.BanAsync(0, reason); //при входе каждого пользователя будем проверять на наличие бана и кикать по возможности.
            }
            catch (NotFoundException)
            {
            }
            catch (UnauthorizedException)
            {
                //user can block the bot
            }

            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Бан**\n\n" +
                $"**Модератор:** {ctx.Member}\n" +
                $"**Пользователь:** {await ctx.Client.GetUserAsync(member.Id)}\n" +
                $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
                $"**Разблокировка:** {unbanDate} UTC | {Utility.FormatTimespan(durationTimeSpan)}\n" +
                $"**ID бана:** {banId}\n" +
                $"**Причина:** {reason}\n");

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдан бан! " +
                                   $"Снятие через: {Utility.FormatTimespan(durationTimeSpan)}!");
        }

        [Command("unban")]
        [Hidden]
        public async Task Unban(CommandContext ctx, DiscordUser member)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            if (!BanList.BannedMembers.ContainsKey(member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не был забанен!");
            }
            
            var bannedUser = BanList.BannedMembers[member.Id];
            bannedUser.Unban();
            BanList.SaveToXML(Bot.BotSettings.BanXML);
            await ctx.Guild.UnbanMemberAsync(member);

            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Снятие бана**\n\n" +
                $"**Модератор:** {ctx.Member}\n" +
                $"**Пользователь:** {await ctx.Client.GetUserAsync(member.Id)}\n" +
                $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n");

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно снят бан!");
        }

        /// <summary>
        ///     Исключает участника и отправляет уведомление в лог и в ЛС
        /// </summary>
        /// <param name="moderator">Модератор</param>
        /// <param name="guild">Сервер</param>
        /// <param name="member">Исключаемый</param>
        /// <param name="reason">Причина исключения</param>
        public async void Kick(DiscordMember moderator, DiscordGuild guild, DiscordMember member, string reason)
        {
            try
            {
                await member.SendMessageAsync(
                    $"Вы были кикнуты модератором **{moderator.Username}#{moderator.Discriminator}** по причине: {reason}.");
            }
            catch (UnauthorizedException)
            {
                //user can block the bot
            }

            await guild.RemoveMemberAsync(member, reason);
            await guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync
            ("**Кик**\n\n" +
             $"**От:** {moderator}\n" +
             $"**Кому:** {member}\n" +
             $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
             $"**Причина:** {reason}");
        }

        public static async void Warn(DiscordClient client, DiscordMember moderator, DiscordGuild guild,
            DiscordMember member, string reason)
        {
            if (!UserList.Users.ContainsKey(member.Id)) User.Create(member.Id);

            var id = RandomString.NextString(6);

            var message = await guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync
            ("**Предупреждение**\n\n" +
             $"**От:** {moderator}\n" +
             $"**Кому:** {member}\n" +
             $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
             $"**ID предупреждения:** {id}\n" +
             $"**Количество предупреждений:** {UserList.Users[member.Id].Warns.Count + 1}\n" +
             $"**Причина:** {reason}");

            //await message.CreateReactionAsync(DiscordEmoji.FromName(client, ":pencil2:"));
            //await message.CreateReactionAsync(DiscordEmoji.FromName(client, ":no_entry:"));

            UserList.Users[member.Id].AddWarning(moderator.Id, DateTime.Now.ToUniversalTime(), reason, id, message.Id);
            UserList.SaveToXML(Bot.BotSettings.WarningsXML);

            try
            {
                await member.SendMessageAsync("Вы получили предупреждение от модератора " +
                                              $"**{moderator.Username}#{moderator.Discriminator}**. Причина: {reason}. " +
                                              $"ID предупреждения: `{id}`");
            }
            catch (UnauthorizedException)
            {
                //user can block the bot
            }
        }

        /// <summary>
        ///     Проверяет, имеет ли пользователь роли из списка AdminRoles.
        /// </summary>
        /// <param name="member">Проверяемый участник</param>
        /// <returns>Является ли участник модератором</returns>
        /*private bool IsModerator(DiscordMember member)
        {
            foreach (var role in member.Roles)
                if (Bot.GetMultiplySettingsSeparated(Bot.BotSettings.AdminRoles).Contains(role.Id))
                    return true;

            return false;
        }*/
    }
}