using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;
using static Bot_NetCore.Misc.Utility;

namespace Bot_NetCore.Commands
{
    public class ModerationCommands : BaseCommandModule
    {
        [Command("clearchannel")]
        [Aliases("cc")]
        [RequirePermissions(Permissions.KickMembers)]
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

            var messagesToDelete = await ctx.Channel.GetMessagesBeforeAsync(ctx.Message.Id, messages);

            await ctx.Channel.DeleteMessagesAsync(messagesToDelete);
            await ctx.Channel.DeleteMessageAsync(ctx.Message);

            //Log deleted messages
            Task taskA = new Task(async () =>
            {
                List<string> splitMessages = new List<string>();
                var singleMessage = "";

                var header = "**Удаление сообщений**\n";
                //Группируем сообщения по 2000 символов
                foreach (var msg in messagesToDelete)
                {
                    var message = $"**Автор:** {msg.Author.Username}#{msg.Author.Discriminator} ({msg.Author.Id})\n" +
                                  $"**Канал:** {msg.Channel}\n" +
                                  $"**Содержимое:** ```{msg.Content}```\n";

                    //Проверка на длинну сообщения.
                    if (singleMessage.Length + message.Length >= 2000 - header.Length)
                    {
                        splitMessages.Add(singleMessage);
                        singleMessage = "";
                    }
                    singleMessage += message;
                }
                //Создаём последнее сообщение, если остался текст
                if (singleMessage.Length > 0)
                    splitMessages.Add(singleMessage);

                //Публикуем сгруппированные сообщения раз в секунду
                foreach (var message in splitMessages)
                {
                    Thread.Sleep(1000);

                    await ctx.Guild.GetChannel(Bot.BotSettings.FulllogChannel)
                            .SendMessageAsync(header + message);
                }
            });

            taskA.Start();

            //await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удалено {messages} сообщений из канала!");
        }

        [Command("clearchannelstartfrom")]
        [Aliases("ccsf")]
        [RequirePermissions(Permissions.KickMembers)]
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

            var messagesToDelete = await ctx.Channel.GetMessagesBeforeAsync(startFrom, messages);
            foreach (var message in messagesToDelete) await ctx.Channel.DeleteMessageAsync(message);
            await ctx.Channel.DeleteMessagesAsync(messagesToDelete);
            await ctx.Channel.DeleteMessageAsync(ctx.Message);

            //await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удалено {messages} сообщений из канала!");
        }

        [Command("purge")]
        [RequirePermissions(Permissions.KickMembers)]
        [Priority(1)]
        public async Task Purge(CommandContext ctx, DiscordMember member, string duration, [RemainingText] string reason = "Не указана") //Блокирует возможность принять правила на время
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var id = RandomString.NextString(6);
            var reportEnd = DateTime.Now + TimeSpanParse(duration);
            ReportSQL purge = null;

            //Возможна только одна блокировка, если уже существует то перезаписываем
            var reports = ReportSQL.GetForUser(member.Id, ReportType.CodexPurge);
            if (reports.Any() && reports.First().ReportEnd > DateTime.Now)
            {
                purge = reports.First();
                purge.ReportEnd = reportEnd;
            }
            else
            {
                purge = ReportSQL.Create(id,
                    member.Id,
                    ctx.Member.Id,
                    reason,
                    DateTime.Now,
                    reportEnd,
                    ReportType.CodexPurge);

                if (purge.ReportDuration.TotalSeconds < 1)
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось определить время!");
                    return;
                }
            }

            //Убираем роль правил
            await member.RevokeRoleAsync(ctx.Channel.Guild.GetRole(Bot.BotSettings.CodexRole));
            await member.GrantRoleAsync(ctx.Channel.Guild.GetRole(Bot.BotSettings.PurgeCodexRole));

            //Отправка сообщения в лс
            try
            {
                await member.SendMessageAsync(
                    $"**Еще раз внимательно прочитайте правила сервера**\n\n" +
                    $"**Возможность принять правила заблокирована**\n" +
                    $"**Снятие через** {Utility.FormatTimespan(purge.ReportDuration)}\n" +
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
                 $"**Модератор:** {ctx.Member}\n" +
                 $"**Пользователь:** {member}\n" +
                 $"**Дата:** {DateTime.Now:HH:mm:ss dd.MM.yyyy}\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(purge.ReportDuration)} | {reportEnd:HH:mm:ss dd.MM.yyyy}\n" +
                 $"**Причина:** {reason}\n" +
                 $"**ID:** {id}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно отобрано право принять правила {member.Mention}. " +
                                   $"Снятие через {Utility.FormatTimespan(purge.ReportDuration)}!");
        }

        [Command("purge")]
        [RequirePermissions(Permissions.KickMembers)]
        [Priority(0)]
        public async Task Purge(CommandContext ctx, DiscordUser member, string duration, [RemainingText] string reason = "Не указана") //Блокирует возможность принять правила на время
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var id = RandomString.NextString(6);
            var reportEnd = DateTime.Now + TimeSpanParse(duration);
            ReportSQL purge = null;

            //Возможна только одна блокировка, если уже существует то перезаписываем
            var reports = ReportSQL.GetForUser(member.Id, ReportType.CodexPurge);
            if (reports.Any() && reports.First().ReportEnd > DateTime.Now)
            {
                purge = reports.First();
                purge.ReportEnd = reportEnd;
            }
            else
            {
                purge = ReportSQL.Create(id,
                    member.Id,
                    ctx.Member.Id,
                    reason,
                    DateTime.Now,
                    reportEnd,
                    ReportType.CodexPurge);

                if (purge.ReportDuration.TotalSeconds < 1)
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось определить время!");
                    return;
                }
            }

            //Отправка в журнал
            await ctx.Channel.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Блокировка принятия правил**\n\n" +
                 $"**Модератор:** {ctx.Member}\n" +
                 $"**Пользователь:** {member}\n" +
                 $"**Дата:** {DateTime.Now:HH:mm:ss dd.MM.yyyy}\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(purge.ReportDuration)} | {reportEnd:HH:mm:ss dd.MM.yyyy}\n" +
                 $"**Причина:** {reason}\n" +
                 $"**ID:** {id}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно отобрано право принять правила {member.Mention}. " +
                                   $"Снятие через {Utility.FormatTimespan(purge.ReportDuration)}!");
        }

        [Command("fleetpurge")]
        [Aliases("fp")]
        [Description("Блокирует доступ к каналам рейда. (Для @Капитан Рейда)")]
        [Priority(1)]
        public async Task FleetPurge(CommandContext ctx, DiscordMember member, string duration = "1d", [RemainingText] string reason = "Не указана") //Блокирует возможность принять правила на время
        {
            var isFleetCaptain = ctx.Member.Roles.Contains(ctx.Guild.GetRole(Bot.BotSettings.FleetCaptainRole)) && !Bot.IsModerator(ctx.Member); //Только капитаны рейда, модераторы не учитываются

            //Проверка на модератора или капитана рейда
            if (!Bot.IsModerator(ctx.Member) && !isFleetCaptain)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            //Проверка на кик модератора капитаном рейда
            if (Bot.IsModerator(member) && isFleetCaptain)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не можете исключить данного пользователя! <@&514282258958385152>");
                return;
            }

            var durationTimeSpan = Utility.TimeSpanParse(duration);
            var id = RandomString.NextString(6);

            if (durationTimeSpan.TotalDays > 3 && isFleetCaptain) //Максимальное время блокировки капитанам 1day
                durationTimeSpan = new TimeSpan(3, 0, 0, 0);

            var reportEnd = DateTime.Now + durationTimeSpan;

            ReportSQL fleetPurge = null;
            var reports = ReportSQL.GetForUser(member.Id, ReportType.FleetPurge);
            if (reports.Any() && reports.First().ReportEnd > DateTime.Now)
            {
                fleetPurge = reports.First();
                fleetPurge.ReportEnd = reportEnd;
            }
            else
            {
                fleetPurge = ReportSQL.Create(id,
                    member.Id,
                    ctx.Member.Id,
                    reason,
                    DateTime.Now,
                    reportEnd,
                    ReportType.FleetPurge);

                if (fleetPurge.ReportDuration.TotalSeconds < 1)
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось определить время!");
                    return;
                }
            }

            //Убираем роль правил
            await member.RevokeRoleAsync(ctx.Channel.Guild.GetRole(Bot.BotSettings.FleetCodexRole));

            //Отправка сообщения в лс
            try
            {
                await member.SendMessageAsync(
                    $"**Возможность принять правила заблокирована**\n" +
                    $"**Снятие через:** {Utility.FormatTimespan(fleetPurge.ReportDuration)}\n" +
                    $"**Модератор:** {ctx.Member.Username}#{ctx.Member.Discriminator}\n" +
                    $"**Причина:** {fleetPurge.Reason}");
            }
            catch (UnauthorizedException)
            {
                //user can block the bot
            }

            //Отправка в журнал
            await ctx.Channel.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Блокировка принятия правил рейда**\n\n" +
                 $"**Модератор:** {ctx.Member}\n" +
                 $"**Пользователь:** {member}\n" +
                 $"**Дата:** {DateTime.Now:HH:mm:ss dd.MM.yyyy}\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(fleetPurge.ReportDuration)} | {fleetPurge.ReportEnd::HH:mm:ss dd.MM.yyyy}\n" +
                 $"**Причина:** {reason}\n" +
                $"**ID:** {id}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно отобрано право принять правила рейда {member.Mention}. " +
                                   $"Снятие через: {Utility.FormatTimespan(fleetPurge.ReportDuration)}!");
        }

        [Command("fleetpurge")]
        [Priority(0)]
        public async Task FleetPurge(CommandContext ctx, DiscordUser member, string duration = "1d", [RemainingText] string reason = "Не указана") //Блокирует возможность принять правила на время
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }
            
            var durationTimeSpan = Utility.TimeSpanParse(duration);
            var id = RandomString.NextString(6);
            var reportEnd = DateTime.Now + durationTimeSpan;

            ReportSQL fleetPurge = null;
            var reports = ReportSQL.GetForUser(member.Id, ReportType.FleetPurge);
            if (reports.Any() && reports.First().ReportEnd > DateTime.Now)
            {
                fleetPurge = reports.First();
                fleetPurge.ReportEnd = reportEnd;
            }
            else
            {
                fleetPurge = ReportSQL.Create(id,
                    member.Id,
                    ctx.Member.Id,
                    reason,
                    DateTime.Now,
                    reportEnd,
                    ReportType.FleetPurge);

                if (fleetPurge.ReportDuration.TotalSeconds < 1)
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось определить время!");
                    return;
                }
            }
            
            //Отправка в журнал
            await ctx.Channel.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Блокировка принятия правил рейда**\n\n" +
                 $"**Модератор:** {ctx.Member}\n" +
                 $"**Пользователь:** {member}\n" +
                 $"**Дата:** {DateTime.Now:HH:mm:ss dd.MM.yyyy}\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(fleetPurge.ReportDuration)} | {fleetPurge.ReportEnd::HH:mm:ss dd.MM.yyyy}\n" +
                 $"**Причина:** {reason}\n" +
                $"**ID:** {id}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно отобрано право принять правила рейда {member.Mention}. " +
                                   $"Снятие через: {Utility.FormatTimespan(fleetPurge.ReportDuration)}!");
        }

        [Command("mute")]
        [Aliases("m")]
        [RequirePermissions(Permissions.KickMembers)]
        [Priority(1)]
        public async Task Mute(CommandContext ctx, DiscordMember member, string duration, [RemainingText] string reason = "Не указана")
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var mute = new MemberReport(member.Id,
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
            if (!ReportList.Mutes.ContainsKey(member.Id))
                ReportList.Mutes.Add(member.Id, mute);
            else
                ReportList.Mutes[member.Id].UpdateReport(DateTime.Now,
                    Utility.TimeSpanParse(duration),
                    ctx.Member.Id,
                    reason);

            //Сохраняем в файл
            ReportList.SaveToXML(Bot.BotSettings.ReportsXML);

            //Выдаем роль мута
            await member.GrantRoleAsync(ctx.Channel.Guild.GetRole(Bot.BotSettings.MuteRole));

            //Отправка сообщения в лс
            try
            {
                await member.SendMessageAsync(
                    $"**Вам выдан мут**\n\n" +
                    $"**Снятие через:** {Utility.FormatTimespan(mute.ReportDuration)}\n" +
                    $"**Модератор:** {ctx.Member.Username}#{ctx.Member.Discriminator}\n" +
                    $"**Причина:** {reason}");
            }
            catch (UnauthorizedException)
            {
                //user can block the bot
            }

            //Отправка в журнал
            await ctx.Channel.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Мут**\n\n" +
                 $"**От:** {ctx.Member}\n" +
                 $"**Кому:** {member}\n" +
                 $"**Дата:** {DateTime.Now}\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(mute.ReportDuration)}\n" +
                 $"**Причина:** {reason}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдан мут {member.Mention}. " +
                                   $"Снятие через: {Utility.FormatTimespan(mute.ReportDuration)}!");
        }

        [Command("mute")]
        [RequirePermissions(Permissions.KickMembers)]
        [Priority(1)]
        public async Task Mute(CommandContext ctx, DiscordUser user, string duration, [RemainingText] string reason = "Не указана")
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var mute = new MemberReport(user.Id,
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
            if (!ReportList.Mutes.ContainsKey(user.Id))
                ReportList.Mutes.Add(user.Id, mute);
            else
                ReportList.Mutes[user.Id].UpdateReport(DateTime.Now,
                    Utility.TimeSpanParse(duration),
                    ctx.Member.Id,
                    reason);

            //Сохраняем в файл
            ReportList.SaveToXML(Bot.BotSettings.ReportsXML);

            //Отправка в журнал
            await ctx.Channel.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Мут**\n\n" +
                 $"**От:** {ctx.Member}\n" +
                 $"**Кому:** {user.Username}#{user.Discriminator}\n" +
                 $"**Дата:** {DateTime.Now}\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(mute.ReportDuration)}\n" +
                 $"**Причина:** {reason}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдан мут {user.Username}#{user.Discriminator}. " +
                                   $"Снятие через: {Utility.FormatTimespan(mute.ReportDuration)}!");
        }

        [Command("voicemute")]
        [Aliases("vm")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task VoiceMute(CommandContext ctx, DiscordMember member, string duration, [RemainingText] string reason = "Не указана")
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var voiceMute = new MemberReport(member.Id,
                DateTime.Now,
                Utility.TimeSpanParse(duration),
                ctx.Member.Id,
                reason);

            if (voiceMute.ReportDuration.TotalSeconds < 1)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось определить время!");
                return;
            }

            //Возможна только одна блокировка, если уже существует то перезаписываем
            if (!ReportList.VoiceMutes.ContainsKey(member.Id))
                ReportList.VoiceMutes.Add(member.Id, voiceMute);
            else
                ReportList.VoiceMutes[member.Id].UpdateReport(DateTime.Now,
                    Utility.TimeSpanParse(duration),
                    ctx.Member.Id,
                    reason);

            //Сохраняем в файл
            ReportList.SaveToXML(Bot.BotSettings.ReportsXML);

            //Выдаем роль мута
            await member.GrantRoleAsync(ctx.Channel.Guild.GetRole(Bot.BotSettings.VoiceMuteRole));

            //Отправка сообщения в лс
            try
            {
                await member.SendMessageAsync(
                    $"**Вам выдан мут в голосовом чате**\n\n" +
                    $"**Снятие через:** {Utility.FormatTimespan(voiceMute.ReportDuration)}\n" +
                    $"**Модератор:** {ctx.Member.Username}#{ctx.Member.Discriminator}\n" +
                    $"**Причина:** {reason}");
            }
            catch (UnauthorizedException)
            {
                //user can block the bot
            }

            //Отправка в журнал
            await ctx.Channel.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Мут в голосовом чате**\n\n" +
                 $"**От:** {ctx.Member}\n" +
                 $"**Кому:** {member}\n" +
                 $"**Дата:** {DateTime.Now}\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(voiceMute.ReportDuration)}\n" +
                 $"**Причина:** {reason}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдан мут в голосовом чате {member.Mention}. " +
                                   $"Снятие через: {Utility.FormatTimespan(voiceMute.ReportDuration)}!");
        }

        [Command("warn")]
        [Aliases("w")]
        [RequirePermissions(Permissions.KickMembers)]
        [Priority(1)]
        public async Task Warn(CommandContext ctx, DiscordMember member, [RemainingText] string reason = "Не указана")
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            Warn(ctx.Client, ctx.Member, ctx.Guild, member, reason);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдано предупреждение {member.Mention}!");
        }

        [Command("warn")]
        [RequirePermissions(Permissions.KickMembers)]
        [Priority(0)]
        public async Task Warn(CommandContext ctx, DiscordUser user, [RemainingText] string reason = "Не указана")
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var id = RandomString.NextString(6);

            var message = await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync
            ("**Предупреждение**\n\n" +
             $"**От:** {ctx.User}\n" +
             $"**Кому:** {user.Username}#{user.Discriminator}\n" +
             $"**Дата:** {DateTime.Now}\n" +
             $"**ID предупреждения:** {id}\n" +
             $"**Количество предупреждений:** {WarnSQL.GetForUser(user.Id).Count + 1}\n" +
             $"**Причина:** {reason}");

            WarnSQL.Create(id, user.Id, ctx.Member.Id, reason, DateTime.Now, message.Id);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдано предупреждение {user.Username}#{user.Discriminator}!");
        }

        [Command("unwarn")]
        [Aliases("uw")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task Unwarn(CommandContext ctx, string id)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var warn = WarnSQL.Get(id);

            if (warn == null)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Не найдено предупреждение с таким идентификатором.");
                return;
            }

            WarnSQL.Delete(warn.Id);
            var member = await ctx.Client.GetUserAsync(warn.User);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удалено предупреждение с **{member.Username}#{member.Discriminator}**!");

            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Снятие предупреждения**\n\n" +
                $"**Администратор:** {ctx.Member}\n" +
                $"**Пользователь:** {member}\n" +
                $"**Дата:** {DateTime.Now}\n" +
                $"**ID предупреждения:** {id}\n" +
                $"**Количество предупреждений:** {WarnSQL.GetForUser(member.Id).Count}\n");

            try
            {
                var gMember = await ctx.Guild.GetMemberAsync(member.Id);
                await gMember.SendMessageAsync(
                    $"Администратор **{ctx.Member.Username}** снял ваше предупреждение с ID `{id}`");
            }
            catch (UnauthorizedException)
            {
                //user can block the bot
            }
            catch (NotFoundException)
            {
                //user is not a guild member
            }
        }

        [Command("kick")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task Kick(CommandContext ctx, DiscordMember member, [RemainingText] string reason = "Не указана")
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            Kick(ctx.Member, ctx.Guild, member, reason);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно исключён участник {member.Mention}!");
        }

        [Command("ban")]
        [RequirePermissions(Permissions.KickMembers)]
        [Priority(1)]
        public async Task Ban(CommandContext ctx, DiscordMember member, string duration = "1d", [RemainingText] string reason = "Не указана")
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

            var banDate = DateTime.Now;
            var unbanDate = DateTime.Now.Add(durationTimeSpan);

            var banId = RandomString.NextString(6);

            var ban = BanSQL.Create(banId, member.Id, ctx.Member.Id, reason, banDate, unbanDate);

            var guildMember = await ctx.Guild.GetMemberAsync(member.Id);
            try
            {
                await guildMember.SendMessageAsync(
                    $"Вы были заблокированы на сервере **{ctx.Guild.Name}** на **{Utility.FormatTimespan(durationTimeSpan)}** до **{unbanDate} **. " +
                    $"Модератор: **{ctx.Member.Username}#{ctx.Member.Discriminator}**. **Причина:** {reason}.");
            }
            catch (NotFoundException)
            {
            }
            catch (UnauthorizedException)
            {
                //user can block the bot
            }
            await guildMember.BanAsync();

            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Бан**\n\n" +
                $"**Модератор:** {ctx.Member}\n" +
                $"**Пользователь:** {await ctx.Client.GetUserAsync(member.Id)}\n" +
                $"**Дата:** {DateTime.Now}\n" +
                $"**Разблокировка:** {unbanDate} | {Utility.FormatTimespan(durationTimeSpan)}\n" +
                $"**ID бана:** {banId}\n" +
                $"**Причина:** {reason}\n");

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдан бан **{member.Username}#{member.Discriminator}**! " +
                                   $"Снятие через: {Utility.FormatTimespan(durationTimeSpan)}!");
        }

        [Command("ban")]
        [RequirePermissions(Permissions.KickMembers)]
        [Priority(0)]
        public async Task BanDiscordUser(CommandContext ctx, DiscordUser user, string duration = "1d", [RemainingText] string reason = "Не указана")
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

            var banDate = DateTime.Now;
            var unbanDate = DateTime.Now.Add(durationTimeSpan);

            var banId = RandomString.NextString(6);

            var ban = BanSQL.Create(banId, user.Id, ctx.Member.Id, reason, banDate, unbanDate);

            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Бан**\n\n" +
                $"**Модератор:** {ctx.Member}\n" +
                $"**Пользователь:** {user.Username}\n" +
                $"**Дата:** {DateTime.Now}\n" +
                $"**Разблокировка:** {unbanDate} | {Utility.FormatTimespan(durationTimeSpan)}\n" +
                $"**ID бана:** {banId}\n" +
                $"**Причина:** {reason}\n");

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдан бан **{user.Username}#{user.Discriminator}**! " +
                                   $"Снятие через: {Utility.FormatTimespan(durationTimeSpan)}!");
        }

        [Command("unban")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task Unban(CommandContext ctx, DiscordUser member)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var bans = BanSQL.GetForUser(member.Id);
            if (bans.Count == 0)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не был забанен!");
                return;
            }

            for (int i = 0; i < bans.Count; i++)
            {
                if (bans[i].UnbanDateTime > DateTime.Now)
                {
                    bans[i].UnbanDateTime = DateTime.Now;
                }
            }

            try
            {
                await ctx.Guild.UnbanMemberAsync(member);
            }
            catch
            {
                
            }

            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Снятие бана**\n\n" +
                $"**Модератор:** {ctx.Member}\n" +
                $"**Пользователь:** {await ctx.Client.GetUserAsync(member.Id)}\n" +
                $"**Дата:** {DateTime.Now}\n");

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно снят бан c {member.Mention}!");
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

            await member.RemoveAsync(reason);
            await guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync
            ("**Кик**\n\n" +
             $"**От:** {moderator}\n" +
             $"**Кому:** {member}\n" +
             $"**Дата:** {DateTime.Now}\n" +
             $"**Причина:** {reason}");
        }

        public static async void Warn(DiscordClient client, DiscordMember moderator, DiscordGuild guild,
            DiscordMember member, string reason)
        {
            //if (!UserList.Users.ContainsKey(member.Id)) User.Create(member.Id);

            var id = RandomString.NextString(6);
            var date = DateTime.Now;

            var message = await guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync
            ("**Предупреждение**\n\n" +
             $"**От:** {moderator}\n" +
             $"**Кому:** {member}\n" +
             $"**Дата:** {date}\n" +
             $"**ID предупреждения:** {id}\n" +
             $"**Количество предупреждений:** {WarnSQL.GetForUser(member.Id).Count + 1}\n" +
             $"**Причина:** {reason}");

            //await message.CreateReactionAsync(DiscordEmoji.FromName(client, ":pencil2:"));
            //await message.CreateReactionAsync(DiscordEmoji.FromName(client, ":no_entry:"));
            var warn = WarnSQL.Create(id, member.Id, moderator.Id, reason, date, message.Id);

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
    }

    [Group("whois")]
    [Description("Информация о пользователе.")]
    [RequirePermissions(Permissions.KickMembers)]
    public class WhoisCommand : BaseCommandModule
    {
        [GroupCommand]
        public async Task WhoIs(CommandContext ctx, [Description("Пользователь")] DiscordUser user)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            try
            {
                DiscordMember member = null;
                try
                {
                    member = await ctx.Guild.GetMemberAsync(user.Id);
                }
                catch
                {

                }

                var embed = new DiscordEmbedBuilder();
                embed.WithAuthor($"{user.Username}#{user.Discriminator}", iconUrl: user.AvatarUrl);
                embed.AddField("ID", user.Id.ToString(), true);
                embed.WithColor(DiscordColor.Blurple);

                if (member == null)
                    embed.WithDescription("Покинул сервер");
                else 
                {
                    embed.AddField("Имя на сервере", member.DisplayName, true);
                    embed.AddField("Присоединился", member.JoinedAt.ToString("HH:mm:ss dd.MM.yyyy"));
                }

                //Предупреждения
                var warnings = WarnSQL.GetForUser(user.Id).Count;

                //Emoji используется при выводе списка предупреждений.
                var emoji = DiscordEmoji.FromName(ctx.Client, ":pencil:");

                embed.AddField("Предупреждения", $"{emoji} {warnings}", true);

                if (member != null)
                {
                    //Модератор
                    var moderator = "Нет";
                    if (Bot.IsModerator(member)) moderator = "Да";
                    embed.AddField("Модератор", moderator, true);

                    //Правила
                    var codex = "Не принял";
                    if (member.Roles.Any(x => x.Id == Bot.BotSettings.CodexRole))
                        codex = "Принял";
                    else if (ReportList.CodexPurges.ContainsKey(user.Id))
                        if (ReportList.CodexPurges[user.Id].Expired())
                            codex = "Не принял*";
                        else
                            codex = Utility.FormatTimespan(ReportList.CodexPurges[user.Id].getRemainingTime());
                    embed.AddField("Правила", codex, true);

                    //Правила рейда
                    var fleetCodex = "Не принял";
                    if (member.Roles.Any(x => x.Id == Bot.BotSettings.FleetCodexRole))
                        fleetCodex = "Принял";
                    else if (ReportList.FleetPurges.ContainsKey(user.Id))
                        if (ReportList.FleetPurges[user.Id].Expired())
                            fleetCodex = "Не принял*";
                        else
                            fleetCodex = Utility.FormatTimespan(ReportList.FleetPurges[user.Id].getRemainingTime());
                    embed.AddField("Правила рейда", fleetCodex, true);
                }

                //Мут
                var mute = "Нет";
                if (ReportList.Mutes.ContainsKey(user.Id))
                    if (!ReportList.Mutes[user.Id].Expired())
                        mute = Utility.FormatTimespan(ReportList.Mutes[user.Id].getRemainingTime());
                embed.AddField("Мут", mute, true);
                
                //Донат
                var donate = 0;
                if (Donator.Donators.ContainsKey(user.Id)) donate = Donator.Donators[user.Id].Balance;
                embed.AddField("Донат", donate.ToString(), true);
                
                //Подписка
                var subscription = "Нет";
                if (Subscriber.Subscribers.ContainsKey(user.Id))
                {
                    var subscriber = Subscriber.Subscribers[user.Id];
                    var length = subscriber.SubscriptionEnd - subscriber.SubscriptionStart;

                    subscription =
                        $"{subscriber.SubscriptionEnd:HH:mm:ss dd.MM.yyyy} ({Utility.ToCorrectCase(length, TimeUnit.Days)})";
                }
                embed.AddField("Подписка", subscription, true);

                //Приватный корабль
                var privateShip = "Нет";
                foreach (var ship in ShipList.Ships.Values)
                    foreach (var shipMember in ship.Members.Values)
                        if (shipMember.Type == MemberType.Owner && shipMember.Id == user.Id)
                        {
                            privateShip = ship.Name;
                            break;
                        }
                embed.AddField("Владелец приватного корабля", privateShip);

                //Заметка
                var note = "Отсутствует";
                if (Note.Notes.ContainsKey(user.Id))
                    note = Note.Notes[user.Id].Content;
                embed.AddField("Заметка", note, false);

                embed.WithFooter("(*) Не принял после разблокировки");

                var message = await ctx.RespondAsync(embed: embed.Build());

                //Реакция на вывод сообщения с предупреждениями
                if (warnings > 0)
                {
                    var interactivity = ctx.Client.GetInteractivity();

                    await message.CreateReactionAsync(emoji);

                    var em = await interactivity.WaitForReactionAsync(xe => xe.Emoji == emoji, message, ctx.User, TimeSpan.FromSeconds(60));

                    if (!em.TimedOut)
                    {
                        await ctx.TriggerTypingAsync();

                        var command = $"whois wl {user.Id}";

                        var cmds = ctx.CommandsNext;

                        // Ищем команду и извлекаем параметры.
                        var cmd = cmds.FindCommand(command, out var customArgs);

                        // Создаем фейковый контекст команды.
                        var fakeContext = cmds.CreateFakeContext(ctx.Member, ctx.Channel, command, ctx.Prefix, cmd, customArgs);

                        // Выполняем команду за пользователя.
                        await cmds.ExecuteCommandAsync(fakeContext);
                    }
                    else
                    {
                        await message.DeleteAllReactionsAsync();
                    }

                }
            }
            catch (NotFoundException)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не найден.");
            }
        }

        [Command("wlist")]
        [Aliases("wl")]
        [Description("Выводит список предупреждений.")]
        public async Task WList(CommandContext ctx, [Description("Пользователь")] DiscordUser user)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var warnings = WarnSQL.GetForUser(user.Id);
            int count = warnings.Count;

            if (count == 0)
            {
                await ctx.RespondAsync("*Предупреждения отсутствуют.*");
                return;
            }

            var interactivity = ctx.Client.GetInteractivity();

            List<CustomEmbedField> fields = new List<CustomEmbedField>();

            for (var i = count; i > 0; i--)
            {
                var warn = warnings[i - 1];
                fields.Add(new CustomEmbedField($"*{i}*.", 
                    $"**ID:** {warn.Id}\n**Причина:** {warn.Reason} \n **Выдан:** {await ctx.Client.GetUserAsync(warn.Moderator)} {warn.Date.ToShortDateString()}"));
            }


            var fields_pagination = GeneratePagesInEmbeds(fields, "Список варнов.");

            if (fields_pagination.Count() > 1)
                await interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, fields_pagination, timeoutoverride: TimeSpan.FromMinutes(5));
            else
                await ctx.RespondAsync(embed: fields_pagination.First().Embed);
        }

        [Command("note")]
        [Description("Показать заметку.")]
        public async Task GetNote(CommandContext ctx, [Description("Пользователь")] DiscordUser user)
        {
            if (!Note.Notes.ContainsKey(user.Id))
            {
                await ctx.RespondAsync("У пользователя нет заметки!");
                return;
            }

            var embed = new DiscordEmbedBuilder();

            embed.WithAuthor($"{user.Username}#{user.Discriminator}", iconUrl: user.AvatarUrl);
            embed.WithColor(DiscordColor.Blurple);

            var note = "Отсутствует";
            note = Note.Notes[user.Id].Content;
            embed.AddField("Заметка", note, false);

            await ctx.RespondAsync(embed: embed.Build());
        }

        [Command("addnote")]
        [Description("Добавить заметку.")]
        public async Task AddNote(CommandContext ctx, [Description("Пользователь")] DiscordUser user, [Description("Заметка")] [RemainingText] string content)
        {
            string oldContent = null;
            if (Note.Notes.ContainsKey(user.Id))
                oldContent = Note.Notes[user.Id].Content;

            var note = new Note(user.Id, content);
            Note.Save(Bot.BotSettings.NotesXML);

            var message = $"{Bot.BotSettings.OkEmoji} Успешно добавлена заметка о пользователе!";
            if (oldContent != null) message += " **Предыдущая заметка:** " + oldContent;

            await ctx.RespondAsync(message);
        }

        [Command("deletenote")]
        [Aliases("dnote")]
        [Description("Удалить заметку.")]
        public async Task DeleteNote(CommandContext ctx, [Description("Пользователь")] DiscordUser user)
        {
            if (!Note.Notes.ContainsKey(user.Id))
            {
                await ctx.RespondAsync("У пользователя нет заметки!");
                return;
            }

            Note.Notes.Remove(user.Id);

            await ctx.RespondAsync($"Успешно удалена заметка пользователя <@{user.Id}>!");
        }
    }
}
