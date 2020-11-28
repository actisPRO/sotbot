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
        public static async Task CreateOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
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
                                e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                                    $"Участник {e.User.Username}#{e.User.Discriminator} ({e.User.Discriminator}) был перемещён в комнату ожидания.",
                                    DateTime.Now);
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
                                channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":moneybag:");
                            else if (x.Id == Bot.BotSettings.EmissaryTradingCompanyRole)
                                channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":pig:");
                            else if (x.Id == Bot.BotSettings.EmissaryOrderOfSoulsRole)
                                channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":skull:");
                            else if (x.Id == Bot.BotSettings.EmissaryAthenaRole)
                                channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":gem:");
                            else if (x.Id == Bot.BotSettings.EmissaryReaperBonesRole)
                                channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":skull_crossbones:");
                            else if (x.Id == Bot.BotSettings.HuntersRole)
                                channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":fish:");
                            else if (x.Id == Bot.BotSettings.ArenaRole)
                                channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":crossed_swords:");

                        });

                        DiscordChannel created = null;
                        // Проверяем канал в котором находится пользователь

                        if (e.Channel.Id == Bot.BotSettings.AutocreateSloop) //Шлюп
                            created = await e.Guild.CreateChannelAsync(
                                $"{channelSymbol} Шлюп {e.User.Username}", ChannelType.Voice,
                                e.Guild.GetChannel(Bot.BotSettings.AutocreateCategory), bitrate: Bot.BotSettings.Bitrate, userLimit: 2);
                        else if (e.Channel.Id == Bot.BotSettings.AutocreateBrigantine) // Бригантина
                            created = await e.Guild.CreateChannelAsync(
                                $"{channelSymbol} Бриг {e.User.Username}", ChannelType.Voice,
                                e.Guild.GetChannel(Bot.BotSettings.AutocreateCategory), bitrate: Bot.BotSettings.Bitrate, userLimit: 3);
                        else // Галеон
                            created = await e.Guild.CreateChannelAsync(
                                $"{channelSymbol} Галеон {e.User.Username}", ChannelType.Voice,
                                e.Guild.GetChannel(Bot.BotSettings.AutocreateCategory), bitrate: Bot.BotSettings.Bitrate, userLimit: 4);

                        await member.PlaceInAsync(created);

                        e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                            $"Участник {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) создал канал через автосоздание.",
                            DateTime.Now);
                    }
            }
            catch (NullReferenceException) // исключение выбрасывается если пользователь покинул канал
            {
                // нам здесь ничего не надо делать, просто пропускаем
            }
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task FindOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            if (e.Channel != null &&
                e.Channel.Id == Bot.BotSettings.FindShip)
            {
                var shipCategory = e.Guild.GetChannel(Bot.BotSettings.AutocreateCategory);

                var membersLookingForTeam = new List<ulong>();
                foreach (var message in (await e.Guild.GetChannel(Bot.BotSettings.FindChannel).GetMessagesAsync(100)))
                {
                    if (message.Pinned) continue; // автор закрепленного сообщения не должен учитываться
                    if (membersLookingForTeam.Contains(message.Author.Id)) continue; // автор сообщения уже мог быть добавлен в лист

                    membersLookingForTeam.Add(message.Author.Id);
                }

                var possibleChannels = new List<DiscordChannel>();
                foreach (var ship in shipCategory.Children)
                    if (ship.Users.Count() < ship.UserLimit)
                        foreach (var user in ship.Users)
                            if (membersLookingForTeam.Contains(user.Id))
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
                e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot", $"Пользователь {m.Username}#{m.Discriminator} успешно воспользовался поиском корабля!", DateTime.Now);
                return;
            }
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task PrivateOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
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
        public static async Task DeleteOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            e.Guild.Channels[Bot.BotSettings.AutocreateCategory].Children
                .Where(x => x.Type == ChannelType.Voice && x.Users.Count() == 0).ToList()
                .ForEach(async x =>
                    {
                        try
                        {
                            await x.DeleteAsync();
                        }
                        catch (NullReferenceException) { } // исключения выбрасывается если пользователь покинул канал
                        catch (NotFoundException) { }
                    });

            await Task.CompletedTask;
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task UpdateClientStatusOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            await Bot.UpdateBotStatusAsync(e.Client, e.Guild);
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task UpdateVoiceTimeOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            //TODO: Раз в 10 минут проверять список и обновлять время

            // User changed voice channel
            if (e.Before != null && e.Before.Channel != null &&
                e.After != null && e.After.Channel != null &&
                e.Before.Channel.Id != e.After.Channel.Id)
            {
                if(e.Before.Channel.Id == e.Guild.AfkChannel.Id)
                {
                    VoiceTimeCounters[e.User.Id] = DateTime.Now;
                }
                else if(e.After.Channel.Id == e.Guild.AfkChannel.Id)
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
                e.Before.Channel.Id != e.Guild.AfkChannel.Id)
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
                e.After.Channel.Id != e.Guild.AfkChannel.Id)
            {
                    VoiceTimeCounters[e.User.Id] = DateTime.Now;
            }

            await Task.CompletedTask;
        }


        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task FleetLogOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
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
                            .SendMessageAsync($"{DiscordEmoji.FromName(e.Client, ":twisted_rightwards_arrows:")} " +
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
                        .SendMessageAsync($"{DiscordEmoji.FromName(e.Client, ":negative_squared_cross_mark:")} " +
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
                        .SendMessageAsync($"{DiscordEmoji.FromName(e.Client, ":white_check_mark:")} " +
                        $"Пользователь **{e.User.Username}#{e.User.Discriminator}** ({e.User.Id}) " +
                        $"подключился к каналу **{e.After.Channel.Name}** ({e.After.Channel.Id})");
                }
            }
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task FleetDeleteOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
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
                        await FleetLogging.LogFleetDeletionAsync(e.Client, e.Guild, leftChannel.Parent);

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
        public static async Task UpdateFindChannelEmbedOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            List<DiscordChannel> channels = new List<DiscordChannel>();
            if (e.Before != null && e.Before.Channel != null)
                channels.Add(e.Before.Channel);
            if (e.After != null && e.After.Channel != null)
                if(!channels.Contains(e.After.Channel))
                    channels.Add(e.After.Channel);

            foreach (var channel in channels)
            {
                try
                {
                    if (FindChannelInvites.ContainsKey(channel.Id))
                    {
                        e.Client.DebugLogger.LogMessage(LogLevel.Debug, "Bot", $"Получение сообщения в поиске игроков!", DateTime.Now);
                        var embedMessage = await e.Guild.GetChannel(Bot.BotSettings.FindChannel).GetMessageAsync(FindChannelInvites[channel.Id]);

                        if (channel.Users.Count() == 0)
                        {
                            try
                            {
                                e.Client.DebugLogger.LogMessage(LogLevel.Debug, "Bot", $"Удаление ембеда в поиске игроков!", DateTime.Now);
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
                            foreach (var member in channel.Users)
                                content += $"{DiscordEmoji.FromName(e.Client, ":doubloon:")} {member.Mention}\n";

                            for (int i = 0; i < usersNeeded; i++)
                                content += $"{DiscordEmoji.FromName(e.Client, ":gold:")} ☐\n";

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
                            embed.WithFooter(usersNeeded != 0 ? $"В поиске команды. +{usersNeeded}" : $"Канал заполнен {DiscordEmoji.FromName(e.Client, ":no_entry:")}");

                            e.Client.DebugLogger.LogMessage(LogLevel.Debug, "Bot", $"Обновление ембеда в поиске игроков!", DateTime.Now);
                            await embedMessage.ModifyAsync(embed: embed.Build());
                        }
                    }
                }
                catch (NullReferenceException)
                {
                    e.Client.DebugLogger.LogMessage(LogLevel.Warning, "Bot",
                        $"Не удалось обновить сообщение с эмбедом для голосового канала. Канал будет удалён из привязки к сообщению.",
                        DateTime.Now);
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
