using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Jitbit.Utils;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.FileIO;

namespace StatsBot
{
    [Group("stats")]
    [Aliases("s")]
    public class Commands : BaseCommandModule
    {
        [Command("global")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Global(CommandContext ctx)
        {
            await ctx.RespondAsync("**Начат сбор статистики со всех каналов**");
            
            var channels = new List<ulong>()
            {
                435486626551037963, // rules
                740544717527449610, // news global
                435730405077811200, // server news
                725708512121847849, // game questions
                573865939976585226, // help
                679729943185588264, // outfits
                558237431007019010, // admins
                556026966340534273, // chosen
                722157860217421894, // caps
                556442653772742676, // raid
                744944702415175760, // raid rules
                435377838066237440, // tavern
            };
            
            var result = new Dictionary<DiscordUser, Data>();
            foreach (var channelId in channels)
            {
                var channel = ctx.Guild.Channels[channelId];
                bool errored = false;
                var channelData = new Dictionary<DiscordUser, Data>();
                await ctx.RespondAsync("Парсинг сообщений в канале " + channel);
                
                // all the messages in the channel

                ulong lastMessageId = channel.LastMessageId;
                var messages = await channel.GetMessagesBeforeAsync(lastMessageId, 100);
                int count = 0;
                bool first = true;
                
                while (messages.Count != 0)
                {
                    count += messages.Count();
                    ctx.Client.Logger.LogInformation($"Parsing channel {channel.Id}: Messages: {count}.");
                    for (int i = first ? 0 : 1; i < messages.Count; ++i)
                    {
                        first = false;
                        var message = messages[i];
                        var author = message.Author;
                
                        if (!result.ContainsKey(author)) result[author] = new Data(author.Username + "#" + author.Discriminator);
                        if (!channelData.ContainsKey(author)) channelData[author] = new Data(author.Username + "#" + author.Discriminator);

                        result[author].Messages += 1;
                        channelData[author].Messages += 1;
                        foreach (var reaction in message.Reactions)
                        {
                            result[author].ReactionsReceived += reaction.Count;
                            channelData[author].ReactionsReceived += reaction.Count;
                        }
                    }
                    
                    lastMessageId = messages.Last().Id;

                    try
                    {
                        messages = await channel.GetMessagesBeforeAsync(lastMessageId, 100);
                    }
                    catch (Exception)
                    {
                        ctx.Client.Logger.LogError("Errored while parsing channel. Stopped at " + count);
                        break;
                    }
                }

                await ctx.RespondAsync($"Завершён парсинг в канале. **Количество сообщений: {count}.**. ");

                using (var fs = new FileStream("global_stats.csv", FileMode.Create))
                using (var sw = new StreamWriter(fs))
                    foreach (var kv in result)
                        await sw.WriteLineAsync(
                            $"\"{kv.Key.Id}\",\"{kv.Value.Username}\",\"{kv.Value.Messages}\",\"{kv.Value.ReactionsReceived}\"");
                
                using (var fs = new FileStream(channel.Id + ".csv", FileMode.Create))
                using (var sw = new StreamWriter(fs))
                    foreach (var kv in channelData)
                        await sw.WriteLineAsync(
                            $"\"{kv.Key.Id}\",\"{kv.Value.Username}\",\"{kv.Value.Messages}\",\"{kv.Value.ReactionsReceived}\"");
            }

            await ctx.RespondAsync("Сбор статистки успешно завершён. Создан файл **global_stats.csv**.");
        }

        [Command("channel")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Channel(CommandContext ctx, ulong channelId)
        {
            var channel = ctx.Guild.Channels[channelId];
            var result = new Dictionary<DiscordUser, Data>();
            await ctx.RespondAsync("Парсинг сообщений в канале " + channel);
                
            // all the messages in the channel

            ulong lastMessageId = channel.LastMessageId;
            var messages = await channel.GetMessagesBeforeAsync(lastMessageId, 100);
            int count = 0;
            while (messages.Count != 0)
            {
                count += messages.Count();
                ctx.Client.Logger.LogInformation($"Parsing channel {channel.Id}: Messages: {count}.");
                foreach (var message in messages)
                {
                    var author = message.Author;
                
                    if (!result.ContainsKey(author)) result[author] = new Data(author.Username + "#" + author.Discriminator);

                    result[author].Messages += 1;
                    foreach (var reaction in message.Reactions)
                        result[author].ReactionsReceived += reaction.Count;
                }
                    
                lastMessageId = messages.Last().Id;
                try
                {
                    messages = await channel.GetMessagesBeforeAsync(lastMessageId, 100);
                }
                catch (Exception)
                {
                    messages = await channel.GetMessagesBeforeAsync(lastMessageId, 95);
                }
            }

            await ctx.RespondAsync($"Завершён парсинг в канале. **Количество сообщений: {count}.**");

            using (var fs = new FileStream(channelId + ".csv", FileMode.Create))
            using (var sw = new StreamWriter(fs))
                foreach (var kv in result)
                    await sw.WriteLineAsync(
                        $"\"{kv.Key.Id}\",\"{kv.Value.Username}\",\"{kv.Value.Messages}\",\"{kv.Value.ReactionsReceived}\"");
        }

        [Command("generate")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Generate(CommandContext ctx)
        {
            ctx.Client.Logger.LogInformation("Generating stats file. . .");
            var extendedData = new Dictionary<ulong, ExtendedData>();
            var members = await ctx.Guild.GetAllMembersAsync();
            using (TextFieldParser parser = new TextFieldParser("global_stats.csv"))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                while (!parser.EndOfData)
                {
                    //Process row
                    try
                    {
                        string[] fields = parser.ReadFields();
                        var data = new Data(fields[1])
                        {
                            Id = Convert.ToUInt64(fields[0]),
                            Username = fields[1],
                            ReactionsReceived = Convert.ToInt32(fields[2]),
                            Messages = Convert.ToInt32(fields[3]),
                            ReactionsGiven = 0
                        };
                        var selectedMembers = from DiscordMember discordMember in members
                            where discordMember.Id == data.Id
                            select discordMember;
                        var member = selectedMembers.Any() ? selectedMembers.First() : null;
                                
                        extendedData[Convert.ToUInt64(fields[0])] = new ExtendedData(data, member);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message + "\n" + e.StackTrace);
                    }
                }
            }
            
            ctx.Client.Logger.LogInformation("Getting data for users without messages. . .");
            
            // get users without messages
            var users = from DiscordMember discordMember in members
                where !extendedData.Keys.Contains(discordMember.Id)
                select discordMember;
            foreach (var user in users)
            {
                var data = new Data(user.Username + "#" + user.Discriminator)
                {
                    Id = user.Id,
                    Messages = 0,
                    ReactionsGiven = 0,
                    ReactionsReceived = 0
                };
                
                extendedData[user.Id] = new ExtendedData(data, user);
            }

            var result = new CsvExport();
            foreach (var user in extendedData.Values)
            {
                result.AddRow();
                result["ID"] = user.Id;
                result["Username"] = user.Username;
                result["Messages"] = user.Messages;
                result["Reactions"] = user.ReactionsReceived;
                result["Voice"] = $"{Math.Floor(user.VoiceTime.TotalHours)}:{user.VoiceTime.Minutes}:{user.VoiceTime.Seconds}";
                result["Warnings"] = user.Warnings;
                result["Days"] = user.Days;
                result["Roles"] = user.Roles;
            }
            result.ExportToFile("global_stats_full.csv");

            var builder = new DiscordMessageBuilder();
            using (var fs = new FileStream("global_stats_full.csv", FileMode.Open))
                builder.WithFile(fs);
            builder.WithContent("Сгенерирован обновленный файл **global_stats_full.csv**!");

            await ctx.RespondAsync(builder);
        }

        [Command("get")]
        public async Task Get(CommandContext ctx)
        {
            if (!Bot.Data.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync(":no_entry: К сожалению, ты присоединился недавно и у нас нет статистики для тебя =(");
                return;
            }

            var memberStats = Bot.Data[ctx.Member.Id];
            var message = $"**:christmas_tree: Привет, {ctx.Member.Mention}! :christmas_tree:**\n";
            TimeSpan time = DateTime.Now - ctx.Member.JoinedAt;
            
            // days
            int days = (int) Math.Round(time.TotalDays);
            string daysStr = days.ToString();
            string daysPart = "";
            if (daysStr.EndsWith("1") && !daysStr.EndsWith("11")) daysPart = $"{daysStr} прекрасный день, проведённый вместе с нами";
            else if ((daysStr.EndsWith("2") || daysStr.EndsWith("3") || daysStr.EndsWith("4")) &&
                     !(daysStr.EndsWith("12") || daysStr.EndsWith("13")
                                              || daysStr.EndsWith("14")))
                daysPart = $"{daysStr} прекрасных дня, проведённых вместе с нами";
            else daysPart = $"{daysStr} прекрасных дней, проведённых вместе с нами";

            message += $"**За {daysPart}, ты:**\n\n";
            
            // messages
            string messageStr = memberStats.Messages.ToString();
            string messagePart = "";
            if (messageStr.EndsWith("1") && !messageStr.EndsWith("11")) messagePart = $"**{messageStr} сообщение**";
            else if ((messageStr.EndsWith("2") || messageStr.EndsWith("3") || messageStr.EndsWith("4")) &&
                     !(messageStr.EndsWith("12") || messageStr.EndsWith("13")
                                                 || messageStr.EndsWith("14")))
                messagePart = $"**{messageStr} сообщения**";
            else messagePart = $"**{messageStr} сообщений**";

            message += $":envelope: Отправил {messagePart}\n";
            
            // reactions
            if (memberStats.ReactionsReceived > 0)
            {
                string rStr = memberStats.ReactionsReceived.ToString();
                string rPart = "";
                if (rStr.EndsWith("1") && !rStr.EndsWith("11")) rPart = $"**{rStr} реакцию** ";
                else if ((rStr.EndsWith("2") || rStr.EndsWith("3") || rStr.EndsWith("4")) &&
                         !(rStr.EndsWith("12") || rStr.EndsWith("13")
                                               || rStr.EndsWith("14")))
                    rPart = $"**{rStr} реакции**";
                else rPart = $"**{rStr} реакций**";

                message += $":smile: Получил {rPart}\n";
            }
            
            // time
            int hours = (int) Math.Floor(memberStats.VoiceTime.TotalHours);
            if (hours > 0)
            {
                string timeStr = hours.ToString();
                string timePart = "";
                if (timeStr.EndsWith("1") && !timeStr.EndsWith("11")) timePart = $"**{timeStr} час**";
                else if ((timeStr.EndsWith("2") || timeStr.EndsWith("3") || timeStr.EndsWith("4")) &&
                         !(timeStr.EndsWith("12") || timeStr.EndsWith("13")
                                                  || timeStr.EndsWith("14")))
                    timePart = $"**{timeStr} часа**";
                else timePart = $"**{timeStr} часов**";

                message += $":microphone2: Провёл {timePart} в голосовых каналах\n";
            }
            
            // roles
            if (ctx.Member.Roles.Any())
            {
                string rStr = ctx.Member.Roles.Count().ToString();
                string rPart = "";
                if (rStr.EndsWith("1") && !rStr.EndsWith("11")) rPart = $"**{rStr} роль**";
                else if ((rStr.EndsWith("2") || rStr.EndsWith("3") || rStr.EndsWith("4")) &&
                         !(rStr.EndsWith("12") || rStr.EndsWith("13")
                                               || rStr.EndsWith("14")))
                    rPart = $"**{rStr} роли**";
                else rPart = $"**{rStr} ролей**";

                message += $":trophy: Заработал {rPart}\n";
            }
            
            // warnings
            if (memberStats.Warnings > 0)
            {
                string rStr = memberStats.Warnings.ToString();
                string rPart = "";
                if (rStr.EndsWith("1") && !rStr.EndsWith("11")) rPart = $"**{rStr} предупреждение**";
                else if ((rStr.EndsWith("2") || rStr.EndsWith("3") || rStr.EndsWith("4")) &&
                         !(rStr.EndsWith("12") || rStr.EndsWith("13")
                                               || rStr.EndsWith("14")))
                    rPart = $"**{rStr} предупреждения**";
                else rPart = $"**{rStr} предупреждений**";

                message += $":bookmark_tabs: И получил {rPart}\n";
            }

            message += "\n**С Новым Годом!**\nhttps://youtu.be/UgU-BduzovM";

            await ctx.Member.SendMessageAsync(message);
            await ctx.RespondAsync($":envelope: Статистика отправлена в ЛС :wink:");
        }
    }

    internal class ExtendedData
    {
        public ulong Id;
        public string Username;
        public int Messages;
        public int ReactionsReceived;
        public TimeSpan VoiceTime;
        public int Warnings;
        public int Days;
        public int Roles;

        public ExtendedData()
        {
            
        }

        public ExtendedData(Data data, DiscordMember member = null)
        {
            Id = data.Id;
            Username = data.Username;
            Messages = data.Messages;
            ReactionsReceived = data.ReactionsReceived;
            VoiceTime = GetHours(data.Id);
            Warnings = GetWarnCount(data.Id);
            Roles = member == null ? 0 : member.Roles.Count();
            Days = member == null ? 0 : GetDays(member);
        }

        private static int GetWarnCount(ulong id)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"SELECT COUNT(id) FROM warnings WHERE user = @ID;";
                    cmd.Parameters.AddWithValue("@ID", id);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        return 0;
                    }
                    else
                    {
                        return reader.GetInt32(0);
                    }
                }
            }
        }

        private TimeSpan GetHours(ulong id)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"SELECT time FROM voice_times WHERE user_id = @ID;";
                    cmd.Parameters.AddWithValue("@ID", id);
                    cmd.Connection = connection;
                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        return TimeSpan.Zero;
                    }
                    else
                    {
                        return reader.GetTimeSpan("time");
                    }
                }
            }
        }

        private int GetDays(DiscordMember member)
        {
            var new_year = new DateTime(DateTime.Today.Year + 1, 1, 1, 0, 0, 0);
            var diff = new_year - member.JoinedAt;

            return diff.Days;
        }
    }

    internal class Data
    {
        public ulong Id;
        public string Username;
        public int Messages = 0;
        public int ReactionsGiven = 0;
        public int ReactionsReceived = 0;

        public Data(string username)
        {
            Username = username;
            Messages = 0;
            ReactionsGiven = 0;
            ReactionsReceived = 0;
        }
    }
}