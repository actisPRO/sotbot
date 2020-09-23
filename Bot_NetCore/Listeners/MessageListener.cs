using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Bot_NetCore.Misc;
using DSharpPlus.EventArgs;
using Microsoft.VisualBasic.FileIO;

namespace Bot_NetCore.Listeners
{
    public static class MessageListener
    {
        [AsyncListener(EventTypes.MessageDeleted)]
        public static async Task ClientOnMessageDeleted(MessageDeleteEventArgs e)
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
    }
}
