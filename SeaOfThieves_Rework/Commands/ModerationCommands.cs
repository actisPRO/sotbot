using System;
using System.Threading.Tasks;
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
            if (!IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var messagesToDelete = await ctx.Channel.GetMessagesAsync(messages, ctx.Message.Id);
            foreach (var message in messagesToDelete) await ctx.Channel.DeleteMessageAsync(message);

            await ctx.Channel.DeleteMessageAsync(ctx.Message);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удалено {messages} сообщений из канала!");
        }

        [Command("clearchannelstartfrom")]
        [Aliases("ccsf")]
        [Hidden]
        public async Task ClearChannelStartFrom(CommandContext ctx, ulong startFrom, int messages)
        {
            if (!IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var messagesToDelete = await ctx.Channel.GetMessagesAsync(messages, startFrom);
            foreach (var message in messagesToDelete) await ctx.Channel.DeleteMessageAsync(message);

            await ctx.Channel.DeleteMessageAsync(ctx.Message);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удалено {messages} сообщений из канала!");
        }

        [Command("warn")]
        [Aliases("w")]
        [Hidden]
        public async Task Warn(CommandContext ctx, DiscordMember member, [RemainingText] string reason = "Не указана")
        {
            if (!IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            if (!UserList.Users.ContainsKey(member.Id)) User.Create(member.Id);

            var id = RandomString.NextString(6);

            UserList.Users[member.Id].AddWarning(ctx.Member.Id, DateTime.Now.ToUniversalTime(), reason, id);
            UserList.SaveToXML(Bot.BotSettings.WarningsXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдано предупреждение!");

            try
            {
                await member.SendMessageAsync("Вы получили предупреждение от модератора " +
                                              $"**{ctx.Member.Username}#{ctx.Member.Discriminator}**. Причина: {reason}. " +
                                              $"ID предупреждения: `{id}`");
            }
            catch (UnauthorizedException)
            {
                //user can block the bot
            }
            
            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync
            ("**Предупреждение**\n\n" +
             $"**От:** {ctx.Member}\n" +
             $"**Кому:** {member}\n" +
             $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
             $"**ID предупреждения:** {id}\n" +
             $"**Количество предупреждений:** {UserList.Users[member.Id].Warns.Count}\n" +
             $"**Причина:** {reason}");
        }

        [Command("wlist")]
        [Aliases("wl")]
        [Hidden]
        public async Task WList(CommandContext ctx, DiscordUser member)
        {
            if (!IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var count = 0;
            if (!UserList.Users.ContainsKey(member.Id)) count = 0; else count = UserList.Users[member.Id].Warns.Count;

            if (count == 0)
            {
                await ctx.RespondAsync("*Предупреждения отсутствуют.*");
                return;
            }

            string response = "*" + member + "*\n"; 
            for (int i = 1; i <= count; ++i)
            {
                var warn = UserList.Users[member.Id].Warns[i - 1];
                response += $"**{i}.** {warn.Reason}. **Выдан:** {await ctx.Client.GetUserAsync(warn.Moderator)} {warn.Date}. **ID:** {warn.Id}.\n";
            }

            await ctx.RespondAsync(response);
        }

        [Command("unwarn")]
        [Aliases("uw")]
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

        [Command("ban")]
        public async Task Ban(CommandContext ctx, DiscordUser member, int mins, int hours = 0, int days = 0,
            [RemainingText] string reason = "Не указана")
        {
            if (!IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var unbanDate = DateTime.Now.ToUniversalTime();
            
            unbanDate = unbanDate.AddMinutes(mins);
            unbanDate = unbanDate.AddHours(hours);
            unbanDate = unbanDate.AddDays(days);

            var banId = RandomString.NextString(6);
            
            var banned = new BannedUser(member.Id, unbanDate, DateTime.Now, ctx.Member.Id, reason, banId);
            BanList.SaveToXML(Bot.BotSettings.BanXML);

            try
            {
                var guildMember = await ctx.Guild.GetMemberAsync(member.Id);
                await guildMember.SendMessageAsync(
                    $"Вы были заблокированы на сервере **{ctx.Guild.Name}** до **{unbanDate} UTC**. " +
                    $"Модератор: **{ctx.Member.Username}#{ctx.Member.Discriminator}**. **Причина:** {reason}.");
                await guildMember.RemoveAsync("Banned: " +
                                              reason); //при входе каждого пользователя будем проверять на наличие бана и кикать по возможности.
            }
            catch (NotFoundException)
            {
                
            }
            catch (UnauthorizedException)
            {
                //user can block the bot
            }

            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                $"**Бан**\n\n" +
                $"**Модератор:** {ctx.Member}\n" +
                $"**Пользователь:** {await ctx.Client.GetUserAsync(member.Id)}\n" +
                $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
                $"**Разблокировка:** {unbanDate} UTC\n" +
                $"**ID бана:** {banId}\n" +
                $"**Причина:** {reason}\n");

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдан бан!");
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

        /// <summary>
        ///     Проверяет, имеет ли пользователь роли из списка AdminRoles.
        /// </summary>
        /// <param name="member">Проверяемый участник</param>
        /// <returns>Является ли участник модератором</returns>
        private bool IsModerator(DiscordMember member)
        {
            foreach (var role in member.Roles)
                if (Bot.GetMultiplySettingsSeparated(Bot.BotSettings.AdminRoles).Contains(role.Id))
                    return true;

            return false;
        }
    }
}