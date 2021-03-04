using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Linq;
using Bot_NetCore.Commands;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bot_NetCore.Listeners
{
    public static class Timers
    {
        private static DiscordClient Client;
        private static int RainbowColor = 0;
        private static DiscordColor[] Colors = new DiscordColor[]
        {
            DiscordColor.Red,
            DiscordColor.Orange,
            DiscordColor.Yellow,
            DiscordColor.Green,
            DiscordColor.Cyan,
            DiscordColor.HotPink
        };

        [AsyncListener(EventTypes.Ready)]
        public static async Task RegisterTimers(DiscordClient client, ReadyEventArgs e)
        {
            Client = client;

            //Таймер, который каждую минуту проверяет все баны и удаляет истёкшие.
            var checkExpiredReports = new Timer(60000);
            checkExpiredReports.Elapsed += CheckExpiredReports;
            checkExpiredReports.AutoReset = true;
            checkExpiredReports.Enabled = true;

            //Таймер который каждую минуту проверяет истекшие сообщения в каналах
            var clearChannelMessages = new Timer(60000);
            clearChannelMessages.Elapsed += ClearChannelMessagesOnElapsed;
            clearChannelMessages.AutoReset = true;
            clearChannelMessages.Enabled = true;


            var clearVotes = new Timer(60000);
            clearVotes.Elapsed += ClearAndRepairVotesOnElapsed;
            clearVotes.AutoReset = true;
            clearVotes.Enabled = true;

            var deleteShips = new Timer(60000 * 10);
            deleteShips.Elapsed += DeleteShipsOnElapsed;
            deleteShips.AutoReset = true;
            deleteShips.Enabled = true;

            //var clearSubscriptions = new Timer(60000);
            //clearSubscriptions.Elapsed += ClearSubscriptionsOnElapsed;
            //clearSubscriptions.AutoReset = true;
            //clearSubscriptions.Enabled = true;

            var updateVoiceTimes = new Timer(60000 * 5);
            updateVoiceTimes.Elapsed += UpdateVoiceTimesOnElapsedAsync;
            updateVoiceTimes.AutoReset = true;
            updateVoiceTimes.Enabled = true;
            
            var sendMessagesOnExactTime = new Timer(60000);
            sendMessagesOnExactTime.Elapsed += SendMessagesOnExactTimeOnElapsed;
            sendMessagesOnExactTime.AutoReset = true;
            sendMessagesOnExactTime.Enabled = true;

            var checkExpiredTickets = new Timer(60000 * 30);
            checkExpiredTickets.Elapsed += CheckExpiredTicketsAsync;
            checkExpiredTickets.AutoReset = true;
            checkExpiredTickets.Enabled = true;

            var checkExpiredFleetPoll = new Timer(60000 * 5);
            checkExpiredFleetPoll.Elapsed += CheckExpiredFleetPoll;
            checkExpiredFleetPoll.AutoReset = true;
            checkExpiredFleetPoll.Enabled = true;

            await Task.CompletedTask;
        }

        private static async void RainbowRoleOnElapsed(object sender, ElapsedEventArgs e)
        {
            if (Bot.BotSettings.RainbowEnabled)
            {
                Client.Logger.LogDebug(BotLoggerEvents.Timers, $"RainbowRoleOnElapsed running");

                try
                {
                    var role = Client.Guilds[Bot.BotSettings.Guild].GetRole(Bot.BotSettings.RainbowRole);
                    if (RainbowColor >= Colors.Length) RainbowColor = 0;
                    await role.ModifyAsync(color: Colors[RainbowColor]);
                    ++RainbowColor;
                }
                catch (NullReferenceException)
                {
                    
                }
            }
        }

        private static async void SendMessagesOnExactTimeOnElapsed(object sender, ElapsedEventArgs e)
        {
            Client.Logger.LogDebug(BotLoggerEvents.Timers, $"SendMessagesOnExactTimeOnElapsed running");

            // send a new year message
            DateTime currentTime = DateTime.Now;
            if (currentTime.Month == 1 && currentTime.Day == 1 && currentTime.Hour == 0 && currentTime.Minute == 0)
                await Client.Guilds[Bot.BotSettings.Guild].GetChannel(435730405077811200).SendMessageAsync("**:christmas_tree: С Новым Годом, пираты! :christmas_tree:**");
        }

        //private static async void ClearSubscriptionsOnElapsed(object sender, ElapsedEventArgs e)
        //{
        //    Client.Logger.LogDebug(BotLoggerEvents.Timers, $"ClearSubscriptionsOnElapsed running");

        //    for (int i = 0; i < Subscriber.Subscribers.Count; ++i)
        //    {
        //        var sub = Subscriber.Subscribers.Values.ToArray()[i];
        //        if (DateTime.Now > sub.SubscriptionEnd)
        //        {
        //            try
        //            {
        //                var guild = Client.Guilds[Bot.BotSettings.Guild];
        //                var member = await guild.GetMemberAsync(sub.Member);
        //                try
        //                {
        //                    if (member != null)
        //                    {
        //                        await member.SendMessageAsync("Ваша подписка истекла :cry:");
        //                    }
        //                }
        //                catch (NotFoundException) { }
        //                catch (ArgumentException) { }

        //                try
        //                {
        //                    await DonatorCommands.DeletePrivateRoleAsync(guild, member.Id);
        //                }
        //                catch (Exceptions.NotFoundException) { }

        //                Subscriber.Subscribers.Remove(sub.Member);
        //                Subscriber.Save(Bot.BotSettings.SubscriberXML);
        //            }
        //            catch (Exception ex)
        //            {
        //                Client.Logger.LogError(BotLoggerEvents.Timers, ex, $"Возникла ошибка при очистке подписок.");
        //            }
        //        }
        //    }
        //}

        private static async void DeleteShipsOnElapsed(object sender, ElapsedEventArgs e)
        {
            Client.Logger.LogDebug(BotLoggerEvents.Timers, $"DeleteShipsOnElapsed running");

            foreach (var ship in PrivateShip.GetAll())
            {
                if ((DateTime.Now - ship.LastUsed).Days >= 3)
                {
                    var guild = await Client.GetGuildAsync(Bot.BotSettings.Guild);
                    try
                    {
                        var channel = guild.GetChannel(ship.Channel);
                        await channel.DeleteAsync();
                    }
                    catch (NotFoundException)
                    {
                        
                    }

                    try
                    {
                        var member = await guild.GetMemberAsync(ship.GetCaptain().MemberId);
                        await member.SendMessageAsync($"Твой корабль **{ship.Name}** был удалён из-за неактивности.");
                    }
                    catch (UnauthorizedException)
                    {

                    }
                    catch (NotFoundException)
                    {
                        
                    }
                    
                    PrivateShip.Delete(ship.Name);
                    
                    await guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync("**Удаление корабля**\n\n" +
                        $"**Модератор:** {Client.CurrentUser}\n" +
                        $"**Название:** {ship.Name}\n" +
                        $"**Дата:** {DateTime.Now}");
                }
            }
        }

        private static async void ClearAndRepairVotesOnElapsed(object sender, ElapsedEventArgs e)
        {
            Client.Logger.LogDebug(BotLoggerEvents.Timers, $"ClearAndRepairVotesOnElapsed running");

            try
            {
                var channelMessages = await Client.Guilds[Bot.BotSettings.Guild].GetChannel(Bot.BotSettings.VotesChannel)
                            .GetMessagesAsync();
                for (int i = 0; i < Vote.Votes.Count; ++i)
                {
                    var vote = Vote.Votes.Values.ToArray()[i];
                    try
                    {
                        var message = channelMessages.FirstOrDefault(x => x.Id == vote.Message);
                        if (message != null)
                        {
                            if (DateTime.Now >= vote.End && (DateTime.Now - vote.End).Days < 10) // выключение голосования
                            {
                                if (message.Reactions.Count == 0) continue;

                                var author = await Client.Guilds[Bot.BotSettings.Guild].GetMemberAsync(vote.Author);
                                var embed = Utility.GenerateVoteEmbed(
                                    author,
                                    vote.Yes > vote.No ? DiscordColor.Green : DiscordColor.Red,
                                    vote.Topic, vote.End,
                                    vote.Voters.Count,
                                    vote.Yes,
                                    vote.No,
                                    vote.Id);

                                await message.ModifyAsync(embed: embed);
                                await message.DeleteAllReactionsAsync();
                            }
                            else if (DateTime.Now >= vote.End && (DateTime.Now - vote.End).Days >= 3 && !message.Pinned) // архивирование голосования
                            {
                                var author = await Client.Guilds[Bot.BotSettings.Guild].GetMemberAsync(vote.Author);
                                var embed = Utility.GenerateVoteEmbed(
                                    author,
                                    vote.Yes > vote.No ? DiscordColor.Green : DiscordColor.Red,
                                    vote.Topic, vote.End,
                                    vote.Voters.Count,
                                    vote.Yes,
                                    vote.No,
                                    vote.Id);

                                var doc = new XDocument();
                                var root = new XElement("Voters");
                                foreach (var voter in vote.Voters)
                                    root.Add(new XElement("Voter", voter));
                                doc.Add(root);
                                doc.Save($"generated/voters-{vote.Id}.xml");

                                var channel = Client.Guilds[Bot.BotSettings.Guild].GetChannel(Bot.BotSettings.VotesArchive);
                                await channel.SendFileAsync($"generated/voters-{vote.Id}.xml", embed: embed);

                                await message.DeleteAsync();

                                //Закрытие канала, если в нём больше нету голосований
                                if(channelMessages.Count() == 0)
                                {
                                    channel = Client.Guilds[Bot.BotSettings.Guild].GetChannel(Bot.BotSettings.VotesChannel);
                                    await channel.AddOverwriteAsync(Client.Guilds[Bot.BotSettings.Guild].GetRole(Bot.BotSettings.CodexRole), deny: Permissions.AccessChannels);
                                }
                            }
                            else if (DateTime.Now < vote.End) // починка голосования
                            {
                                if (message.Reactions.Count < 2)
                                {
                                    await message.DeleteAllReactionsAsync();
                                    await message.CreateReactionAsync(DiscordEmoji.FromName(Client, ":white_check_mark:"));
                                    await Task.Delay(400);
                                    await message.CreateReactionAsync(DiscordEmoji.FromName(Client, ":no_entry:"));
                                }
                            }
                        }
                    }
                    catch (ArgumentNullException)
                    {
                        //Do nothing, message not found
                    }
                }
            }
            catch (Exception ex)
            {
                Client.Logger.LogError(BotLoggerEvents.Timers, ex, $"Возникла ошибка при очистке голосований.");
            }
        }


        /// <summary>
        ///     Очистка сообщений из каналов
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static async void ClearChannelMessagesOnElapsed(object sender, ElapsedEventArgs e)
        {
            Client.Logger.LogDebug(BotLoggerEvents.Timers, $"ClearChannelMessagesOnElapsed running");

            try
            {
                var guild = Client.Guilds[Bot.BotSettings.Guild];

                var channels = new Dictionary<DiscordChannel, TimeSpan>
                {
                    { guild.GetChannel(Bot.BotSettings.FindChannel), new TimeSpan(0, 30, 0) },           //30 минут для канала поиска
                    { guild.GetChannel(Bot.BotSettings.FleetCreationChannel), new TimeSpan(24, 0, 0) }   //24 часа для канала создания рейда
                };

                foreach (var channel in channels)
                {
                    try
                    {
                        var messages = await channel.Key.GetMessagesAsync();
                        var toDelete = messages.ToList()
                            .Where(x => !x.Pinned).ToList()                                             //Не закрепленные сообщения
                            .Where(x =>
                            {
                                if (x.IsEdited && x.Embeds.Count() != 0 &&
                                    x.Embeds.FirstOrDefault().Footer.Text.Contains("заполнен"))
                                    return DateTimeOffset.Now.Subtract(x.EditedTimestamp.Value.Add(new TimeSpan(0, 5, 0))).TotalSeconds > 0;
                                else
                                    return DateTimeOffset.Now.Subtract(x.CreationTimestamp.Add(channel.Value)).TotalSeconds > 0;
                            });     //Опубликованные ранее определенного времени

                        //Clear FindChannelInvites from deleted messages
                        foreach (var message in toDelete)
                            if (VoiceListener.FindChannelInvites.ContainsValue(message.Id))
                            {
                                VoiceListener.FindChannelInvites.Remove(VoiceListener.FindChannelInvites.FirstOrDefault(x => x.Value == message.Id).Key);
                                await VoiceListener.SaveFindChannelMessagesAsync();
                            }

                        if (toDelete.Count() > 0)
                        {
                            await channel.Key.DeleteMessagesAsync(toDelete);
                            Client.Logger.LogInformation(BotLoggerEvents.Timers, $"Канал {channel.Key.Name} был очищен.");
                        }

                    }
                    catch (Exception ex)
                    {
                        Client.Logger.LogWarning(BotLoggerEvents.Timers, ex, $"Ошибка при удалении сообщений в {channel.Key.Name}.", DateTime.Now);
                    }
                }
            }
            catch (Exception ex)
            {
                Client.Logger.LogError(BotLoggerEvents.Timers, ex, $"Ошибка при удалении сообщений в каналах.");
            }
        }

        private static async void CheckExpiredReports(object sender, ElapsedEventArgs e)
        {
            Client.Logger.LogDebug(BotLoggerEvents.Timers, $"CheckExpiredReports running");

            var guild = await Client.GetGuildAsync(Bot.BotSettings.Guild);

            //Check for expired bans
            var toUnban = BanSQL.GetExpiredBans();

            if (toUnban.Any())
            {
                var bans = await guild.GetBansAsync();
                foreach (var ban in toUnban)
                {
                    for (int i = 0; i < bans.Count; ++i)
                    {
                        if (bans[i].User.Id == ban.User)
                        {
                            Client.Logger.LogDebug(BotLoggerEvents.Timers, $"Попытка снятия бана {ban}");
                            await guild.UnbanMemberAsync(ban.User);
                            var user = await Client.GetUserAsync(ban.User);
                            await guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                                "**Снятие бана**\n\n" +
                                $"**Модератор:** {Client.CurrentUser.Username}\n" +
                                $"**Пользователь:** {user}\n" +
                                $"**Дата:** {DateTime.Now}\n");

                            Client.Logger.LogInformation(BotLoggerEvents.Timers, $"Пользователь {user} был разбанен.");
                            break;
                        }
                    }
                }
            }

            //Check for expired mutes
            var reports = ReportSQL.GetExpiredReports();
            foreach (var report in reports)
            {
                if (report.ReportType == ReportType.Mute)
                {
                    Client.Logger.LogDebug(BotLoggerEvents.Timers, $"Попытка снятия блокировки {report}");
                    try
                    {
                        var user = await guild.GetMemberAsync(report.User);
                        await user.RevokeRoleAsync(guild.GetRole(Bot.BotSettings.MuteRole), "Unmuted");
                    }
                    catch (NotFoundException)
                    {
                        //Пользователь не найден
                    }
                    ReportSQL.Delete(report.Id);
                }
                else if (report.ReportType == ReportType.VoiceMute)
                {
                    Client.Logger.LogDebug(BotLoggerEvents.Timers, $"Попытка снятия блокировки {report}");
                    try
                    {
                        var user = await guild.GetMemberAsync(report.User);
                        await user.RevokeRoleAsync(guild.GetRole(Bot.BotSettings.VoiceMuteRole), "Unmuted");
                    }
                    catch (NotFoundException)
                    {
                        //Пользователь не найден
                    }
                    ReportSQL.Delete(report.Id);
                }
            }
        }


        /// <summary>
        ///     Обновление времени в голосовых каналах
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void UpdateVoiceTimesOnElapsedAsync(object sender, ElapsedEventArgs e)
        {
            Client.Logger.LogDebug(BotLoggerEvents.Timers, $"UpdateVoiceTimesOnElapsedAsync running");

            try
            {
                foreach (var entry in VoiceListener.VoiceTimeCounters)
                {
                    try
                    {
                        var time = DateTime.Now - entry.Value;
                        VoiceTimeSQL.AddForUser(entry.Key, time);
                    }
                    catch (Exception ex)
                    {
                        Client.Logger.LogError(BotLoggerEvents.Timers, ex, $"Ошибка при обновлении времени активности пользователя ({entry.Key}).");
                    }
                }

                //Clear old values
                VoiceListener.VoiceTimeCounters.Clear();
                var guild = Client.Guilds[Bot.BotSettings.Guild];
                foreach (var entry in guild.VoiceStates.Where(x => x.Value.Channel != null && x.Value.Channel.Id != guild.AfkChannel.Id && x.Value.Channel.Id != Bot.BotSettings.WaitingRoom).ToList())
                {
                    if (!VoiceListener.VoiceTimeCounters.ContainsKey(entry.Key))
                        VoiceListener.VoiceTimeCounters.Add(entry.Key, DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                Client.Logger.LogError(BotLoggerEvents.Timers, ex, $"Ошибка при обновлении времени активности пользователей.");
            }
        }

        /// <summary>
        ///     Удаление старых тикетов.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static async void CheckExpiredTicketsAsync(object sender, ElapsedEventArgs e)
        {
            Client.Logger.LogDebug(BotLoggerEvents.Timers, $"CheckExpiredTicketsAsync running");

            var expiredTickets = TicketSQL.GetClosedFor(TimeSpan.FromDays(2));

            var guild = Client.Guilds[Bot.BotSettings.Guild];

            foreach (var ticket in expiredTickets)
            {
                ticket.Status = TicketSQL.TicketStatus.Deleted;
                try
                {
                    await guild.GetChannel(ticket.ChannelId).DeleteAsync();
                }
                catch (NotFoundException) { }
            }
        }

        /// <summary>
        ///     Удаление старых тикетов.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static async void CheckExpiredFleetPoll(object sender, ElapsedEventArgs e)
        {
            Client.Logger.LogDebug(BotLoggerEvents.Timers, $"CheckExpiredFleetPoll running");

            try
            {
                var fleetPollResetTime = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 12, 0, 0);

                if (DateTime.Now > fleetPollResetTime)
                {
                    var message = await Client.Guilds[Bot.BotSettings.Guild].GetChannel(Bot.BotSettings.FleetCreationChannel)
                        .GetMessageAsync(Bot.BotSettings.FleetVotingMessage);

                    if (message.Embeds.Count > 0)
                    {
                        var oldEmbed = message.Embeds.FirstOrDefault();

                        var messageTime = message.EditedTimestamp != null ? message.EditedTimestamp : message.CreationTimestamp;

                        //Проверка времени
                        if (fleetPollResetTime >= oldEmbed.Timestamp)
                        {
                            var embed = new DiscordEmbedBuilder(oldEmbed)
                                .WithDescription($"Вы можете проголосовать за тип рейда который вас интересует больше всего **{fleetPollResetTime.AddDays(2):dd/MM}**.\n\n" +
                                                "Таким образом капитанам рейда будет легче узнать какой тип рейда больше всего востребован.\n")
                                .WithTimestamp(fleetPollResetTime.AddDays(1));

                            foreach (var reaction in message.Reactions)
                            {
                                var fieldId = reaction.Emoji.GetDiscordName() switch
                                {
                                    ":one:" => 0,
                                    ":two:" => 1,
                                    ":three:" => 2,
                                    _ => 0
                                };

                                embed.Fields[fieldId].Value = $":black_circle: **{reaction.Count - 1}**";
                            }

                            await message.ModifyAsync(embed: embed.Build());

                            await message.DeleteAllReactionsAsync();

                            var emojis = new DiscordEmoji[]
                            {
                                DiscordEmoji.FromName(Client, ":one:"),
                                DiscordEmoji.FromName(Client, ":two:"),
                                DiscordEmoji.FromName(Client, ":three:")
                            };

                            foreach (var emoji in emojis)
                            {
                                await Task.Delay(400);
                                await message.CreateReactionAsync(emoji);
                            }

                            Client.Logger.LogInformation(BotLoggerEvents.Timers, $"Успешно обновлено голосование рейдов");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Client.Logger.LogError(BotLoggerEvents.Timers, ex, $"Ошибка при обновлении голосования рейдов.");
            }
        }
    }
}
