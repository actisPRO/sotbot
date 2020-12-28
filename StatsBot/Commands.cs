using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

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
    }

    internal class ExtendedData
    {
        public ulong Id;
        public string Username;
        public int Messages;
        public int ReactionsReceived;
        public TimeSpan VoiceTime;
        public int Warnings;

        public ExtendedData(Data data)
        {
            Id = data.Id;
            Username = data.Username;
            Messages = data.Messages;
            ReactionsReceived = data.ReactionsReceived;
            VoiceTime = GetHours(data.Id);
            Warnings = GetWarnCount(data.Id);
        }

        private static int GetWarnCount(ulong id)
        {
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    cmd.CommandText = $"SELECT COUNT(id) FROM warnings WHERE user = &ID;";
                    cmd.Parameters.AddWithValue("&ID", id);
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
            return TimeSpan.FromHours(1);
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