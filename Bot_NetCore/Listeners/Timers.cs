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

namespace Bot_NetCore.Listeners
{
    public static class Timers
    {
        private static DiscordClient Client;

        [AsyncListener(EventTypes.Ready)]
        public static async Task RegisterTimers(ReadyEventArgs e)
        {
            Client = e.Client;

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

            var clearSubscriptions = new Timer(60000);
            clearSubscriptions.Elapsed += ClearSubscriptionsOnElapsed;
            clearSubscriptions.AutoReset = true;
            clearSubscriptions.Enabled = true;

            await Task.CompletedTask;
        }

        private static async void ClearSubscriptionsOnElapsed(object sender, ElapsedEventArgs e)
        {
            for (int i = 0; i < Subscriber.Subscribers.Count; ++i)
            {
                var sub = Subscriber.Subscribers.Values.ToArray()[i];
                if (DateTime.Now > sub.SubscriptionEnd)
                {
                    try
                    {
                        var guild = Client.Guilds[Bot.BotSettings.Guild];
                        var member = await guild.GetMemberAsync(sub.Member);
                        if (member != null)
                        {
                            await member.SendMessageAsync("Ваша подписка истекла :cry:");
                        }

                        try
                        {
                            await DonatorCommands.DeletePrivateRoleAsync(guild, member.Id);
                        }
                        catch (Exceptions.NotFoundException) { }

                        Subscriber.Subscribers.Remove(sub.Member);
                        Subscriber.Save(Bot.BotSettings.SubscriberXML);
                    }
                    catch (Exception ex)
                    {
                        Client.DebugLogger.LogMessage(LogLevel.Error, "Bot",
                            $"Возникла ошибка при очистке подписок {ex.StackTrace}.",
                            DateTime.Now);
                    }
                }
            }
        }

        private static async void DeleteShipsOnElapsed(object sender, ElapsedEventArgs e)
        {
            for (int i = 0; i < ShipList.Ships.Count; ++i)
            {
                var ship = ShipList.Ships.Values.ToArray()[i];
                if ((DateTime.Now - ship.LastUsed).Days >= 3)
                {
                    var channel = Client.Guilds[Bot.BotSettings.Guild].GetChannel(ship.Channel);

                    ulong ownerId = 0;
                    foreach (var member in ship.Members.Values)
                        if (member.Type == MemberType.Owner)
                        {
                            ownerId = member.Id;
                            break;
                        }

                    DiscordMember owner = null;
                    try
                    {
                        owner = await Client.Guilds[Bot.BotSettings.Guild].GetMemberAsync(ownerId);
                        await owner.SendMessageAsync(
                            "Ваш приватный корабль был неактивен долгое время и поэтому он был удалён. \n**Пожалуйста, не отправляйте новый запрос на создание, если" +
                            " не планируете пользоваться этой функцией**");
                    }
                    catch (NotFoundException)
                    {
                        // ничего не делаем, владелец покинул сервер
                    }

                    ship.Delete();
                    ShipList.SaveToXML(Bot.BotSettings.ShipXML);

                    await channel.DeleteAsync();

                    var doc = XDocument.Load("actions.xml");
                    foreach (var action in doc.Element("actions").Elements("action"))
                        if (Convert.ToUInt64(action.Value) == ownerId)
                            action.Remove();
                    doc.Save("actions.xml");

                    await Client.Guilds[Bot.BotSettings.Guild].GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                        "**Удаление корабля**\n\n" +
                        $"**Модератор:** {Client.CurrentUser}\n" +
                        $"**Корабль:** {ship.Name}\n" +
                        $"**Владелец:** {owner}\n" +
                        $"**Дата:** {DateTime.Now}");
                }
            }
        }

        private static async void ClearAndRepairVotesOnElapsed(object sender, ElapsedEventArgs e)
        {
            for (int i = 0; i < Vote.Votes.Count; ++i)
            {
                var vote = Vote.Votes.Values.ToArray()[i];
                try
                {
                    var message = await Client.Guilds[Bot.BotSettings.Guild].GetChannel(Bot.BotSettings.VotesChannel)
                        .GetMessageAsync(vote.Message);
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
                    }
                    else if (DateTime.Now < vote.End) // починка голосования
                    {
                        if (message.Reactions.Count < 2)
                        {
                            await message.DeleteAllReactionsAsync();
                            await message.CreateReactionAsync(DiscordEmoji.FromName(Client, ":white_check_mark:"));
                            await message.CreateReactionAsync(DiscordEmoji.FromName(Client, ":no_entry:"));
                        }
                    }
                }
                catch (NotFoundException)
                {
                    //Do nothing, message not found
                }
                catch (Exception ex)
                {
                    Client.DebugLogger.LogMessage(LogLevel.Error, "Bot",
                        $"Возникла ошибка при очистке голосований {ex.StackTrace}.",
                        DateTime.Now);
                }
            }
        }

        /// <summary>
        ///     Очистка сообщений из каналов
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static async void ClearChannelMessagesOnElapsed(object sender, ElapsedEventArgs e)
        {
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
                            .Where(x => !x.Pinned).ToList()                                                                           //Не закрепленные сообщения
                            .Where(x => DateTimeOffset.Now.Subtract(x.CreationTimestamp.Add(channel.Value)).TotalSeconds > 0);     //Опубликованные ранее определенного времени

                        if (toDelete.Count() > 0)
                        {
                            await channel.Key.DeleteMessagesAsync(toDelete);
                            Client.DebugLogger.LogMessage(LogLevel.Info, "Bot", $"Канал {channel.Key.Name} был очищен.", DateTime.Now);
                        }

                    }
                    catch (Exception ex)
                    {
                        Client.DebugLogger.LogMessage(LogLevel.Warning, "Bot", $"Ошибка при удалении сообщений в {channel.Key.Name}. \n{ex.Message}", DateTime.Now);
                    }
                }
            }
            catch (Exception ex)
            {
                Client.DebugLogger.LogMessage(LogLevel.Error, "Bot", $"Ошибка при удалении сообщений в каналах. \n" +
                        $"{ex.GetType()}" +
                        $"{ex.Message}\n" +
                        $"{ex.StackTrace}",
                    DateTime.Now);
            }
        }

        private static async void CheckExpiredReports(object sender, ElapsedEventArgs e)
        {
            var guild = await Client.GetGuildAsync(Bot.BotSettings.Guild);

            //Check for expired bans
            var toUnban = BanSQL.GetExpiredBans();

            if (toUnban.Any())
            {
                foreach (var ban in toUnban)
                {
                    try
                    {
                        await guild.UnbanMemberAsync(ban.User);
                    }
                    catch (NotFoundException)
                    {
                        //пользователь мог и не быть заблокирован через Discord
                    }

                    var user = await Client.GetUserAsync(ban.User);
                    await guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                        "**Снятие бана**\n\n" +
                        $"**Модератор:** {Client.CurrentUser.Username}\n" +
                        $"**Пользователь:** {user}\n" +
                        $"**Дата:** {DateTime.Now}\n");

                    Client.DebugLogger.LogMessage(LogLevel.Info, "Bot", $"Пользователь {user} был разбанен.", DateTime.Now);
                }
            }

            //Check for expired mutes
            var count = ReportList.Mutes.Count;
            ReportList.Mutes.Values.Where(x => x.Expired()).ToList()
                .ForEach(async x =>
                {
                    ReportList.Mutes.Remove(x.Id);
                    try
                    {
                        var user = await guild.GetMemberAsync(x.Id);
                        await user.RevokeRoleAsync(guild.GetRole(Bot.BotSettings.MuteRole), "Unmuted");
                    }
                    catch (NotFoundException)
                    {
                        //Пользователь не найден
                    }
                });
            if (count != ReportList.Mutes.Count)
                ReportList.SaveToXML(Bot.BotSettings.ReportsXML);

            //Check for expired voice mutes
            count = ReportList.VoiceMutes.Count;
            ReportList.VoiceMutes.Values.Where(x => x.Expired()).ToList()
                .ForEach(async x =>
                {
                    ReportList.VoiceMutes.Remove(x.Id);
                    try
                    {
                        var user = await guild.GetMemberAsync(x.Id);
                        await user.RevokeRoleAsync(guild.GetRole(Bot.BotSettings.VoiceMuteRole), "Unmuted");
                    }
                    catch (NotFoundException)
                    {
                        //Пользователь не найден
                    }
                });
            if (count != ReportList.VoiceMutes.Count)
                ReportList.SaveToXML(Bot.BotSettings.ReportsXML);
        }
    }
}
