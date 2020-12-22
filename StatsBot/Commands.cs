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
                435377838066237440, // tavern
                725708512121847849, // game questions
                573865939976585226, // help
                679729943185588264, // outfits
                558237431007019010, // admins
                556026966340534273, // chosen
                722157860217421894, // caps
                556442653772742676, // raid
                744944702415175760, // raid rules
            };
            
            var result = new Dictionary<DiscordUser, Data>();
            foreach (var channel in ctx.Guild.Channels.Values)
            { 
                if (!channels.Contains(channel.Id)) continue;
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

                using (var fs = new FileStream("global_stats.csv", FileMode.Create))
                using (var sw = new StreamWriter(fs))
                    foreach (var kv in result)
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

    internal class Data
    {
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