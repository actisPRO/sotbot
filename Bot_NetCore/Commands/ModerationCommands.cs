﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot_NetCore.Attributes;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity.Extensions;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bot_NetCore.Commands
{
    [RequireGuild]
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

            var channelMessages = await ctx.Channel.GetMessagesBeforeAsync(ctx.Message.Id, messages);

            var messagesToDelete = channelMessages.Where(x => x.Pinned == false);

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
                                  $"**Содержимое:** ```\u200B{ msg.Content}```\n";

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

            var channelMessages = await ctx.Channel.GetMessagesBeforeAsync(startFrom, messages);

            var messagesToDelete = channelMessages.Where(x => x.Pinned == false);

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
            var reportEnd = DateTime.Now + Utility.TimeSpanParse(duration);
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
            var reportEnd = DateTime.Now + Utility.TimeSpanParse(duration);
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
                "**Блокировка доступа к рейдам**\n\n" +
                 $"**Модератор:** {ctx.Member}\n" +
                 $"**Пользователь:** {member}\n" +
                 $"**Дата:** {DateTime.Now:HH:mm:ss dd.MM.yyyy}\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(purge.ReportDuration)} | {reportEnd:HH:mm:ss dd.MM.yyyy}\n" +
                 $"**Причина:** {reason}\n" +
                 $"**ID:** {id}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно заблокирован доступ к рейдам {member.Mention}. " +
                                   $"Снятие через {Utility.FormatTimespan(purge.ReportDuration)}!");
        }

        [Command("fleetpurge")]
        [Aliases("fp")]
        [Description("Блокирует доступ к каналам рейда.")]
        [RequireCustomRole(RoleType.FleetCaptain)]
        [Priority(1)]
        public async Task FleetPurge(CommandContext ctx, DiscordMember member, string duration = "1d", [RemainingText] string reason = "Не указана") //Блокирует возможность принять правила на время
        {
            var isFleetCaptain = ctx.Member.Roles.Contains(ctx.Guild.GetRole(Bot.BotSettings.FleetCaptainRole)) &&
                !Bot.IsModerator(ctx.Member) && !ctx.Member.Roles.Any(x => x.Id == Bot.BotSettings.HelperRole); //Только капитаны рейда, модераторы и хелперы не учитываются

            //Проверка на кик модератора капитаном рейда
            if (Bot.IsModerator(member) && isFleetCaptain)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не можете исключить данного пользователя! <@&514282258958385152>");
                return;
            }

            var durationTimeSpan = Utility.TimeSpanParse(duration);
            var id = RandomString.NextString(6);

            if (durationTimeSpan.TotalDays > 31 && isFleetCaptain) //Максимальное время блокировки капитанам 1day
                durationTimeSpan = new TimeSpan(31, 0, 0, 0);

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
                    $"**Доступ к рейдам заблокирован**\n" +
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
                "**Блокировка доступа к рейдам**\n\n" +
                 $"**Модератор:** {ctx.Member}\n" +
                 $"**Пользователь:** {member}\n" +
                 $"**Дата:** {DateTime.Now:HH:mm:ss dd.MM.yyyy}\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(fleetPurge.ReportDuration)} | {fleetPurge.ReportEnd::HH:mm:ss dd.MM.yyyy}\n" +
                 $"**Причина:** {reason}\n" +
                $"**ID:** {id}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно заблокирован доступ к рейдам {member.Mention}. " +
                                   $"Снятие через: {Utility.FormatTimespan(fleetPurge.ReportDuration)}!");
        }

        [Command("fleetpurge")]
        [RequireCustomRole(RoleType.FleetCaptain)]
        [Priority(0)]
        public async Task FleetPurge(CommandContext ctx, DiscordUser member, string duration = "1d", [RemainingText] string reason = "Не указана") //Блокирует возможность принять правила на время
        {
            var isFleetCaptain = ctx.Member.Roles.Contains(ctx.Guild.GetRole(Bot.BotSettings.FleetCaptainRole)) &&
                !Bot.IsModerator(ctx.Member) && !ctx.Member.Roles.Any(x => x.Id == Bot.BotSettings.HelperRole); //Только капитаны рейда, модераторы и хелперы не учитываются

            var durationTimeSpan = Utility.TimeSpanParse(duration);
            var id = RandomString.NextString(6);

            if (durationTimeSpan.TotalDays > 31 && isFleetCaptain) //Максимальное время блокировки капитанам 1day
                durationTimeSpan = new TimeSpan(31, 0, 0, 0);

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

            var durationTimeSpan = Utility.TimeSpanParse(duration);
            var id = RandomString.NextString(6);
            var reportEnd = DateTime.Now + durationTimeSpan;

            ReportSQL mute = null;
            var reports = ReportSQL.GetForUser(member.Id, ReportType.Mute);
            if (reports.Any() && reports.First().ReportEnd > DateTime.Now)
            {
                mute = reports.First();
                mute.ReportEnd = reportEnd;
            }
            else
            {
                mute = ReportSQL.Create(id,
                    member.Id,
                    ctx.Member.Id,
                    reason,
                    DateTime.Now,
                    reportEnd,
                    ReportType.Mute);
            }

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
                 $"**Причина:** {reason}\n" +
                 $"**ID:** {id}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдан мут {member.Mention}. " +
                                   $"Снятие через: {Utility.FormatTimespan(mute.ReportDuration)}!");
        }

        [Command("mute")]
        [RequirePermissions(Permissions.KickMembers)]
        [Priority(1)]
        public async Task Mute(CommandContext ctx, DiscordUser member, string duration, [RemainingText] string reason = "Не указана")
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            var durationTimeSpan = Utility.TimeSpanParse(duration);
            var id = RandomString.NextString(6);
            var reportEnd = DateTime.Now + durationTimeSpan;

            ReportSQL mute = null;
            var reports = ReportSQL.GetForUser(member.Id, ReportType.Mute);
            if (reports.Any() && reports.First().ReportEnd > DateTime.Now)
            {
                mute = reports.First();
                mute.ReportEnd = reportEnd;
            }
            else
            {
                mute = ReportSQL.Create(id,
                    member.Id,
                    ctx.Member.Id,
                    reason,
                    DateTime.Now,
                    reportEnd,
                    ReportType.Mute);
            }

            //Отправка в журнал
            await ctx.Channel.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Мут**\n\n" +
                 $"**От:** {ctx.Member}\n" +
                 $"**Кому:** {member}\n" +
                 $"**Дата:** {DateTime.Now}\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(mute.ReportDuration)}\n" +
                 $"**Причина:** {reason}\n" +
                 $"**ID:** {id}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдан мут {member.Mention}. " +
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

            var durationTimeSpan = Utility.TimeSpanParse(duration);
            var id = RandomString.NextString(6);
            var reportEnd = DateTime.Now + durationTimeSpan;

            ReportSQL mute = null;
            var reports = ReportSQL.GetForUser(member.Id, ReportType.VoiceMute);
            if (reports.Any() && reports.First().ReportEnd > DateTime.Now)
            {
                mute = reports.First();
                mute.ReportEnd = reportEnd;
            }
            else
            {
                mute = ReportSQL.Create(id,
                    member.Id,
                    ctx.Member.Id,
                    reason,
                    DateTime.Now,
                    reportEnd,
                    ReportType.VoiceMute);
            }

            //Выдаем роль мута
            await member.GrantRoleAsync(ctx.Channel.Guild.GetRole(Bot.BotSettings.VoiceMuteRole));

            //Отправка сообщения в лс
            try
            {
                await member.SendMessageAsync(
                    $"**Вам выдан мут в голосовом чате**\n\n" +
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
                "**Мут в голосовом чате**\n\n" +
                 $"**От:** {ctx.Member}\n" +
                 $"**Кому:** {member}\n" +
                 $"**Дата:** {DateTime.Now}\n" +
                 $"**Снятие через:** {Utility.FormatTimespan(mute.ReportDuration)}\n" +
                 $"**Причина:** {reason}");

            //Ответное сообщение в чат
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдан мут в голосовом чате {member.Mention}. " +
                                   $"Снятие через: {Utility.FormatTimespan(mute.ReportDuration)}!");
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
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдано предупреждение {member.Mention}! Количество предупреждений: **{WarnSQL.GetForUser(member.Id).Count + 1}**");
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

            var warnsCount = WarnSQL.GetForUser(user.Id).Count + 1;

            var message = await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync
            ("**Предупреждение**\n\n" +
             $"**От:** {ctx.User}\n" +
             $"**Кому:** {user.Username}#{user.Discriminator}\n" +
             $"**Дата:** {DateTime.Now}\n" +
             $"**ID предупреждения:** {id}\n" +
             $"**Количество предупреждений:** {warnsCount}\n" +
             $"**Причина:** {reason}");

            WarnSQL.Create(id, user.Id, ctx.Member.Id, reason, DateTime.Now, message.Id);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдано предупреждение {user.Username}#{user.Discriminator}! Количество предупреждений: **{warnsCount}**");
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
            await guildMember.BanAsync(delete_message_days: 1);

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

        [Command("massiveban")]
        [Aliases("mban")]
        [RequirePermissions(Permissions.KickMembers)]
        [Description("Массовый бан пользователей")]
        [Priority(3)]
        public async Task MassiveBan(CommandContext ctx, [Description("Список пользователей через ,")] string members, string duration = "1d", [RemainingText] string reason = "Не указана")
        {
            List<string> results = new List<string>();
            // get the command service, we need this for sudo purposes
            var cmds = ctx.CommandsNext;

            var membersId = members.Split(',').ToList();

            foreach (var memberId in membersId)
            {
                try
                {
                    var memberIdTrimmed = ulong.Parse(memberId.Trim());
                    var member = await ctx.Guild.GetMemberAsync(memberIdTrimmed);
                    var banCommand = $"ban {member.Id} {duration} {reason}";
                    // retrieve the command and its arguments from the given string
                    var cmd = cmds.FindCommand(banCommand, out var customArgs);

                    // create a fake CommandContext
                    var fakeContext = cmds.CreateFakeContext(ctx.User, ctx.Channel, banCommand, ctx.Prefix, cmd, customArgs);

                    // and perform the sudo
                    await cmds.ExecuteCommandAsync(fakeContext);

                    results.Add($"{Bot.BotSettings.OkEmoji} {memberId}");
                    await Task.Delay(300);
                }
                catch (Exception ex)
                {
                    results.Add($"{Bot.BotSettings.ErrorEmoji} {memberId}");
                    ctx.Client.Logger.LogError(BotLoggerEvents.Commands, $"MassiveBan command: Ошибка при бане пользователя { memberId.Trim()}");
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось забанить пользователя {memberId.Trim()}. Ошибка {ex.Message}");
                }
            }

            var members_pagination = Utility.GeneratePagesInEmbeds(results, $"Массовый бан пользователей.");

            var interactivity = ctx.Client.GetInteractivity();
            if (members_pagination.Count() > 1)
                await interactivity.SendPaginatedMessageAsync(await ctx.Member.CreateDmChannelAsync(), ctx.User, members_pagination, timeoutoverride: TimeSpan.FromMinutes(5));
            else
                await ctx.RespondAsync(embed: members_pagination.First().Embed);
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
            catch { }

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

        [Command("addcaptain")]
        [Description("Выдает роль капитана.")]
        [RequireCustomRole(RoleType.Helper)]
        public async Task AddCaptain(CommandContext ctx, [Description("Пользователь")] DiscordMember member)
        {
            await member.GrantRoleAsync(ctx.Guild.GetRole(Bot.BotSettings.FleetCaptainRole));
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно добавлена роль капитана.");
            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync
                ("**Выдана роль капитана**\n\n" +
                 $"**Модератор:** {ctx.Member}\n" +
                 $"**Кому:** {member}\n" +
                 $"**Дата:** {DateTime.Now}\n");
        }

        [Command("delcaptain")]
        [Description("Убирает роль капитана.")]
        [RequireCustomRole(RoleType.Helper)]
        public async Task DeleteCaptain(CommandContext ctx, [Description("Пользователь")] DiscordMember member)
        {
            await member.RevokeRoleAsync(ctx.Guild.GetRole(Bot.BotSettings.FleetCaptainRole));
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно убрана роль капитана.");
            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync
                ("**Снята роль капитана**\n\n" +
                 $"**Модератор:** {ctx.Member}\n" +
                 $"**Кому:** {member}\n" +
                 $"**Дата:** {DateTime.Now}\n");
        }
    }
}
