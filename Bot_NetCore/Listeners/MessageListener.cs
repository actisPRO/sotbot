using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using Microsoft.VisualBasic.FileIO;

namespace Bot_NetCore.Listeners
{
    public static class MessageListener
    {
        [AsyncListener(EventTypes.MessageDeleted)]
        public static async Task LogOnMessageDeleted(MessageDeleteEventArgs e)
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

                                    var client = new WebClient();
                                    client.DownloadFile(attachment.Url, file);
                                    await e.Guild.GetChannel(Bot.BotSettings.FulllogChannel)
                                        .SendFileAsync(file, "**Удаление сообщения**\n" +
                                                          $"**Автор:** {e.Message.Author.Username}#{e.Message.Author.Discriminator} ({e.Message.Author.Id})\n" +
                                                          $"**Канал:** {e.Channel}\n" +
                                                          $"**Содержимое: ```{e.Message.Content}```**");
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

        [AsyncListener(EventTypes.MessageCreated)]
        public static async Task LogOnMessageCreated(MessageCreateEventArgs e)
        {
            if (e.Channel.Id == Bot.BotSettings.CodexReserveChannel)
            {
                if (!Bot.IsModerator(await e.Guild.GetMemberAsync(e.Author.Id)))
                    await e.Message.DeleteAsync();

                //Проверка на purge
                if (ReportList.CodexPurges.ContainsKey(e.Author.Id))
                    if (!ReportList.CodexPurges[e.Author.Id].Expired()) //Проверка истекшей блокировки
                    {
                        var moderator = await e.Channel.Guild.GetMemberAsync(ReportList.CodexPurges[e.Author.Id].Moderator);
                        try
                        {
                            await ((DiscordMember)e.Author).SendMessageAsync(
                                "**Возможность принять правила заблокирована**\n" +
                                $"**Снятие через:** {Utility.FormatTimespan(ReportList.CodexPurges[e.Author.Id].getRemainingTime())}\n" +
                                $"**Модератор:** {moderator.Username}#{moderator.Discriminator}\n" +
                                $"**Причина:** {ReportList.CodexPurges[e.Author.Id].Reason}\n");
                        }

                        catch (UnauthorizedException)
                        {
                            //user can block the bot
                        }
                        return;
                    }
                    else
                        ReportList.CodexPurges.Remove(e.Author.Id); //Удаляем блокировку если истекла

                //Выдаем роль правил
                var user = (DiscordMember)e.Author;
                if (!user.Roles.Contains(e.Channel.Guild.GetRole(Bot.BotSettings.CodexRole)))
                {
                    //Выдаем роль правил
                    await user.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.CodexRole));

                    //Убираем роль блокировки правил
                    await user.RevokeRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.PurgeCodexRole));

                    e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                        $"Пользователь {e.Author.Username}#{e.Author.Discriminator} ({e.Author.Id}) подтвердил прочтение правил через сообщение.",
                        DateTime.Now);
                }
            }

            if (e.Message.Attachments.Count > 0 && !e.Message.Author.IsBot)
            {
                var message = $"**Автор:** {e.Message.Author}\n" +
                              $"**Канал:**  {e.Message.Channel}\n" +
                              $"**Сообщение:** {e.Message.Id}\n" +
                              $"**Вложение:**\n";

                using (var client = new WebClient())
                {
                    var attachment = e.Message.Attachments[0]; //проверить: не может быть больше 1 вложения в сообщении
                    var file = $"generated/attachments/{attachment.FileName}";
                    client.DownloadFile(attachment.Url, file);
                    var logMessage = await e.Guild.GetChannel(Bot.BotSettings.AttachmentsLog).SendFileAsync(file, message);
                    File.Delete(file);

                    using (var fs = new FileStream("generated/attachments_messages.csv", FileMode.Append))
                    using (var sw = new StreamWriter(fs))
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
                            Bot.RunCommand((DiscordClient)e.Client, CommandType.Warn, args, e.Message);
                            return;
                        default:
                            return;
                    }
                }
        }
    }
}
