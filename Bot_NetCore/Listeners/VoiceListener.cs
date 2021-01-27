using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace Bot_NetCore.Listeners
{
    public static class VoiceListener
    {
        /// <summary>
        ///     Словарь, содержащий в качестве ключа пользователя Discord, а в качестве значения - время истечения кулдауна.
        /// </summary>
        public static Dictionary<DiscordUser, DateTime> ShipCooldowns = new Dictionary<DiscordUser, DateTime>();

        /// <summary>
        ///     Словарь, содержащий в качестве ключа id голосового канала, а в качестве значения - id сообщения в поиске игроков.
        /// </summary>
        public static Dictionary<ulong, ulong> FindChannelInvites = new Dictionary<ulong, ulong>();

        /// <summary>
        ///     Словарь, содержащий в качестве ключа пользователя Discord, а в качестве значения - время входа в канал.
        /// </summary>
        public static Dictionary<ulong, DateTime> VoiceTimeCounters = new Dictionary<ulong, DateTime>();

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task CreateOnVoiceStateUpdated(DiscordClient client, VoiceStateUpdateEventArgs e)
        {
            try
            {
                if (e.Channel != null)
                    if (e.Channel.Id == Bot.BotSettings.AutocreateGalleon ||
                        e.Channel.Id == Bot.BotSettings.AutocreateBrigantine ||
                        e.Channel.Id == Bot.BotSettings.AutocreateSloop
                    ) // мы создаем канал, если пользователь зашел в один из каналов автосоздания
                    {
                        if (ShipCooldowns.ContainsKey(e.User)) // проверка на кулдаун
                            if ((ShipCooldowns[e.User] - DateTime.Now).Seconds > 0)
                            {
                                var m = await e.Guild.GetMemberAsync(e.User.Id);
                                await m.PlaceInAsync(e.Guild.GetChannel(Bot.BotSettings.WaitingRoom));
                                await m.SendMessageAsync($"{Bot.BotSettings.ErrorEmoji} Вам нужно подождать " +
                                                         $"**{(ShipCooldowns[e.User] - DateTime.Now).Seconds}** секунд прежде чем " +
                                                         "создавать новый корабль!");
                                client.Logger.LogInformation(BotLoggerEvents.Event, $"Участник {e.User.Username}#{e.User.Discriminator} ({e.User.Discriminator}) был перемещён в комнату ожидания.");
                                return;
                            }

                        // если проверка успешно пройдена, добавим пользователя
                        // в словарь кулдаунов
                        ShipCooldowns[e.User] = DateTime.Now.AddSeconds(Bot.BotSettings.FastCooldown);

                        //Проверка на эмиссарство
                        var channelSymbol = Bot.BotSettings.AutocreateSymbol;

                        var member = await e.Guild.GetMemberAsync(e.User.Id);

                        member.Roles.ToList().ForEach(x =>
                        {
                            if (x.Id == Bot.BotSettings.EmissaryGoldhoadersRole)
                                channelSymbol = DiscordEmoji.FromName(client, ":moneybag:");
                            else if (x.Id == Bot.BotSettings.EmissaryTradingCompanyRole)
                                channelSymbol = DiscordEmoji.FromName(client, ":pig:");
                            else if (x.Id == Bot.BotSettings.EmissaryOrderOfSoulsRole)
                                channelSymbol = DiscordEmoji.FromName(client, ":skull:");
                            else if (x.Id == Bot.BotSettings.EmissaryAthenaRole)
                                channelSymbol = DiscordEmoji.FromName(client, ":gem:");
                            else if (x.Id == Bot.BotSettings.EmissaryReaperBonesRole)
                                channelSymbol = DiscordEmoji.FromName(client, ":skull_crossbones:");
                            else if (x.Id == Bot.BotSettings.HuntersRole)
                                channelSymbol = DiscordEmoji.FromName(client, ":fish:");
                            else if (x.Id == Bot.BotSettings.ArenaRole)
                                channelSymbol = DiscordEmoji.FromName(client, ":crossed_swords:");

                        });

                        var autoCreateSloopCategory = e.Guild.GetChannel(Bot.BotSettings.AutocreateSloopCategory);
                        var autoCreateBrigantineCategory = e.Guild.GetChannel(Bot.BotSettings.AutocreateBrigantineCategory);
                        var autoCreateGalleongCategory = e.Guild.GetChannel(Bot.BotSettings.AutocreateGalleonCategory);

                        //Генерируем создание канала
                        var used_names = autoCreateSloopCategory.Children.Select(x => x.Name).ToArray();
                        used_names.Concat(autoCreateBrigantineCategory.Children.Select(x => x.Name).ToArray());
                        used_names.Concat(autoCreateGalleongCategory.Children.Select(x => x.Name).ToArray());

                        var generatedName = ShipNames.GenerateChannelName(used_names);
                        var channelName = $"{channelSymbol} {generatedName}";

                        DiscordChannel created = null;

                        if (!Bot.ShipNamesStats.ContainsKey(generatedName)) // create a key-value pair for a new ship name
                            Bot.ShipNamesStats[generatedName] = new[] {0, 0, 0};

                        if (e.Channel.Id == Bot.BotSettings.AutocreateSloop)
                        {
                            Bot.ShipNamesStats[generatedName][0]++;
                            created = await e.Guild.CreateVoiceChannelAsync(
                                channelName, autoCreateSloopCategory,
                                bitrate: Bot.BotSettings.Bitrate, user_limit: 2);
                        }
                        else if (e.Channel.Id == Bot.BotSettings.AutocreateBrigantine)
                        {
                            Bot.ShipNamesStats[generatedName][1]++;
                            created = await e.Guild.CreateVoiceChannelAsync(
                                channelName, autoCreateBrigantineCategory,
                                bitrate: Bot.BotSettings.Bitrate, user_limit: 3);
                        }
                        else
                        {
                            Bot.ShipNamesStats[generatedName][2]++;
                            created = await e.Guild.CreateVoiceChannelAsync(
                                channelName, autoCreateGalleongCategory,
                                bitrate: Bot.BotSettings.Bitrate, user_limit: 4);
                        }
                        
                        FastShipStats.WriteToFile(Bot.ShipNamesStats, "generated/stats/ship_names.csv");

                        await member.PlaceInAsync(created);

                        client.Logger.LogInformation(BotLoggerEvents.Event, $"Участник {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) создал канал через автосоздание." +
                            $" Каналов в категории: {created.Parent.Children.Count()}");
                    }
            }
            catch (NullReferenceException) // исключение выбрасывается если пользователь покинул канал
            {
                // нам здесь ничего не надо делать, просто пропускаем
            }
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task FindOnVoiceStateUpdated(DiscordClient client, VoiceStateUpdateEventArgs e)
        {
            if (e.Channel != null &&
                e.Channel.Id == Bot.BotSettings.FindShip)
            {
                var shipCategories = new[] {
                    e.Guild.GetChannel(Bot.BotSettings.AutocreateSloopCategory),
                    e.Guild.GetChannel(Bot.BotSettings.AutocreateBrigantineCategory),
                    e.Guild.GetChannel(Bot.BotSettings.AutocreateGalleonCategory)
                };

                var possibleChannels = new List<DiscordChannel>();
                foreach(var shipCategory in shipCategories)
                    foreach (var ship in shipCategory.Children)
                        if (ship.Users.Count() < ship.UserLimit && FindChannelInvites.ContainsKey(ship.Id))
                            possibleChannels.Add(ship);
                        

                var m = await e.Guild.GetMemberAsync(e.User.Id);
                if (possibleChannels.Count == 0)
                {
                    await m.PlaceInAsync(e.Guild.GetChannel(Bot.BotSettings.WaitingRoom));
                    await m.SendMessageAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось найти подходящий корабль.");
                    return;
                }

                var random = new Random();
                var rShip = random.Next(0, possibleChannels.Count);

                await m.PlaceInAsync(possibleChannels[rShip]);
                client.Logger.LogInformation(BotLoggerEvents.Event, $"Пользователь {m.Username}#{m.Discriminator} успешно воспользовался поиском корабля!");
                return;
            }
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task PrivateOnVoiceStateUpdated(DiscordClient client, VoiceStateUpdateEventArgs e)
        {
            if (e.Channel != null &&
                e.Channel.ParentId == Bot.BotSettings.PrivateCategory)
                foreach (var ship in ShipList.Ships.Values)
                    if (ship.Channel == e.Channel.Id)
                    {
                        ship.SetLastUsed(DateTime.Now);
                        ShipList.SaveToXML(Bot.BotSettings.ShipXML);
                        break;
                    }
            await Task.CompletedTask;
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task DeleteOnVoiceStateUpdated(DiscordClient client, VoiceStateUpdateEventArgs e)
        {
            var shipCategories = new[] {
                    e.Guild.GetChannel(Bot.BotSettings.AutocreateSloopCategory),
                    e.Guild.GetChannel(Bot.BotSettings.AutocreateBrigantineCategory),
                    e.Guild.GetChannel(Bot.BotSettings.AutocreateGalleonCategory)
                };

            shipCategories.ToList().ForEach(x =>
            {
                x.Children.Where(x => x.Type == ChannelType.Voice && x.Users.Count() == 0).ToList()
                    .ForEach(async x =>
                        {
                            try
                            {
                                await x.DeleteAsync();
                            }
                            catch (NullReferenceException) { } // исключения выбрасывается если пользователь покинул канал
                            catch (NotFoundException) { }
                        });
            });

            await Task.CompletedTask;
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task UpdateClientStatusOnVoiceStateUpdated(DiscordClient client, VoiceStateUpdateEventArgs e)
        {
            await Bot.UpdateBotStatusAsync(client, e.Guild);
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task UpdateVoiceTimeOnVoiceStateUpdated(DiscordClient client, VoiceStateUpdateEventArgs e)
        {
            // User changed voice channel
            if (e.Before != null && e.Before.Channel != null &&
                e.After != null && e.After.Channel != null &&
                e.Before.Channel.Id != e.After.Channel.Id)
            {
                if (e.Before.Channel.Id == e.Guild.AfkChannel.Id)
                {
                    VoiceTimeCounters[e.User.Id] = DateTime.Now;
                }
                else if (e.After.Channel.Id == e.Guild.AfkChannel.Id ||
                        e.After.Channel.Id == Bot.BotSettings.WaitingRoom)
                {
                    if (VoiceTimeCounters.ContainsKey(e.User.Id))
                    {
                        var time = DateTime.Now - VoiceTimeCounters[e.User.Id];
                        VoiceTimeSQL.AddForUser(e.User.Id, time);
                        VoiceTimeCounters.Remove(e.User.Id);
                    }
                }
            }
            //User left from voice
            else if (e.Before != null && e.Before.Channel != null &&
                     e.Before.Channel.Id != e.Guild.AfkChannel.Id &&
                     e.Before.Channel.Id != Bot.BotSettings.WaitingRoom)
            {
                if (VoiceTimeCounters.ContainsKey(e.User.Id))
                {
                    var time = DateTime.Now - VoiceTimeCounters[e.User.Id];
                    VoiceTimeSQL.AddForUser(e.User.Id, time);
                    VoiceTimeCounters.Remove(e.User.Id);
                }
            }
            //User joined to server voice
            else if (e.After != null && e.After.Channel != null &&
                     e.After.Channel.Id != e.Guild.AfkChannel.Id &&
                     e.After.Channel.Id != Bot.BotSettings.WaitingRoom)
            {
                VoiceTimeCounters[e.User.Id] = DateTime.Now;
            }

            await Task.CompletedTask;
        }


        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task FleetLogOnVoiceStateUpdated(DiscordClient client, VoiceStateUpdateEventArgs e)
        {
            //Для проверки если канал рейда чекать если название КАТЕГОРИИ канала начинается с "рейд"

            // User changed voice channel
            if (e.Before != null && e.Before.Channel != null &&
                e.After != null && e.After.Channel != null)
            {
                if (e.Before.Channel.Id != e.After.Channel.Id)
                    if (e.Before.Channel.Parent.Name.StartsWith("Рейд") ||
                        e.After.Channel.Parent.Name.StartsWith("Рейд"))
                    {
                        await e.Guild.GetChannel(Bot.BotSettings.FleetLogChannel)
                            .SendMessageAsync($"{DiscordEmoji.FromName(client, ":twisted_rightwards_arrows:")} " +
                            $"Пользователь **{e.User.Username}#{e.User.Discriminator}** ({e.User.Id}) " +
                            $"сменил канал с **{e.Before.Channel.Name}** ({e.Before.Channel.Id}) " +
                            $"на **{e.After.Channel.Name}** ({e.After.Channel.Id})");
                    }
            }
            //User left from voice
            else if (e.Before != null && e.Before.Channel != null)
            {
                if (e.Before.Channel.Parent.Name.StartsWith("Рейд"))
                {
                    await e.Guild.GetChannel(Bot.BotSettings.FleetLogChannel)
                        .SendMessageAsync($"{DiscordEmoji.FromName(client, ":negative_squared_cross_mark:")} " +
                        $"Пользователь **{e.User.Username}#{e.User.Discriminator}** ({e.User.Id}) " +
                        $"покинул канал **{e.Before.Channel.Name}** ({e.Before.Channel.Id})"); ; ;
                }
            }
            //User joined to server voice
            else if (e.After != null && e.After.Channel != null)
            {
                if (e.After.Channel.Parent.Name.StartsWith("Рейд"))
                {
                    await e.Guild.GetChannel(Bot.BotSettings.FleetLogChannel)
                        .SendMessageAsync($"{DiscordEmoji.FromName(client, ":white_check_mark:")} " +
                        $"Пользователь **{e.User.Username}#{e.User.Discriminator}** ({e.User.Id}) " +
                        $"подключился к каналу **{e.After.Channel.Name}** ({e.After.Channel.Id})");
                }
            }
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task FleetDeleteOnVoiceStateUpdated(DiscordClient client, VoiceStateUpdateEventArgs e)
        {
            //Проверка на пустые рейды
            if (e.Before != null && e.Before.Channel != null)
            {
                var leftChannel = e.Before.Channel;

                //Пользователь вышел из автоматически созданных каналов рейда
                if (leftChannel.Parent.Name.StartsWith("Рейд") &&
                   leftChannel.ParentId != Bot.BotSettings.FleetCategory &&
                   !leftChannel.Users.Contains(e.User))
                {
                    //Проверка всех каналов рейда на присутствие в них игроков
                    var fleetIsEmpty = leftChannel.Parent.Children
                                            .Where(x => x.Type == ChannelType.Voice)
                                            .Where(x => x.Users.Count() > 0)
                                            .Count() == 0;

                    //Удаляем каналы и категорию
                    if (fleetIsEmpty)
                    {
                        await FleetLogging.LogFleetDeletionAsync(client, e.Guild, leftChannel.Parent);

                        foreach (var emptyChannel in leftChannel.Parent.Children.Where(x => x.Type == ChannelType.Voice))
                            try
                            {
                                await emptyChannel.DeleteAsync();
                            }
                            catch (NotFoundException) { }

                        await leftChannel.Parent.DeleteAsync();
                    }
                }
            }
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task UpdateFindChannelEmbedOnVoiceStateUpdated(DiscordClient client, VoiceStateUpdateEventArgs e)
        {
            if (e.Before?.Channel?.Id == e.After?.Channel?.Id)
                return;

            List<DiscordChannel> channels = new List<DiscordChannel>();

            if (e.Before?.Channel?.Id != null)
                channels.Add(e.Before.Channel);

            if (e.After?.Channel?.Id != null)
                channels.Add(e.After.Channel);

            foreach (var channel in channels)
            {
                try
                {
                    if (FindChannelInvites.ContainsKey(channel.Id))
                    {
                        client.Logger.LogDebug(BotLoggerEvents.Event, $"Получение сообщения в поиске игроков!");
                        try
                        {
                            var embedMessage = await e.Guild.GetChannel(Bot.BotSettings.FindChannel).GetMessageAsync(FindChannelInvites[channel.Id]);

                            if (channel.Users.Count() == 0)
                            {
                                try
                                {
                                    client.Logger.LogDebug(BotLoggerEvents.Event, $"Удаление ембеда в поиске игроков!");
                                    await embedMessage.DeleteAsync();
                                    FindChannelInvites.Remove(channel.Id);
                                    await SaveFindChannelMessagesAsync();
                                }
                                catch (NotFoundException) { }
                            }
                            else
                            {
                                var oldEmbed = embedMessage.Embeds.FirstOrDefault();
                                var oldContent = oldEmbed.Description.Split("\n\n");

                                var usersNeeded = channel.UserLimit - channel.Users.Count();

                                var embedThumbnail = "";
                                //Если канал в категории рейда, вставляем картинку с рейдом и проверяем если это обычный канал рейда (в нём 1 лишний слот, его мы игнорируем)
                                if (channel.Parent.Name.StartsWith("Рейд"))
                                {
                                    if (channel.Name.StartsWith("Рейд"))
                                        usersNeeded = Math.Max(0, (usersNeeded - 1));

                                    embedThumbnail = usersNeeded switch
                                    {
                                        0 => Bot.BotSettings.ThumbnailFull,
                                        _ => Bot.BotSettings.ThumbnailRaid
                                    };
                                }
                                //Если это не канал рейда, вставляем подходящую картинку по слотам, или NA если число другое
                                else
                                {
                                    embedThumbnail = usersNeeded switch
                                    {
                                        0 => Bot.BotSettings.ThumbnailFull,
                                        1 => Bot.BotSettings.ThumbnailOne,
                                        2 => Bot.BotSettings.ThumbnailTwo,
                                        3 => Bot.BotSettings.ThumbnailThree,
                                        _ => Bot.BotSettings.ThumbnailNA
                                    };
                                }

                                //Index 0 for description
                                var content = $"{oldContent[0]}\n\n";

                                //Index 1 for users in channel
                                var slotsCount = 1;
                                foreach (var member in channel.Users)
                                {
                                    if (content.Length > 1900 || slotsCount > 15)
                                    {
                                        content += $"{DiscordEmoji.FromName(client, ":arrow_heading_down:")} и еще {channel.Users.Count() - slotsCount + 1}.\n";
                                        break;
                                    }
                                    else
                                    {
                                        content += $"{DiscordEmoji.FromName(client, ":doubloon:")} {member.Mention}\n";
                                        slotsCount++;
                                    }
                                }

                                for (int i = 0; i < usersNeeded; i++)
                                {
                                    if (content.Length > 1900 || slotsCount > 15)
                                    {
                                        if (i != 0) //Без этого сообщение будет отправлено вместе с тем что выше
                                            content += $"{DiscordEmoji.FromName(client, ":arrow_heading_down:")} и еще {channel.UserLimit - slotsCount + 1} свободно.\n";
                                        break;
                                    }
                                    else
                                    {
                                        content += $"{DiscordEmoji.FromName(client, ":gold:")} ☐\n";
                                        slotsCount++;
                                    }
                                }

                                //Index 2 for invite link
                                content += $"\n{oldContent[2]}";

                                //Embed
                                var embed = new DiscordEmbedBuilder
                                {
                                    Description = content,
                                    Color = usersNeeded == 0 ? new DiscordColor("#2c3e50") : new DiscordColor("#e67e22")
                                };

                                embed.WithAuthor($"{channel.Name}", oldEmbed.Author.Url.ToString(), oldEmbed.Author.IconUrl.ToString());
                                embed.WithThumbnail(embedThumbnail);
                                embed.WithTimestamp(DateTime.Now);
                                embed.WithFooter(usersNeeded != 0 ? $"В поиске команды. +{usersNeeded}" : $"Канал заполнен {DiscordEmoji.FromName(client, ":no_entry:")}");

                                client.Logger.LogDebug(BotLoggerEvents.Event, $"Обновление ембеда в поиске игроков!");
                                await embedMessage.ModifyAsync(embed: embed.Build());
                            }
                        }
                        catch (NotFoundException)
                        {
                            FindChannelInvites.Remove(channel.Id);
                        }
                    }
                }
                catch (NullReferenceException)
                {
                    client.Logger.LogWarning(BotLoggerEvents.Event, $"Не удалось обновить сообщение с эмбедом для голосового канала. Канал будет удалён из привязки к сообщению.");
                    FindChannelInvites.Remove(channel.Id);
                    await SaveFindChannelMessagesAsync();
                    return;
                }
            }
        }

        public static void ReadFindChannelMesages()
        {
            //Read
            using (TextFieldParser parser = new TextFieldParser("generated/find_channel_invites.csv"))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                while (!parser.EndOfData)
                {
                    string[] fields = parser.ReadFields();
                    FindChannelInvites[Convert.ToUInt64(fields[0])] = Convert.ToUInt64(fields[1]);
                }
            }
        }

        public static async Task SaveFindChannelMessagesAsync()
        {
            using (var fs = new FileStream("generated/find_channel_invites.csv", FileMode.Truncate))
            using (var sw = new StreamWriter(fs))
                foreach(var elem in FindChannelInvites)
                    await sw.WriteLineAsync($"{elem.Key},{elem.Value}");
        }

        public static TimeSpan GetUpdatedVoiceTime(ulong userId)
        {
            //Force update voiceTime
            if (VoiceTimeCounters.ContainsKey(userId))
            {
                var time = DateTime.Now - VoiceTimeCounters[userId];
                VoiceTimeSQL.AddForUser(userId, time);
                VoiceTimeCounters[userId] = DateTime.Now;
            }

            return VoiceTimeSQL.GetForUser(userId);
        }
    }
}
