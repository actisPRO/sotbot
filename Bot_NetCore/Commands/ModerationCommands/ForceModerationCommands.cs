using System;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using SeaOfThieves.Entities;
using SeaOfThieves.Misc;

namespace SeaOfThieves.Commands
{
    [Group("force")]
    [Aliases("f")]
    [Description("Модерация участников покинувших сервер")]
    [RequirePermissions(Permissions.KickMembers)]
    public class ForceModerationCommands
    {
        [Command("ban")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task Ban(CommandContext ctx, ulong memberId, string duration = "1d", [RemainingText] string reason = "Не указана")
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var durationTimeSpan = Utility.TimeSpanParse(duration);

            if (durationTimeSpan.TotalSeconds < 1)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось определить время!");
                return;
            }

            var unbanDate = DateTime.Now.ToUniversalTime().Add(durationTimeSpan);

            var banId = RandomString.NextString(6);

            var banned = new BannedUser(memberId, unbanDate, DateTime.Now.ToUniversalTime(), ctx.Member.Id, reason, banId);
            BanList.SaveToXML(Bot.BotSettings.BanXML);

            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Бан**\n\n" +
                $"**Модератор:** {ctx.Member}\n" +
                $"**Пользователь:** {memberId}\n" +
                $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
                $"**Разблокировка:** {unbanDate} UTC | {Utility.FormatTimespan(durationTimeSpan)}\n" +
                $"**ID бана:** {banId}\n" +
                $"**Причина:** {reason}\n");

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдан бан <@{memberId}>! " +
                                   $"Снятие через: {Utility.FormatTimespan(durationTimeSpan)}!");
        }

        [Command("purge")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task Purge(CommandContext ctx, ulong memberId, string duration, [RemainingText] string reason = "Не указана") //Блокирует возможность принять правила на время
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var purge = new MemberReport(memberId,
                DateTime.Now,
                Utility.TimeSpanParse(duration),
                ctx.Member.Id,
                reason);

            if (purge.ReportDuration.TotalSeconds < 1)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось определить время!");
                return;
            }

            //Возможна только одна блокировка, если уже существует то перезаписываем
            if (!ReportList.CodexPurges.ContainsKey(memberId))
                ReportList.CodexPurges.Add(memberId, purge);
            else
                ReportList.CodexPurges[memberId].UpdateReport(DateTime.Now,
                    Utility.TimeSpanParse(duration),
                    ctx.Member.Id,
                    reason);

            //Сохраняем в файл
            ReportList.SaveToXML(Bot.BotSettings.ReportsXML);

            //Отправка в журнал
            await ctx.Channel.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Блокировка принятия правил**\n\n" +
                 $"**От:** {ctx.Member}\n" +
                 $"**Кому:** {memberId}\n" +
                 $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(purge.ReportDuration)}\n" +
                 $"**Причина:** {reason}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно отобрано право принять правила <@{memberId}>. " +
                                   $"Снятие через: {Utility.FormatTimespan(purge.ReportDuration)}!");
        }

        [Command("fleetpurge")]
        [Aliases("fp")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task FleetPurge(CommandContext ctx, ulong memberId, string duration = "1d", [RemainingText] string reason = "Не указана") //Блокирует возможность принять правила на время
        {
            //Проверка на модератора или капитана рейда
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var durationTimeSpan = Utility.TimeSpanParse(duration);

            var fleetPurge = new MemberReport(memberId,
                DateTime.Now,
                durationTimeSpan,
                ctx.Member.Id,
                reason);

            //Возможна только одна блокировка, если уже существует то перезаписываем
            if (!ReportList.FleetPurges.ContainsKey(memberId))
                ReportList.FleetPurges.Add(memberId, fleetPurge);
            else
                ReportList.FleetPurges[memberId].UpdateReport(DateTime.Now,
                    durationTimeSpan,
                    ctx.Member.Id,
                    reason);

            //Сохраняем в файл
            ReportList.SaveToXML(Bot.BotSettings.ReportsXML);

            //Отправка в журнал
            await ctx.Channel.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Блокировка принятия правил рейда**\n\n" +
                 $"**От:** {ctx.Member}\n" +
                 $"**Кому:** {memberId}\n" +
                 $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(fleetPurge.ReportDuration)}\n" +
                 $"**Причина:** {reason}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно отобрано право принять правила рейда <@{memberId}>. " +
                                   $"Снятие через: {Utility.FormatTimespan(fleetPurge.ReportDuration)}!");
        }

        [Command("mute")]
        [Aliases("m")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task Mute(CommandContext ctx, ulong memberId, string duration, [RemainingText] string reason = "Не указана")
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var mute = new MemberReport(memberId,
                DateTime.Now,
                Utility.TimeSpanParse(duration),
                ctx.Member.Id,
                reason);

            if (mute.ReportDuration.TotalSeconds < 1)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось определить время!");
                return;
            }

            //Возможна только одна блокировка, если уже существует то перезаписываем
            if (!ReportList.Mutes.ContainsKey(memberId))
                ReportList.Mutes.Add(memberId, mute);
            else
                ReportList.Mutes[memberId].UpdateReport(DateTime.Now,
                    Utility.TimeSpanParse(duration),
                    ctx.Member.Id,
                    reason);

            //Сохраняем в файл
            ReportList.SaveToXML(Bot.BotSettings.ReportsXML);

            //Отправка в журнал
            await ctx.Channel.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Мут**\n\n" +
                 $"**От:** {ctx.Member}\n" +
                 $"**Кому:** {memberId}\n" +
                 $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(mute.ReportDuration)}\n" +
                 $"**Причина:** {reason}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдан мут <@{memberId}>. " +
                                   $"Снятие через: {Utility.FormatTimespan(mute.ReportDuration)}!");
        }

        [Command("warn")]
        [Aliases("w")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task Warn(CommandContext ctx, ulong memberId, [RemainingText] string reason = "Не указана")
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            if (!UserList.Users.ContainsKey(memberId)) User.Create(memberId);

            var id = RandomString.NextString(6);

            var message = await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync
            ("**Предупреждение**\n\n" +
             $"**От:** {ctx.User}\n" +
             $"**Кому:** {memberId}\n" +
             $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n" +
             $"**ID предупреждения:** {id}\n" +
             $"**Количество предупреждений:** {UserList.Users[memberId].Warns.Count + 1}\n" +
             $"**Причина:** {reason}");

            UserList.Users[memberId].AddWarning(ctx.User.Id, DateTime.Now.ToUniversalTime(), reason, id, message.Id);
            UserList.SaveToXML(Bot.BotSettings.WarningsXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдано предупреждение <@{memberId}>!");
        }
    }
}
