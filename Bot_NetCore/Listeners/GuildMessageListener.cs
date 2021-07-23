﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using Renci.SshNet.Security;

namespace Bot_NetCore.Listeners
{
    public static class GuildMessageListener
    {
        /// <summary>
        ///     Словарь, содержащий в качестве ключа пользователя Discord, а в качестве значения - время истечения кулдауна.
        /// </summary>
        public static Dictionary<ulong, DateTime> BlackListCooldowns = new Dictionary<ulong, DateTime>();
        
        [AsyncListener(EventTypes.MessageDeleted)]
        public static async Task LogOnMessageDeleted(DiscordClient client, MessageDeleteEventArgs e)
        {
            if (e.Guild != null)
            {
                if (!Bot.GetMultiplySettingsSeparated(Bot.BotSettings.IgnoredChannels).Contains(e.Channel.Id)
                ) // в лог не должны отправляться сообщения,
                  // удаленные из лога
                    try
                    {

                        //Каналы авто-очистки отправляются в отдельный канал.
                        if (e.Channel.Id == Bot.BotSettings.FindChannel ||
                            e.Channel.Id == Bot.BotSettings.FleetCreationChannel ||
                            e.Channel.Id == Bot.BotSettings.CodexReserveChannel)
                            await e.Guild.GetChannel(Bot.BotSettings.AutoclearLogChannel)
                                .SendMessageAsync("**Удаление сообщения**\n" +
                                                $"**Автор:** {e.Message.Author.Username}#{e.Message.Author.Discriminator} ({e.Message.Author.Id})\n" +
                                                $"**Канал:** {e.Channel}\n" +
                                                $"**Содержимое: ```{e.Message.Content}```**");
                        else
                        {
                            using (TextFieldParser parser = new TextFieldParser("generated/attachments_messages.csv"))
                            {
                                parser.TextFieldType = FieldType.Delimited;
                                parser.SetDelimiters(",");
                                while (!parser.EndOfData)
                                {
                                    string[] fields = parser.ReadFields();
                                    if (Convert.ToUInt64(fields[0]) == e.Message.Id)
                                    {
                                        var attachment =
                                            (await e.Guild.GetChannel(Bot.BotSettings.AttachmentsLog)
                                                .GetMessageAsync(Convert.ToUInt64(fields[1]))).Attachments[0];

                                        var file = $"generated/attachments/{attachment.FileName}";

                                        var wClient = new WebClient();
                                        wClient.DownloadFile(attachment.Url, file);

                                        var message = new DiscordMessageBuilder()
                                        .WithContent("**Удаление сообщения**\n" +
                                                    $"**Автор:** {e.Message.Author.Username}#{e.Message.Author.Discriminator} ({e.Message.Author.Id})\n" +
                                                    $"**Канал:** {e.Channel}\n" +
                                                    $"**Содержимое: ```{e.Message.Content}```**");

                                        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                                        {
                                            message.WithFile(fs);

                                            await e.Guild.GetChannel(Bot.BotSettings.FulllogChannel).SendMessageAsync(message);
                                        }
                                        File.Delete(file);
                                        return;

                                    }
                                }
                            }
                            await e.Guild.GetChannel(Bot.BotSettings.FulllogChannel)
                                .SendMessageAsync("**Удаление сообщения**\n" +
                                                  $"**Автор:** {e.Message.Author.Username}#{e.Message.Author.Discriminator} ({e.Message.Author.Id})\n" +
                                                  $"**Канал:** {e.Channel}\n" +
                                                  $"**Содержимое: ```{e.Message.Content}```**");
                        }
                    }
                    catch (NullReferenceException)
                    {
                        //Ничего не делаем
                    }
            }
        }

        [AsyncListener(EventTypes.MessageCreated)]
        public static async Task LogOnMessageCreated(DiscordClient client, MessageCreateEventArgs e)
        {
            if (e.Guild != null)
            {
                //TODO: REMOVE THIS -> Автобан за гифку
                if (e.Message.Content == "https://media.discordapp.net/attachments/741675549612572793/782183382850600960/not_spoiler.gif" ||
                        e.Message.Content.Contains("https://media.discordapp.net/attachments/741675549612572793/782183382850600960/not_spoiler.gif") ||
                        e.Message.Content.Contains("not_spoiler.gif") ||
                        e.Message.Content.Contains("giant.gfycat.com/NeatPhonyAcornweevil.mp4") ||
                        e.Message.Content.Contains("tornadus.net/orange"))
                {
                    var member = await e.Guild.GetMemberAsync(e.Author.Id);
                    await member.BanAsync(1, "Forbidden content");
                }

                if (e.Channel.Id == Bot.BotSettings.CodexReserveChannel)
                {
                    if (!Bot.IsModerator(await e.Guild.GetMemberAsync(e.Author.Id)))
                        await e.Message.DeleteAsync();

                    //Проверка на purge
                    var hasPurge = false;
                    ReportSQL validPurge = null;
                    foreach (var purge in ReportSQL.GetForUser(e.Message.Author.Id, ReportType.CodexPurge))
                    {
                        if (purge.ReportEnd > DateTime.Now)
                        {
                            validPurge = purge;
                            hasPurge = true;
                            break;
                        }
                    }

                    if (hasPurge)
                    {
                        var moderator = await e.Channel.Guild.GetMemberAsync(validPurge.Moderator);
                        try
                        {
                            await ((DiscordMember)e.Author).SendMessageAsync(
                                "**Возможность принять правила заблокирована**\n" +
                                $"**Снятие через:** {Utility.FormatTimespan(DateTime.Now - validPurge.ReportEnd)}\n" +
                                $"**Модератор:** {moderator.Username}#{moderator.Discriminator}\n" +
                                $"**Причина:** {validPurge.Reason}\n");
                        }

                        catch (UnauthorizedException)
                        {
                            //user can block the bot
                        }
                        return;
                    }

                    //Выдаем роль правил
                    var member = await e.Guild.GetMemberAsync(e.Author.Id);

                    //Проверка времени входа на сервер.
                    if (member.JoinedAt > DateTime.Now.AddMinutes(-10))
                    {
                        try
                        {
                            await member.SendMessageAsync(
                            $"{Bot.BotSettings.ErrorEmoji} Для принятия правил вы должны находиться на сервере минимум " +
                            $"**{Utility.FormatTimespan(TimeSpan.FromMinutes(10))}**.");

                            await e.Message.DeleteReactionAsync(DiscordEmoji.FromName(client, ":white_check_mark:"), member);
                        }
                        catch (UnauthorizedException) { }
                        return;
                    }
                    else if (!member.Roles.Contains(e.Channel.Guild.GetRole(Bot.BotSettings.CodexRole)))
                    {
                        //Выдаем роль правил
                        await member.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.CodexRole));

                        //Убираем роль блокировки правил
                        await member.RevokeRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.PurgeCodexRole));

                        client.Logger.LogInformation(BotLoggerEvents.Event, $"Пользователь {e.Author.Username}#{e.Author.Discriminator} ({e.Author.Id}) подтвердил прочтение правил через сообщение.");
                    }
                }

                if (e.Message.Attachments.Count > 0 && !e.Message.Author.IsBot)
                {
                    var message = new DiscordMessageBuilder()
                        .WithContent($"**Автор:** {e.Message.Author}\n" +
                                    $"**Канал:**  {e.Message.Channel}\n" +
                                    $"**Сообщение:** {e.Message.Id}\n" +
                                    $"**Вложение:**\n");


                    using (var wClient = new WebClient())
                    {
                        var attachment = e.Message.Attachments[0]; //проверить: не может быть больше 1 вложения в сообщении
                        var file = $"generated/attachments/{attachment.FileName}";
                        wClient.DownloadFile(attachment.Url, file);

                        DiscordMessage logMessage;

                        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                        {
                            message.WithFile(fs);

                            logMessage = await e.Guild.GetChannel(Bot.BotSettings.AttachmentsLog).SendMessageAsync(message);
                        }
                        File.Delete(file);

                        using (var fsCsd = new FileStream("generated/attachments_messages.csv", FileMode.Append))
                        using (var sw = new StreamWriter(fsCsd))
                            await sw.WriteLineAsync($"{e.Message.Id},{logMessage.Id}");
                    }
                }

                if (e.Message.Content.StartsWith("> "))
                    if (Bot.IsModerator(await e.Guild.GetMemberAsync(e.Author.Id)))
                    {
                        var messageStrings = e.Message.Content.Split('\n');
                        var command = "";
                        foreach (var str in messageStrings)
                            if (str.StartsWith("<@"))
                            {
                                command = str;
                                break;
                            }

                        var args = command.Split(' ');
                        var receiver = args[0];
                        var action = args[1];

                        switch (action)
                        {
                            case "w":
                                await e.Message.DeleteAsync();
                                Bot.RunCommand(client, CommandType.Warn, args, e.Message);
                                return;
                            default:
                                return;
                        }
                    }

                //Чистка сообщений в канале поиска игроков
                if (e.Channel.Id == Bot.BotSettings.FindChannel &&
                    !VoiceListener.FindChannelInvites.ContainsValue(e.Message.Id)) //Защита от любой другой команды
                {
                    //Проверка если сообщение содержит команду, если нет то отправляем инструкцию в лс.
                    if (!e.Message.Content.StartsWith(Bot.BotSettings.Prefix) && !e.Author.IsBot)
                    {
                        try
                        {
                            await ((DiscordMember)e.Author).SendMessageAsync($"{Bot.BotSettings.ErrorEmoji} Для создания приглашения в свой канал, введите команду `!invite [текст]`\n" +
                                $"Полный гайд в закрепленных сообщениях в канале <#{Bot.BotSettings.FindChannel}>.");
                        }
                        catch { }
                    }

                    var delete = new Timer(5000);
                    delete.Elapsed += async (sender, args) =>
                    {
                        try
                        {
                            if (!e.Message.Pinned && !VoiceListener.FindChannelInvites.ContainsValue(e.Message.Id))
                                await e.Message.DeleteAsync();
                        }
                        catch { }

                        delete.Stop();
                        delete.Close();
                    };
                    delete.Enabled = true;
                }
            }
        }

        [AsyncListener(EventTypes.MessageCreated)]
        public static async Task FilterBannedWords(DiscordClient client, MessageCreateEventArgs e)
        {
            if (e.Guild == null) return;
            if (e.Author.IsBot) return;
            if (((DiscordMember)e.Message.Author).Roles.Any(r => r.Permissions.HasPermission(Permissions.KickMembers))) return;
            if (BlacklistedWordsSQL.Words.Any(w => e.Message.Content.Contains(w)))
            {
                try
                {
                    await e.Message.DeleteAsync();
                }
                catch (Exception) { }

                string msgContent = e.Message.Content;

                BlackListCooldowns = BlackListCooldowns.Where(x => (x.Value - DateTime.Now).Seconds > 0)
                    .ToDictionary(x => x.Key, x => x.Value);

                if (BlackListCooldowns.ContainsKey(e.Author.Id)) return;
        
                BlackListCooldowns[e.Author.Id] = DateTime.Now.AddSeconds(10);

                var modChannel = await client.GetChannelAsync(558237431007019010);

                // get the command service, we need this for sudo purposes
                var cmds = Bot.Commands;

                // retrieve the command and its arguments from the given string
                var cmd = cmds.FindCommand($"mute {e.Message.Author.Id} 2h Автоблокировка.\n**Содержимое: ```{msgContent}```**", out var customArgs);

                // create a fake CommandContext
                var fakeContext = cmds.CreateFakeContext(client.CurrentUser, modChannel, "mute", Bot.BotSettings.Prefix, cmd, customArgs);

                // and perform the sudo
                await cmds.ExecuteCommandAsync(fakeContext);

                await modChannel.SendMessageAsync($"<@&{Bot.BotSettings.ModeratorsRole}> Проверьте сообщение.\n" + 
                                                  $"**Автор:** {e.Author.Username}#{e.Author.Discriminator} ({e.Author.Id}) " +
                                                  $"**Канал:** {e.Channel}\n" +
                                                  $"```{msgContent}```");
            }
        }
    }
}
