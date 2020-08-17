using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;
using Bot_NetCore.Commands;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus.CommandsNext.Exceptions;
using System.Reflection;
using DSharpPlus.Interactivity.Enums;
using Microsoft.VisualBasic.FileIO;
using DSharpPlus.CommandsNext.Attributes;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnassignedField.Global

namespace Bot_NetCore
{
    /// <summary>
    ///     Основной класс бота
    /// </summary>
    internal sealed class Bot
    {
        /// <summary>
        ///     Словарь, содержащий в качестве ключа пользователя Discord, а в качестве значения - время истечения кулдауна.
        /// </summary>
        public static Dictionary<DiscordUser, DateTime> ShipCooldowns = new Dictionary<DiscordUser, DateTime>();

        /// <summary>
        ///     Словарь, содержащий в качестве ключа пользователя Discord, а в качестве значения - время истечения кулдауна.
        /// </summary>
        public static Dictionary<DiscordUser, DateTime> EmojiCooldowns = new Dictionary<DiscordUser, DateTime>();

        ///     Invites список приглашений
        /// </summary>
        public List<DiscordInvite> Invites;

        /// <summary>
        ///     DiscordClient бота.
        /// </summary>
        public DiscordClient Client { get; set; }

        /// <summary>
        ///     Модуль команд.
        /// </summary>
        public CommandsNextExtension Commands { get; set; }

        /// <summary>
        ///     Модуль взаимодействия.
        /// </summary>
        public InteractivityExtension Interactivity { get; set; }

        /// <summary>
        ///     Структура с настройками бота.
        /// </summary>
        public static Settings BotSettings { get; private set; }

        public static void Main(string[] args)
        {
            var bot = new Bot();

            Console.WriteLine(@"   
                ██████╗    ██╗██╗  ██╗
                ╚════██╗  ███║██║  ██║
                 █████╔╝  ╚██║███████║
                ██╔═══╝    ██║╚════██║
                ███████╗██╗██║     ██║
                ╚══════╝╚═╝╚═╝     ╚═╝
            "); //Font Name: ANSI Shadow

            ReloadSettings(); // Загрузим настройки

            ShipList.ReadFromXML(BotSettings.ShipXML);
            DonatorList.ReadFromXML(BotSettings.DonatorXML);
            UserList.ReadFromXML(BotSettings.WarningsXML);
            BanList.ReadFromXML(BotSettings.BanXML);
            InviterList.ReadFromXML(BotSettings.InviterXML);
            ReportList.ReadFromXML(BotSettings.ReportsXML);
            UsersLeftList.ReadFromXML(BotSettings.UsersLeftXML);
            PriceList.ReadFromXML(BotSettings.PriceListXML);
            Vote.Read(BotSettings.VotesXML);

            bot.RunBotAsync().GetAwaiter().GetResult();
        }

        public async Task RunBotAsync()
        {
            var cfg = new DiscordConfiguration
            {
                Token = BotSettings.Token,
                LogLevel = LogLevel.Info,
                AutoReconnect = true,
                TokenType = TokenType.Bot,
                UseInternalLogHandler = true
            };

            Client = new DiscordClient(cfg);

            var ccfg = new CommandsNextConfiguration
            {
                StringPrefixes = new[] { BotSettings.Prefix },
                EnableDms = false,
                CaseSensitive = false,
                EnableMentionPrefix = true
            };

            Commands = Client.UseCommandsNext(ccfg);

            var icfg = new InteractivityConfiguration
            {
                PaginationBehaviour = PaginationBehaviour.WrapAround,
                PaginationDeletion = PaginationDeletion.DeleteEmojis,
                Timeout = TimeSpan.FromMinutes(2)
            };

            Interactivity = Client.UseInteractivity(icfg);

            Commands.RegisterCommands(Assembly.GetExecutingAssembly());
            Commands.SetHelpFormatter<HelpFormatter>();

            //Ивенты
            Client.Ready += ClientOnReady;
            Client.GuildMemberAdded += ClientOnGuildMemberAdded;
            Client.GuildMemberRemoved += ClientOnGuildMemberRemoved;
            Client.MessageDeleted += ClientOnMessageDeleted;
            Client.VoiceStateUpdated += ClientOnVoiceStateUpdated;
            Client.MessageCreated += ClientOnMessageCreated;
            Client.MessageReactionAdded += ClientOnMessageReactionAdded;
            Client.InviteCreated += ClientOnInviteCreated;
            Client.InviteDeleted += ClientOnInviteDeleted;
            Client.ClientErrored += ClientOnErrored;
            Client.GuildAvailable += ClientOnGuildAvailable;

            //Не используются
            //Client.MessageReactionRemoved += ClientOnMessageReactionRemoved; //Не нужный ивент

            //Логгер
            Client.DebugLogger.LogMessageReceived += DebugLoggerOnLogMessageReceived;
#if DEBUG
            Client.ClientErrored += args =>
            {
                Console.WriteLine(args.Exception.InnerException);
                return Task.CompletedTask;
            };
#endif

            Commands.CommandErrored += CommandsOnCommandErrored;
            Commands.CommandExecuted += CommandsOnCommandExecuted;

            await Client.ConnectAsync();

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

            if (!Directory.Exists("generated")) Directory.CreateDirectory("generated");
            if (!File.Exists("generated/attachments_messages.csv")) File.Create("generated/attachments_messages.csv");

            await Task.Delay(-1);
        }

        private async Task ClientOnGuildAvailable(GuildCreateEventArgs e)
        {
            await UpdateMembersCountAsync(Client, e.Guild.MemberCount);
        }

        private async void DeleteShipsOnElapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var ship in ShipList.Ships.Values)
            {
                if ((DateTime.Now - ship.LastUsed).Days >= 3)
                {
                    var channel = Client.Guilds[BotSettings.Guild].GetChannel(ship.Channel);

                    DiscordMember owner = null;
                    foreach (var member in ship.Members.Values)
                        if (member.Type == MemberType.Owner)
                        {
                            owner = await Client.Guilds[BotSettings.Guild].GetMemberAsync(member.Id);
                            break;
                        }
                    
                    ship.Delete();
                    ShipList.SaveToXML(Bot.BotSettings.ShipXML);

                    await channel.DeleteAsync();
                    
                    var doc = XDocument.Load("actions.xml");
                    foreach (var action in doc.Element("actions").Elements("action"))
                        if (owner != null && Convert.ToUInt64(action.Value) == owner.Id)
                            action.Remove();
                    doc.Save("actions.xml");

                    if (owner != null)
                        await owner.SendMessageAsync(
                            "Ваш приватный корабль был неактивен долгое время и поэтому он был удалён. \n**Пожалуйста, не отправляйте новый запрос на создание, если" +
                            "не планируете пользоваться этой функцией**");
                    
                    await Client.Guilds[BotSettings.Guild].GetChannel(BotSettings.ModlogChannel).SendMessageAsync(
                        "**Удаление корабля**\n\n" +
                        $"**Модератор:** {Client.CurrentUser}\n" +
                        $"**Корабль:** {ship.Name}\n" +
                        $"**Владелец:** {owner}\n" +
                        $"**Дата:** {DateTime.Now}");
                }
            }
        }

        private async void ClearAndRepairVotesOnElapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var vote in Vote.Votes.Values)
            {
                try
                {
                    var message = await Client.Guilds[BotSettings.Guild].GetChannel(BotSettings.VotesChannel)
                        .GetMessageAsync(vote.Message);
                    if (DateTime.Now > vote.End)
                    {
                        if (message.Reactions.Count == 0) continue;

                        var author = await Client.Guilds[BotSettings.Guild].GetMemberAsync(vote.Author);
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
                    else
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

        private Task ClientOnErrored(ClientErrorEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Warning, "Bot",
                $"Возникла ошибка при выполнении ивента {e.EventName}.",
                DateTime.Now);
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Очистка сообщений из каналов
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ClearChannelMessagesOnElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                var guild = Client.Guilds[BotSettings.Guild];

                var channels = new Dictionary<DiscordChannel, TimeSpan>
                {
                    { guild.GetChannel(BotSettings.FindChannel), new TimeSpan(0, 30, 0) },           //30 минут для канала поиска
                    { guild.GetChannel(BotSettings.FleetCreationChannel), new TimeSpan(24, 0, 0) }   //24 часа для канала создания рейда
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

        private Task CommandsOnCommandExecuted(CommandExecutionEventArgs e)
        {
            var command = (e.Command.Parent != null ? e.Command.Parent.Name + " " : "") + e.Command.Name;

            e.Context.Client.DebugLogger.LogMessage(LogLevel.Info,
                    "Bot",
                    $"Пользователь {e.Context.Member.Username}#{e.Context.Member.Discriminator} ({e.Context.Member.Id}) выполнил команду {command}",
                    DateTime.Now);
            return Task.CompletedTask; //Пришлось добавить, выдавало ошибку при компиляции
        }

#nullable enable //Выдавало warning
        private async void DebugLoggerOnLogMessageReceived(object? sender, DebugLogMessageEventArgs e)
        {
            if (!Directory.Exists("logs")) Directory.CreateDirectory("logs");

            var fileName = "logs/" + DateTime.Today.ToString("yyyy-MM-dd");

            var loglevel = "";
            switch (e.Level)
            {
                case LogLevel.Critical:
                    loglevel = "Critical";
                    break;
                case LogLevel.Error:
                    loglevel = "Error";
                    break;
                case LogLevel.Warning:
                    loglevel = "Warning";
                    break;
                case LogLevel.Info:
                    loglevel = "Info";
                    break;
                case LogLevel.Debug:
                    loglevel = "Debug";
                    break;
            }

            //файл для удобного парсинга
            using (var fs = new FileStream(fileName + ".csv", FileMode.Append))
            {
                using (var sw = new StreamWriter(fs))
                {
                    var message = e.Message.Replace("\"", "'");
                    await sw.WriteLineAsync($"{e.Timestamp:s},{loglevel},{e.Application},\"{message}\"");
                }
            }

            //файл для удобного просмотра
            using (var fs = new FileStream(fileName + ".log", FileMode.Append))
            {
                using (var sw = new StreamWriter(fs))
                {
                    await sw.WriteLineAsync($"[{e.Timestamp:G}] [{loglevel}] [{e.Application}] {e.Message}");
                }
            }
        }
#nullable disable

        private async void CheckExpiredReports(object sender, ElapsedEventArgs e)
        {
            var guild = await Client.GetGuildAsync(BotSettings.Guild);

            //Check for expired bans
            var toUnban = from ban in BanList.BannedMembers.Values
                          where ban.UnbanDateTime <= DateTime.Now
                          select ban;

            if (toUnban.Count() > 0)
            {
                foreach (var ban in toUnban)
                {
                    try
                    {
                        await guild.UnbanMemberAsync(ban.Id);
                    }
                    catch (NotFoundException)
                    {
                        //пользователь мог и не быть заблокирован через Discord
                    }

                    ban.Unban();

                    var user = await Client.GetUserAsync(ban.Id);
                    await guild.GetChannel(BotSettings.ModlogChannel).SendMessageAsync(
                        "**Снятие Бана**\n\n" +
                        $"**Модератор:** {Client.CurrentUser.Username}\n" +
                        $"**Пользователь:** {user}\n" +
                        $"**Дата:** {DateTime.Now}\n");

                    Client.DebugLogger.LogMessage(LogLevel.Info, "Bot", $"Пользователь {user} был разбанен.", DateTime.Now);
                }

                BanList.SaveToXML(BotSettings.BanXML);

                Client.DebugLogger.LogMessage(LogLevel.Info, "Bot", "Бан-лист был обновлён.", DateTime.Now);
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
                        await user.RevokeRoleAsync(guild.GetRole(BotSettings.MuteRole), "Unmuted");
                    }
                    catch (NotFoundException)
                    {
                        //Пользователь не найден
                    }
                });
            if (count != ReportList.Mutes.Count)
                ReportList.SaveToXML(BotSettings.ReportsXML);

            //Check for expired voice mutes
            count = ReportList.VoiceMutes.Count;
            ReportList.VoiceMutes.Values.Where(x => x.Expired()).ToList()
                .ForEach(async x =>
                {
                    ReportList.VoiceMutes.Remove(x.Id);
                    try
                    {
                        var user = await guild.GetMemberAsync(x.Id);
                        await user.RevokeRoleAsync(guild.GetRole(BotSettings.VoiceMuteRole), "Unmuted");
                    }
                    catch (NotFoundException)
                    {
                        //Пользователь не найден
                    }
                });
            if (count != ReportList.VoiceMutes.Count)
                ReportList.SaveToXML(BotSettings.ReportsXML);
        }

        private async Task ClientOnMessageReactionRemoved(MessageReactionRemoveEventArgs e)
        {
            if (e.User.IsBot) return;

            //Проверка если сообщение с принятием правил
            if (e.Message.Id == BotSettings.CodexMessageId)
            {
                //При надобности добавить кулдаун
                //if (EmojiCooldowns.ContainsKey(e.User)) // проверка на кулдаун
                //    if ((EmojiCooldowns[e.User] - DateTime.Now).Seconds > 0) return;

                //// если проверка успешно пройдена, добавим пользователя
                //// в словарь кулдаунов
                //EmojiCooldowns[e.User] = DateTime.Now.AddSeconds(BotSettings.FastCooldown);

                //Забираем роль
                var user = (DiscordMember)e.User;
                if (user.Roles.Any(x => x.Id == BotSettings.CodexRole))
                    await user.RevokeRoleAsync(e.Channel.Guild.GetRole(BotSettings.CodexRole));

                return;
            }

            //Emissary Message
            if (e.Message.Id == BotSettings.EmissaryMessageId) return;
        }

        private async Task ClientOnMessageReactionAdded(MessageReactionAddEventArgs e)
        {
            if (e.User.IsBot) return;

            //Проверка если сообщение с принятием правил
            if (e.Message.Id == BotSettings.CodexMessageId && e.Emoji.GetDiscordName() == ":white_check_mark:")
            {
                //При надобности добавить кулдаун
                /*if (EmojiCooldowns.ContainsKey(e.User)) // проверка на кулдаун
                    if ((EmojiCooldowns[e.User] - DateTime.Now).Seconds > 0) return;

                // если проверка успешно пройдена, добавим пользователя
                // в словарь кулдаунов
                EmojiCooldowns[e.User] = DateTime.Now.AddSeconds(BotSettings.FastCooldown);*/

                //Проверка на purge
                if (ReportList.CodexPurges.ContainsKey(e.User.Id))
                    if (!ReportList.CodexPurges[e.User.Id].Expired()) //Проверка истекшей блокировки
                    {
                        var moderator = await e.Channel.Guild.GetMemberAsync(ReportList.CodexPurges[e.User.Id].Moderator);
                        try
                        {
                            await ((DiscordMember)e.User).SendMessageAsync(
                                "**Возможность принять правила заблокирована**\n" +
                                $"**Снятие через:** {Utility.FormatTimespan(ReportList.CodexPurges[e.User.Id].getRemainingTime())}\n" +
                                $"**Модератор:** {moderator.Username}#{moderator.Discriminator}\n" +
                                $"**Причина:** {ReportList.CodexPurges[e.User.Id].Reason}\n");
                        }

                        catch (UnauthorizedException)
                        {
                            //user can block the bot
                        }
                        return;
                    }
                    else
                        ReportList.CodexPurges.Remove(e.User.Id); //Удаляем блокировку если истекла

                //Выдаем роль правил
                var user = (DiscordMember)e.User;
                if (!user.Roles.Contains(e.Channel.Guild.GetRole(BotSettings.CodexRole)))
                {
                    //Выдаем роль правил
                    await user.GrantRoleAsync(e.Channel.Guild.GetRole(BotSettings.CodexRole));

                    //Убираем роль блокировки правил
                    await user.RevokeRoleAsync(e.Channel.Guild.GetRole(BotSettings.PurgeCodexRole));

                    e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                        $"Пользователь {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) подтвердил прочтение правил через реакцию.",
                        DateTime.Now);
                }

                return;
            }

            //Проверка если сообщение с принятием правил
            if (e.Message.Id == BotSettings.FleetCodexMessageId && e.Emoji.GetDiscordName() == ":white_check_mark:")
            {
                //Проверка на purge
                if (ReportList.FleetPurges.ContainsKey(e.User.Id))
                    if (!ReportList.FleetPurges[e.User.Id].Expired()) //Проверка истекшей блокировки
                    {
                        var moderator = await e.Channel.Guild.GetMemberAsync(ReportList.FleetPurges[e.User.Id].Moderator);
                        try
                        {
                            await ((DiscordMember)e.User).SendMessageAsync(
                                "**Возможность принять правила рейда заблокирована**\n" +
                                $"**Снятие через:** {Utility.FormatTimespan(ReportList.FleetPurges[e.User.Id].getRemainingTime())}\n" +
                                $"**Модератор:** {moderator.Username}#{moderator.Discriminator}\n" +
                                $"**Причина:** {ReportList.FleetPurges[e.User.Id].Reason}\n");
                        }

                        catch (UnauthorizedException)
                        {
                            //user can block the bot
                        }
                        return;
                    }
                    else
                        ReportList.FleetPurges.Remove(e.User.Id); //Удаляем блокировку если истекла

                //Выдаем роль правил рейда
                var user = (DiscordMember)e.User;
                if (!user.Roles.Any(x => x.Id == BotSettings.FleetCodexRole))
                {
                    await user.GrantRoleAsync(e.Channel.Guild.GetRole(BotSettings.FleetCodexRole));
                    e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                        $"Пользователь {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) подтвердил прочтение правил рейда.",
                        DateTime.Now);
                }

                return;
            }

            //Проверка на сообщение эмиссарства
            if (e.Message.Id == BotSettings.EmissaryMessageId)
            {
                await e.Message.DeleteReactionAsync(e.Emoji, e.User);

                if (EmojiCooldowns.ContainsKey(e.User)) // проверка на кулдаун
                    if ((EmojiCooldowns[e.User] - DateTime.Now).Seconds > 0) return;

                // если проверка успешно пройдена, добавим пользователя
                // в словарь кулдаунов
                EmojiCooldowns[e.User] = DateTime.Now.AddSeconds(BotSettings.FastCooldown);

                //Проверка у пользователя уже существующих ролей эмисарства и их удаление
                var user = (DiscordMember)e.User;
                user.Roles.Where(x => x.Id == BotSettings.EmissaryGoldhoadersRole ||
                                x.Id == BotSettings.EmissaryTradingCompanyRole ||
                                x.Id == BotSettings.EmissaryOrderOfSoulsRole ||
                                x.Id == BotSettings.EmissaryAthenaRole ||
                                x.Id == BotSettings.EmissaryReaperBonesRole ||
                                x.Id == BotSettings.HuntersRole ||
                                x.Id == BotSettings.ArenaRole).ToList()
                         .ForEach(async x => await user.RevokeRoleAsync(x));

                //Выдаем роль в зависимости от реакции
                switch (e.Emoji.GetDiscordName())
                {
                    case ":moneybag:":
                        await user.GrantRoleAsync(e.Channel.Guild.GetRole(BotSettings.EmissaryGoldhoadersRole));
                        break;
                    case ":pig:":
                        await user.GrantRoleAsync(e.Channel.Guild.GetRole(BotSettings.EmissaryTradingCompanyRole));
                        break;
                    case ":skull:":
                        await user.GrantRoleAsync(e.Channel.Guild.GetRole(BotSettings.EmissaryOrderOfSoulsRole));
                        break;
                    case ":gem:":
                        await user.GrantRoleAsync(e.Channel.Guild.GetRole(BotSettings.EmissaryAthenaRole));
                        break;
                    case ":skull_crossbones:":
                        await user.GrantRoleAsync(e.Channel.Guild.GetRole(BotSettings.EmissaryReaperBonesRole));
                        break;
                    case ":fish:":
                        await user.GrantRoleAsync(e.Channel.Guild.GetRole(BotSettings.HuntersRole));
                        break;
                    case ":crossed_swords:":
                        await user.GrantRoleAsync(e.Channel.Guild.GetRole(BotSettings.ArenaRole));
                        break;
                    default:
                        break;
                }
                //Отправка в лог
                e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT",
                    $"{e.User.Username}#{e.User.Discriminator} получил новую роль эмиссарства.",
                    DateTime.Now);

                return;
            }

            //Проверка на голосование
            if (e.Message.Channel.Id == BotSettings.VotesChannel)
            {
                var vote = Vote.Votes[e.Message.Id];

                await e.Message.DeleteReactionAsync(e.Emoji, e.User);

                // Проверка на окончание голосования
                if (DateTime.Now > vote.End)
                {
                    return;
                }

                // Проверка на предыдущий голос
                if (vote.Voters.Contains(e.User.Id))
                {
                    return;
                }

                vote.Voters.Add(e.User.Id);
                var total = vote.Voters.Count;

                if (e.Emoji.GetDiscordName() == ":white_check_mark:")
                    ++vote.Yes;
                else ++vote.No;

                var author = await e.Guild.GetMemberAsync(vote.Author);
                var embed = Utility.GenerateVoteEmbed(
                    author, 
                    DiscordColor.Yellow, 
                    vote.Topic, 
                    vote.End,
                    vote.Voters.Count, 
                    vote.Yes, 
                    vote.No, 
                    vote.Id);

                Vote.Votes[e.Message.Id] = vote;
                Vote.Save(BotSettings.VotesXML);

                await e.Message.ModifyAsync(embed: embed);
                await (await e.Guild.GetMemberAsync(e.User.Id)).SendMessageAsync($"{BotSettings.OkEmoji} Спасибо, ваш голос учтён!");
            }

            //then check if it is a private ship confirmation message
            foreach (var ship in ShipList.Ships.Values)
            {
                if (ship.Status) continue;

                if (e.Message.Id == ship.CreationMessage)
                {
                    if (e.Emoji == DiscordEmoji.FromName((DiscordClient)e.Client, ":white_check_mark:"))
                    {
                        var name = ship.Name;
                        var channel = await e.Channel.Guild.CreateChannelAsync($"☠{name}☠", ChannelType.Voice,
                            e.Channel.Guild.GetChannel(BotSettings.PrivateCategory), bitrate: BotSettings.Bitrate);

                        var member = await e.Channel.Guild.GetMemberAsync(ShipList.Ships[name].Members.ToArray()[0].Value.Id);

                        await channel.AddOverwriteAsync(member, Permissions.UseVoice);
                        await channel.AddOverwriteAsync(e.Channel.Guild.GetRole(BotSettings.CodexRole), Permissions.AccessChannels);
                        await channel.AddOverwriteAsync(e.Channel.Guild.EveryoneRole, Permissions.None, Permissions.UseVoice);

                        ShipList.Ships[name].SetChannel(channel.Id);
                        ShipList.Ships[name].SetStatus(true);
                        ShipList.Ships[name].SetMemberStatus(member.Id, true);

                        ShipList.SaveToXML(BotSettings.ShipXML);

                        await member.SendMessageAsync(
                            $"{BotSettings.OkEmoji} Запрос на создание корабля **{name}** был подтвержден администратором **{e.User.Username}#{e.User.Discriminator}**");
                        await e.Channel.SendMessageAsync(
                            $"{BotSettings.OkEmoji} Администратор **{e.User.Username}#{e.User.Discriminator}** успешно подтвердил " +
                            $"запрос на создание корабля **{name}**!");

                        e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                            $"Администратор {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) подтвердил создание приватного корабля {name}.",
                            DateTime.Now);
                    }
                    else if (e.Emoji == DiscordEmoji.FromName((DiscordClient)e.Client, ":no_entry:"))
                    {
                        var name = ship.Name;
                        var member =
                            await e.Channel.Guild.GetMemberAsync(ShipList.Ships[name].Members.ToArray()[0].Value.Id);

                        ShipList.Ships[name].Delete();
                        ShipList.SaveToXML(BotSettings.ShipXML);

                        var doc = XDocument.Load("actions.xml");
                        foreach (var action in doc.Element("actions").Elements("action"))
                            if (Convert.ToUInt64(action.Value) == member.Id)
                                action.Remove();
                        doc.Save("actions.xml");

                        await member.SendMessageAsync(
                            $"{BotSettings.OkEmoji} Запрос на создание корабля **{name}** был отклонен администратором **{e.User.Username}#{e.User.Discriminator}**");
                        await e.Channel.SendMessageAsync(
                            $"{BotSettings.OkEmoji} Администратор **{e.User.Username}#{e.User.Discriminator}** успешно отклонил запрос на " +
                            $"создание корабля **{name}**!");

                        e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                            $"Администратор {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) отклонил создание приватного корабля {name}.",
                            DateTime.Now);
                    }
                    else
                    {
                        return;
                    }
                }
            }

        }

        /// <summary>
        ///     Новый обработчик команд.
        /// </summary>
        private async Task ClientOnMessageCreated(MessageCreateEventArgs e)
        {
            if (e.Channel.Id == BotSettings.CodexReserveChannel)
            {
                if (!IsModerator(await e.Guild.GetMemberAsync(e.Author.Id)))
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
                if (!user.Roles.Contains(e.Channel.Guild.GetRole(BotSettings.CodexRole)))
                {
                    //Выдаем роль правил
                    await user.GrantRoleAsync(e.Channel.Guild.GetRole(BotSettings.CodexRole));

                    //Убираем роль блокировки правил
                    await user.RevokeRoleAsync(e.Channel.Guild.GetRole(BotSettings.PurgeCodexRole));

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
                    var logMessage = await e.Guild.GetChannel(BotSettings.AttachmentsLog).SendFileAsync(file, message);
                    File.Delete(file);

                    using (var fs = new FileStream("generated/attachments_messages.csv", FileMode.Append))
                    using (var sw = new StreamWriter(fs))
                        await sw.WriteLineAsync($"{e.Message.Id},{logMessage.Id}");
                }
            }

            if (e.Message.Content.StartsWith("> "))
                if (IsModerator(await e.Guild.GetMemberAsync(e.Author.Id)))
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
                            RunCommand((DiscordClient)e.Client, CommandType.Warn, args, e.Message);
                            return;
                        default:
                            return;
                    }
                }
        }

        /// <summary>
        ///     Отлавливаем удаленные сообщения и отправляем в лог
        /// </summary>
        private async Task ClientOnMessageDeleted(MessageDeleteEventArgs e)
        {
            if (!GetMultiplySettingsSeparated(BotSettings.IgnoredChannels).Contains(e.Channel.Id)
                ) // в лог не должны отправляться сообщения,
                // удаленные из лога
                try
                {
                    //Каналы авто-очистки отправляются в отдельный канал.
                    if (e.Channel.Id == BotSettings.FindChannel ||
                        e.Channel.Id == BotSettings.FleetCreationChannel ||
                        e.Channel.Id == BotSettings.CodexReserveChannel)
                        await e.Guild.GetChannel(BotSettings.AutoclearLogChannel)
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
                                        (await e.Guild.GetChannel(BotSettings.AttachmentsLog)
                                            .GetMessageAsync(Convert.ToUInt64(fields[1]))).Attachments[0];

                                    var file = $"generated/attachments/{attachment.FileName}";

                                    var client = new WebClient();
                                    client.DownloadFile(attachment.Url, file);
                                    await e.Guild.GetChannel(BotSettings.FulllogChannel)
                                        .SendFileAsync(file, "**Удаление сообщения**\n" +
                                                          $"**Автор:** {e.Message.Author.Username}#{e.Message.Author.Discriminator} ({e.Message.Author.Id})\n" +
                                                          $"**Канал:** {e.Channel}\n" +
                                                          $"**Содержимое: ```{e.Message.Content}```**");
                                    File.Delete(file);
                                    return;
                                }
                            }
                        }
                        await e.Guild.GetChannel(BotSettings.FulllogChannel)
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

        /// <summary>
        ///     Лог посещений
        /// </summary>
        private async Task ClientOnGuildMemberRemoved(GuildMemberRemoveEventArgs e)
        {
            // Сохранение ролей участника
            var roles = e.Member.Roles;
            var rolesToSave = new List<ulong>();
            var ignoredRoles = new List<ulong> //роли, которые не нужно сохранять при выходе
            {
                BotSettings.CodexRole,
                BotSettings.FleetCodexRole,
                BotSettings.EmissaryAthenaRole,
                BotSettings.EmissaryGoldhoadersRole,
                BotSettings.EmissaryReaperBonesRole,
                BotSettings.EmissaryTradingCompanyRole,
                BotSettings.EmissaryOrderOfSoulsRole,
                e.Guild.EveryoneRole.Id,
            };

            foreach (var role in roles)
            {
                if (!ignoredRoles.Contains(role.Id))
                {
                    rolesToSave.Add(role.Id);
                }
            }

            if (rolesToSave.Count != 0)
            {
                UsersLeftList.Users[e.Member.Id] = new UserLeft(e.Member.Id, rolesToSave);
                UsersLeftList.SaveToXML(BotSettings.UsersLeftXML);
            }

            await e.Guild.GetChannel(BotSettings.UserlogChannel)
                .SendMessageAsync(
                    $"**Участник покинул сервер:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}).");

            //Обновляем статус бота
            await UpdateMembersCountAsync(e.Client, e.Guild.MemberCount);

            //Если пользователь не был никем приглашен, то при выходе он будет сохранен.
            if (!InviterList.Inviters.ToList().Any(i => i.Value.Referrals.ContainsKey(e.Member.Id)))
                InviterList.Inviters[0].AddReferral(e.Member.Id, false);

            //При выходе обновляем реферала на неактив.
            InviterList.Inviters.ToList().Where(i => i.Value.Referrals.ContainsKey(e.Member.Id)).ToList()
                                         .ForEach(i => i.Value.UpdateReferral(e.Member.Id, false));

            InviterList.SaveToXML(BotSettings.InviterXML);

            await LeaderboardCommands.UpdateLeaderboard(e.Guild);

            e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                $"Участник {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}) покинул сервер.",
                DateTime.Now);
        }

        /// <summary>
        ///     Приветственное сообщение + лог посещений + проверка на бан
        /// </summary>
        private async Task ClientOnGuildMemberAdded(GuildMemberAddEventArgs e)
        {
            //Проверка на бан
            if (BanList.BannedMembers.ContainsKey(e.Member.Id) && BanList.BannedMembers[e.Member.Id].UnbanDateTime > BanList.BannedMembers[e.Member.Id].BanDateTime)
            {
                var ban = BanList.BannedMembers[e.Member.Id];
                //Отправка сообщения в лс
                try
                {
                    await e.Member.SendMessageAsync($"Вы были заблокированы на этом сервере. **Причина:** " +
                                                $"{ban.Reason}. **Блокировка истекает:** ${ban.UnbanDateTime}.");
                }
                catch (UnauthorizedException)
                {
                    //user can block the bot
                }
                await e.Member.BanAsync(0, "Autoban");

                return;
            }

            //Проверка на mute
            if (ReportList.Mutes.ContainsKey(e.Member.Id) && !ReportList.Mutes[e.Member.Id].Expired())
            {
                //Отправка сообщения в лс
                try
                {
                    await e.Member.SendMessageAsync(
                        $"**Вам выдан мут на данном сервере**\n\n" +
                        $"**Снятие через:** {Utility.FormatTimespan(ReportList.Mutes[e.Member.Id].getRemainingTime())}\n" +
                        $"**Причина:** {ReportList.Mutes[e.Member.Id].Reason}");
                }
                catch (UnauthorizedException)
                {
                    //user can block the bot
                }

                //Выдаем роль мута
                await e.Member.GrantRoleAsync(e.Guild.GetRole(BotSettings.MuteRole));
            }

            //Проверка на voice mute
            if (ReportList.VoiceMutes.ContainsKey(e.Member.Id) && !ReportList.VoiceMutes[e.Member.Id].Expired())
            {
                //Отправка сообщения в лс
                try
                {
                    await e.Member.SendMessageAsync(
                        $"**Вам выдан голосовой мут на данном сервере**\n\n" +
                        $"**Снятие через:** {Utility.FormatTimespan(ReportList.VoiceMutes[e.Member.Id].getRemainingTime())}\n" +
                        $"**Причина:** {ReportList.VoiceMutes[e.Member.Id].Reason}");
                }
                catch (UnauthorizedException)
                {
                    //user can block the bot
                }

                //Выдаем роль мута
                await e.Member.GrantRoleAsync(e.Guild.GetRole(BotSettings.VoiceMuteRole));
            }

            //Проверка на purge
            if (ReportList.CodexPurges.ContainsKey(e.Member.Id) && !ReportList.CodexPurges[e.Member.Id].Expired())
                await e.Member.GrantRoleAsync(e.Guild.GetRole(BotSettings.PurgeCodexRole));

            //Выдача доступа к приватным кораблям
            try
            {
                var ships = ShipList.Ships.Values.Where(x => x.Members.ContainsKey(e.Member.Id));
                foreach (var ship in ships)
                    await e.Guild.GetChannel(ship.Channel).AddOverwriteAsync(e.Member, Permissions.UseVoice);
            }
            catch (Exception ex)
            {
                e.Client.DebugLogger.LogMessage(LogLevel.Warning, "Bot",
                   $"Ошибка при выдаче доступа к приватному кораблю. \n{ex.Message}\n{ex.StackTrace}",
                   DateTime.Now); ;
            }

            var invites = Invites.AsReadOnly().ToList(); //Сохраняем список старых инвайтов в локальную переменную
            var guildInvites = await e.Guild.GetInvitesAsync(); //Запрашиваем новый список инвайтов
            Invites = guildInvites.ToList(); //Обновляю список инвайтов

            try
            {
                await e.Member.SendMessageAsync($"**Привет, {e.Member.Mention}!\n**" +
                                                "Мы рады что ты присоединился к нашему сообществу :wink:!\n\n" +
                                                "Прежде чем приступать к игре, прочитай и прими правила в канале " +
                                                "`#👮-пиратский-кодекс-👮`. После принятия можешь ознакомиться с гайдом по боту" +
                                                "в канале `#📚-гайд-по-боту-📚`.\n" +
                                                "Если у тебя есть какие-то вопросы, не стесняйся писать администрации.\n\n" +
                                                "**Удачной игры!**");
            }
            catch (UnauthorizedException)
            {
                //Пользователь заблокировал бота
            }

            // Выдача ролей, которые были у участника перед выходом.
            if (UsersLeftList.Users.ContainsKey(e.Member.Id))
            {
                foreach (var role in UsersLeftList.Users[e.Member.Id].Roles)
                {
                    try
                    {
                        await e.Member.GrantRoleAsync(e.Guild.GetRole(role));
                    }
                    catch (NotFoundException)
                    {

                    }
                }

                UsersLeftList.Users.Remove(e.Member.Id);
                UsersLeftList.SaveToXML(BotSettings.UsersLeftXML);
            }

            try
            {
                //Находит обновившийся инвайт по количеству приглашений
                //Вызывает NullReferenceException в случае если ссылка только для одного использования
                var updatedInvite = guildInvites.ToList().Find(g => invites.Find(i => i.Code == g.Code).Uses < g.Uses);

                //Если не удалось определить инвайт, значит его нет в новых так как к.во использований ограничено и он был удален
                if (updatedInvite == null)
                {
                    updatedInvite = invites.Where(p => guildInvites.All(p2 => p2.Code != p.Code))                       //Ищем удаленный инвайт
                                           .Where(x => (x.CreatedAt.AddSeconds(x.MaxAge) < DateTimeOffset.Now))      //Проверяем если он не истёк
                                           .FirstOrDefault();                                                           //С такими условиями будет только один такой инвайт
                }

                if (updatedInvite != null)
                {

                    await e.Guild.GetChannel(BotSettings.UserlogChannel)
                    .SendMessageAsync(
                        $"**Участник присоединился:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}) используя " +
                        $"приглашение {updatedInvite.Code} от участника {updatedInvite.Inviter.Username}#{updatedInvite.Inviter.Discriminator}. ");

                    e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                        $"Участник {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}) присоединился к серверу. Приглашение: {updatedInvite.Code} от участника {updatedInvite.Inviter.Username}#{updatedInvite.Inviter.Discriminator}.",
                        DateTime.Now);

                    //Проверяем если пригласивший уже существует, если нет то создаем
                    if (!InviterList.Inviters.ContainsKey(updatedInvite.Inviter.Id))
                        Inviter.Create(updatedInvite.Inviter.Id);

                    //Проверяем если пользователь был ранее приглашен другими и обновляем активность, если нет то вносим в список
                    if (InviterList.Inviters.ToList().Exists(x => x.Value.Referrals.ContainsKey(e.Member.Id)))
                        InviterList.Inviters.ToList().Where(x => x.Value.Referrals.ContainsKey(e.Member.Id)).ToList()
                            .ForEach(x => x.Value.UpdateReferral(e.Member.Id, true));
                    else
                        InviterList.Inviters[updatedInvite.Inviter.Id].AddReferral(e.Member.Id);

                    InviterList.SaveToXML(BotSettings.InviterXML);
                    //Обновление статистики приглашений
                    await LeaderboardCommands.UpdateLeaderboard(e.Guild);
                }
                else
                {
                    await e.Guild.GetChannel(BotSettings.UserlogChannel)
                        .SendMessageAsync(
                            $"**Участник присоединился:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}). " +
                            $"Приглашение не удалось определить.");

                    e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                        $"Участник {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}) присоединился к серверу. Приглашение не удалось определить.",
                        DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                await e.Guild.GetChannel(BotSettings.UserlogChannel)
                    .SendMessageAsync(
                        $"**Участник присоединился:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}). " +
                        $"При попытке отследить инвайт произошла ошибка.");

                e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                    $"Участник {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}) присоединился к серверу. Приглашение не удалось определить.",
                    DateTime.Now);

                e.Client.DebugLogger.LogMessage(LogLevel.Warning, "Bot",
                    "Не удалось определить приглашение.",
                    DateTime.Now);

                var errChannel = e.Guild.GetChannel(BotSettings.ErrorLog);

                var message = "**Ошибка при логгинге инвайта**\n" +
                              $"**Пользователь:** {e.Member}\n" +
                              $"**Исключение:** {ex.GetType()}:{ex.Message}\n" +
                              $"**Трассировка стека:** \n```{ex.StackTrace}```\n" +
                              $"{ex}";

                await errChannel.SendMessageAsync(message);
            }

            //Обновляем статус бота
            await UpdateMembersCountAsync(e.Client, e.Guild.MemberCount);
        }

        /// <summary>
        ///     Отправляем в консоль сообщения об ошибках при выполнении команды.
        /// </summary>
        private async Task CommandsOnCommandErrored(CommandErrorEventArgs e)
        {
            if (e.Exception is CommandNotFoundException) return;

            if (e.Command.Name == "dgenlist" && e.Exception is NotFoundException) return; //костыль

            if (e.Exception is ArgumentException &&
                e.Exception.Message.Contains("Could not convert specified value to given type."))
            {
                await e.Context.RespondAsync(
                    $"{BotSettings.ErrorEmoji} Не удалось выполнить команду. Проверьте правильность введенных параметров.");
                return;
            }

            if (e.Exception is ArgumentException &&
                e.Exception.Message == "Not enough arguments supplied to the command.")
            {
                await e.Context.RespondAsync(
                    $"{BotSettings.ErrorEmoji} Не удалось выполнить команду: вы ввели не все параметры.");
                return;
            }

            if (e.Exception is ArgumentNullException &&
                e.Exception.Message == "Value cannot be null. (Parameter 'key').")
            {
                await e.Context.RespondAsync(
                    $"{BotSettings.ErrorEmoji} Не удалось выполнить команду: вы ввели недопустимые параметры.");
                return;
            }

            if (e.Exception is NotFoundException)
            {
                await e.Context.RespondAsync($"{BotSettings.ErrorEmoji} Не был найден указанный пользователь.");
                return;
            }

            if (e.Exception is InvalidOperationException)
            {
                await e.Context.RespondAsync($"{BotSettings.ErrorEmoji} Не была найдена подкоманда.");
                return;
            }

            if (e.Exception is ChecksFailedException)
            {
                var msg = $"{BotSettings.ErrorEmoji} Не удалось выполнить команду: ";

                var ex = e.Exception as ChecksFailedException;
                foreach (var check in ex.FailedChecks)
                    if (check is CooldownAttribute)
                        msg += $"\n Подождите {Utility.FormatTimespan((check as CooldownAttribute).Reset)} после последнего запуска команды.";
                    else if (check is RequireBotPermissionsAttribute)
                        msg += "\n У бота недостаточно прав.";
                    else if (check is RequireOwnerAttribute)
                        msg += "\n Команда для приватных сообщений.";
                    else if (check is RequireGuildAttribute)
                        msg += "\n Доступна только на определённом  сервере.";
                    else if (check is RequireNsfwAttribute)
                        msg += "\n Команда для использования только в NSFW канале.";
                    else if (check is RequireOwnerAttribute)
                        msg += "\n Команда только для владельца бота.";
                    else if (check is RequirePermissionsAttribute)
                        msg += "\n У вас нет доступа к этой команде!";
                    else if (check is RequirePrefixesAttribute)
                        msg += "\n Команда работает только с определённым префиксом.";
                    else if (check is RequireRolesAttribute)
                        msg += "\n У вас нет доступа к этой команде!";
                    else if (check is RequireUserPermissionsAttribute)
                        msg += "\n У вас нет доступа к этой команде!";

                await e.Context.RespondAsync(msg);
                return;
            }

            var command = (e.Command.Parent != null ? e.Command.Parent.Name + " " : "") + e.Command.Name;

            e.Context.Client.DebugLogger.LogMessage(LogLevel.Warning, "SoT",
                $"Участник {e.Context.Member.Username}#{e.Context.Member.Discriminator} " +
                $"({e.Context.Member.Id}) пытался запустить команду {command}, но произошла ошибка.",
                DateTime.Now);

            await e.Context.RespondAsync(
                $"{BotSettings.ErrorEmoji} Возникла ошибка при выполнении команды **{command}**! Попробуйте ещё раз, если " +
                "ошибка повторяется - проверьте канал `#📚-гайд-по-боту📚`. " +
                $"**Информация об ошибке:** {e.Exception.Message}");

            var errChannel = e.Context.Guild.GetChannel(BotSettings.ErrorLog);

            var message = $"**Команда:** {command}\n" +
                          $"**Канал:** {e.Context.Channel}\n" +
                          $"**Пользователь:** {e.Context.Member}\n" +
                          $"**Исключение:** {e.Exception.GetType()}:{e.Exception.Message}\n" +
                          $"**Трассировка стека:** \n```{e.Exception.StackTrace}```";

            await errChannel.SendMessageAsync(message);
        }

        /// <summary>
        ///     Система автосоздания кораблей
        /// </summary>
        private async Task ClientOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            try
            {
                if (e.Channel.Id == BotSettings.AutocreateGalleon ||
                    e.Channel.Id == BotSettings.AutocreateBrigantine ||
                    e.Channel.Id == BotSettings.AutocreateSloop
                ) // мы создаем канал, если пользователь зашел в один из каналов автосоздания
                {
                    if (ShipCooldowns.ContainsKey(e.User)) // проверка на кулдаун
                        if ((ShipCooldowns[e.User] - DateTime.Now).Seconds > 0)
                        {
                            var m = await e.Guild.GetMemberAsync(e.User.Id);
                            await m.PlaceInAsync(e.Guild.GetChannel(BotSettings.WaitingRoom));
                            await m.SendMessageAsync($"{BotSettings.ErrorEmoji} Вам нужно подождать " +
                                                     $"**{(ShipCooldowns[e.User] - DateTime.Now).Seconds}** секунд прежде чем " +
                                                     "создавать новый корабль!");
                            e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                                $"Участник {e.User.Username}#{e.User.Discriminator} ({e.User.Discriminator}) был перемещён в комнату ожидания.",
                                DateTime.Now);
                            return;
                        }

                    // если проверка успешно пройдена, добавим пользователя
                    // в словарь кулдаунов
                    ShipCooldowns[e.User] = DateTime.Now.AddSeconds(BotSettings.FastCooldown);

                    //Проверка на эмиссарство
                    var channelSymbol = BotSettings.AutocreateSymbol;
                    ((DiscordMember)e.User).Roles.ToList().ForEach(x =>
                    {
                        if (x.Id == BotSettings.EmissaryGoldhoadersRole)
                            channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":moneybag:");
                        else if (x.Id == BotSettings.EmissaryTradingCompanyRole)
                            channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":pig:");
                        else if (x.Id == BotSettings.EmissaryOrderOfSoulsRole)
                            channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":skull:");
                        else if (x.Id == BotSettings.EmissaryAthenaRole)
                            channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":gem:");
                        else if (x.Id == BotSettings.EmissaryReaperBonesRole)
                            channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":skull_crossbones:");
                        else if (x.Id == BotSettings.HuntersRole)
                            channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":fish:");
                        else if (x.Id == BotSettings.ArenaRole)
                            channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":crossed_swords:");

                    });

                    DiscordChannel created = null;
                    // Проверяем канал в котором находится пользователь

                    if (e.Channel.Id == BotSettings.AutocreateSloop) //Шлюп
                        created = await e.Guild.CreateChannelAsync(
                            $"{channelSymbol} Шлюп {e.User.Username}", ChannelType.Voice,
                            e.Guild.GetChannel(BotSettings.AutocreateCategory), bitrate: BotSettings.Bitrate, userLimit: 2);
                    else if (e.Channel.Id == BotSettings.AutocreateBrigantine) // Бригантина
                        created = await e.Guild.CreateChannelAsync(
                            $"{channelSymbol} Бриг {e.User.Username}", ChannelType.Voice,
                            e.Guild.GetChannel(BotSettings.AutocreateCategory), bitrate: BotSettings.Bitrate, userLimit: 3);
                    else // Галеон
                        created = await e.Guild.CreateChannelAsync(
                            $"{channelSymbol} Галеон {e.User.Username}", ChannelType.Voice,
                            e.Guild.GetChannel(BotSettings.AutocreateCategory), bitrate: BotSettings.Bitrate, userLimit: 4);

                    var member = await e.Guild.GetMemberAsync(e.User.Id);

                    await member.PlaceInAsync(created);

                    e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                        $"Участник {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) создал канал через автосоздание.",
                        DateTime.Now);
                }
                else if (e.Channel.Id == BotSettings.FindShip)
                {
                    var shipCategory = e.Guild.GetChannel(BotSettings.AutocreateCategory);

                    var membersLookingForTeam = new List<ulong>();
                    foreach (var message in (await e.Guild.GetChannel(BotSettings.FindChannel).GetMessagesAsync(100)))
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
                        await m.PlaceInAsync(e.Guild.GetChannel(BotSettings.WaitingRoom));
                        await m.SendMessageAsync($"{BotSettings.ErrorEmoji} Не удалось найти подходящий корабль.");
                        return;
                    }
                    
                    var random = new Random();
                    var rShip = random.Next(0, possibleChannels.Count);

                    await m.PlaceInAsync(possibleChannels[rShip]);
                    e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot", $"Пользователь {m.Username}#{m.Discriminator} успешно воспользовался поиском корабля!", DateTime.Now);
                    return;
                }
                else if (e.Channel.ParentId == BotSettings.PrivateCategory)
                {
                    foreach (var ship in ShipList.Ships.Values)
                    {
                        if (ship.Channel == e.Channel.Id)
                        {
                            ship.SetLastUsed(DateTime.Now);
                            ShipList.SaveToXML(BotSettings.ShipXML);
                            break;
                        }
                    }
                }
            }
            catch (NullReferenceException) // исключение выбрасывается если пользователь покинул канал
            {
                // нам здесь ничего не надо делать, просто пропускаем
            }

            try
            {
                // удалим пустые каналы

                var autocreatedChannels = new List<DiscordChannel>(); // это все автосозданные каналы
                foreach (var channel in e.Guild.Channels.Values)
                    if (channel.Type == ChannelType.Voice
                        && channel.ParentId == BotSettings.AutocreateCategory)
                        autocreatedChannels.Add(channel);

                var notEmptyChannels = new List<DiscordChannel>(); // это все НЕ пустые каналы
                foreach (var voiceState in e.Guild.VoiceStates.Values) notEmptyChannels.Add(voiceState.Channel);

                var forDeletionChannels = autocreatedChannels.Except(notEmptyChannels); // это пустые каналы
                foreach (var channel in forDeletionChannels) await channel.DeleteAsync(); // мы их удаляем
            }
            catch (NotFoundException) // Если пользователь пересоздает канал перейдя с уже автосозданного канала
            {
                // пропускаем
            }

            //Проверка на пустые рейды
            if (e.Before != null && e.Before.Channel != null)
            {
                var leftChannel = e.Before.Channel;

                //Пользователь вышел из автоматически созданных каналов рейда
                if (leftChannel.Parent.Name.StartsWith("Рейд") &&
                   leftChannel.ParentId != BotSettings.FleetCategory &&
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
                        foreach (var emptyChannel in leftChannel.Parent.Children)
                            await emptyChannel.DeleteAsync();
                        await leftChannel.Parent.DeleteAsync();
                    }
                }
            }
        }

        /// <summary>
        ///     Проверка на создание инвайтов
        /// </summary>
        private async Task ClientOnInviteCreated(InviteCreateEventArgs e)
        {
            var guildInvites = await e.Client.Guilds[BotSettings.Guild].GetInvitesAsync();
            Invites = guildInvites.ToList();
        }

        /// <summary>
        ///     Проверка на удаление инвайтов
        /// </summary>
        private async Task ClientOnInviteDeleted(InviteDeleteEventArgs e)
        {
            var guildInvites = await e.Client.Guilds[BotSettings.Guild].GetInvitesAsync();
            Invites = guildInvites.ToList();
        }

        /// <summary>
        ///     Сообщение в лог о готовности клиента
        /// </summary>
        private async Task ClientOnReady(ReadyEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT", $"Sea Of Thieves Bot, version {BotSettings.Version}",
                DateTime.Now);
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT", "Made by Actis",
                DateTime.Now); // и еще немного ЧСВ

            var guild = e.Client.Guilds[BotSettings.Guild];

            var member = await guild.GetMemberAsync(e.Client.CurrentUser.Id);
            await member.ModifyAsync(x => x.Nickname = $"SeaOfThieves {BotSettings.Version}");

            var guildInvites = await guild.GetInvitesAsync();
            Invites = guildInvites.ToList();
        }


        /// <summary>
        ///     Обновляет статус бота, отображая к.во участников на сервере.
        /// </summary>
        /// <param name="client">Клиент бота</param>
        /// <param name="number">Число пользователей на сервере</param>
        /// <returns></returns>
        public async Task UpdateMembersCountAsync(DiscordClient client, int number)
        {
            await client.UpdateStatusAsync(new DiscordActivity($"за {number} пользователями", ActivityType.Watching));
        }


        /// <summary>
        ///     Загрузка и перезагрузка настроек
        /// </summary>
        public static void ReloadSettings()
        {
            var serializer = new XmlSerializer(typeof(Settings));
            using (var fs = new FileStream("settings.xml", FileMode.Open))
            {
                var reader = XmlReader.Create(fs);
                BotSettings = (Settings)serializer.Deserialize(reader);
            }
        }

        /// <summary>
        ///     Обновляет параметр в настройках бота и перезагружает их.
        /// </summary>
        public static void EditSettings(string param, string value)
        {
            try
            {
                if (param == "Token") throw new Exception("Невозможно изменить Token бота");
                var doc = XDocument.Load("settings.xml", LoadOptions.PreserveWhitespace);
                var elem = doc.Element("Settings").Element(param);

                elem.Value = value;
                doc.Save("settings.xml");
                ReloadSettings();
            }
            catch (NullReferenceException)
            {
                throw new NullReferenceException("Не был найден указанный параметр");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        ///     Предназначено для разделения строки с настройками (например, IgnoredChannels)
        /// </summary>
        /// <param name="notSeparatedStrings">Строка, считанная из XML</param>
        /// <returns></returns>
        public static List<ulong> GetMultiplySettingsSeparated(string notSeparatedStrings) //
        {
            var separatedStrings = notSeparatedStrings.Split(',');
            var result = new List<ulong>();
            foreach (var separatedString in separatedStrings)
                try
                {
                    result.Add(Convert.ToUInt64(separatedString));
                }
                catch
                {
                }

            return result;
        }

        public static bool IsModerator(DiscordMember member)
        {
            foreach (var role in member.Roles)
                if (GetMultiplySettingsSeparated(BotSettings.AdminRoles).Contains(role.Id))
                    return true;

            return false;
        }

        private static async void RunCommand(DiscordClient client, CommandType type, string[] args,
            DiscordMessage message)
        {
            switch (type)
            {
                case CommandType.Warn:
                    if (args.Length < 2) return;

                    var moderator = await message.Channel.Guild.GetMemberAsync(message.Author.Id);

                    try
                    {
                        var messageId =
                            Convert.ToUInt64(args[0].TrimStart('<').TrimStart('@').TrimStart('!').TrimEnd('>'));
                        var member = await message.Channel.Guild.GetMemberAsync(messageId);

                        var reason = "Не указана";
                        if (args.Length > 2)
                        {
                            reason = "";
                            if (args.Length == 3) reason = args[2];
                            else
                                for (var i = 2; i < args.Length; ++i)
                                    reason += args[i] + " ";
                        }

                        ModerationCommands.Warn(client, moderator, message.Channel.Guild, member, reason);
                    }
                    catch (FormatException)
                    {
                        await moderator.SendMessageAsync(
                            $"{BotSettings.ErrorEmoji} Не удалось отправить предупреждение!");
                    }
                    catch (NotFoundException)
                    {
                        await moderator.SendMessageAsync(
                            $"{BotSettings.ErrorEmoji} Не удалось отправить предупреждение!");
                    }
                    catch (Exception e)
                    {
                        var errChannel = message.Channel.Guild.GetChannel(BotSettings.ErrorLog);

                        var msg = "**Команда:** warn\n" +
                                  $"**Канал:** {message.Channel}\n" +
                                  $"**Пользователь:** {moderator}\n" +
                                  $"**Исключение:** {e.GetType()}:{e.Message}\n" +
                                  $"**Трассировка стека:** \n```{e.StackTrace}```";

                        await errChannel.SendMessageAsync(msg);
                    }

                    return;
            }
        }
    }

    /// <summary>
    ///     Структура с настройками бота.
    /// </summary>
    public struct Settings
    {
        /// <summary>
        ///     Версия.
        /// </summary>
        public string Version;

        /// <summary>
        ///     ID основного сервера.
        /// </summary>
        public ulong Guild;

        /// <summary>
        ///     Токен Discord.
        /// </summary>
        public string Token;

        /// <summary>
        ///     Префикс команд.
        /// </summary>
        public string Prefix;

        /// <summary>
        ///     ID роли бота.
        /// </summary>
        public ulong BotRole;

        /// <summary>
        ///     Id категории донатных ролей
        /// </summary>
        public ulong DonatorSpacerRole;

        /// <summary>
        ///     Текстовый код эмодзи, отправляемого при успешной операции.
        /// </summary>
        public string OkEmoji;

        /// <summary>
        ///     Текстовый код эмодзи, отправляемого при неудачной операции.
        /// </summary>
        public string ErrorEmoji;

        /// <summary>
        ///     ID канала в который отправляются роли (устарело).
        /// </summary>
        public ulong RolesChannel;

        /// <summary>
        ///     ID категории быстрых кораблей
        /// </summary>
        public ulong AutocreateCategory;

        /// <summary>
        ///     ID канала автосоздания для галеона.
        /// </summary>
        public ulong AutocreateGalleon;

        /// <summary>
        ///     ID канала автосоздания для бригантины.
        /// </summary>
        public ulong AutocreateBrigantine;

        /// <summary>
        ///     ID канала автосоздания для шлюпа.
        /// </summary>
        public ulong AutocreateSloop;

        /// <summary>
        ///     Символ, с которого начинаеются названия автосозданных кораблей.
        /// </summary>
        public string AutocreateSymbol;

        /// <summary>
        ///     Битрейт создаваемых каналов.
        /// </summary>
        public int Bitrate;

        /// <summary>
        ///     Кулдаун на создание быстрых кораблей.
        /// </summary>
        public int FastCooldown;

        /// <summary>
        ///     ID канала в который переносятся пользователи, кулдаун которых не истек.
        /// </summary>
        public ulong WaitingRoom;


        /// <summary>
        ///     ID категории рейдов.
        /// </summary>
        public ulong FleetCategory;

        /// <summary>
        ///     ID канала Chill в категории рейдов.
        /// </summary>
        public ulong FleetChillChannel;

        /// <summary>
        ///     ID канала лобби в категории рейдов.
        /// </summary>
        public ulong FleetLobby;

        /// <summary>
        ///     Начальное количество пользователей в канале рейда.
        /// </summary>
        public int FleetUserLimiter;

        /// <summary>
        ///     Путь до XML-файла с приватными кораблями.
        /// </summary>
        public string ShipXML;

        /// <summary>
        ///     ID категории с приватными кораблями.
        /// </summary>
        public ulong PrivateCategory;

        /// <summary>
        ///     ID канала, в который отправляются уведомления о запросах приватных кораблей.
        /// </summary>
        public ulong PrivateRequestsChannel;

        /// <summary>
        ///     Максимальное число приватных кораблей для пользователя.
        /// </summary>
        public int MaxPrivateShips;

        /// <summary>
        ///     Путь до XML-файлов с донатерами.
        /// </summary>
        public string DonatorXML;

        /// <summary>
        ///     ID роли донатеров.
        /// </summary>
        public ulong DonatorRole;

        /// <summary>
        ///     ID канала с топом донатеров.
        /// </summary>
        public ulong DonatorChannel;

        /// <summary>
        ///     ID сообщения с топом донатов.
        /// </summary>
        public ulong DonatorMessage;

        /// <summary>
        ///     Id канала с топом приглашений.
        /// </summary>
        public ulong InvitesLeaderboardChannel;

        /// <summary>
        ///     Путь до файла с предупреждениями.
        /// </summary>
        public string WarningsXML;

        /// <summary>
        ///     Путь до файла с приглашениями.
        /// </summary>
        public string InviterXML;

        /// <summary>
        ///     ID канала-лога с сообщениями о действиях модераторов.
        /// </summary>
        public ulong ModlogChannel;

        /// <summary>
        ///     ID канала-лога в который отправляются все остальные сообщения.
        /// </summary>
        public ulong FulllogChannel;

        /// <summary>
        ///     ID канала-лога в который отправляются сообщения с каналов авто-очистки.
        /// </summary>
        public ulong AutoclearLogChannel;

        /// <summary>
        ///     ID канала-лога в который отправляются сообщения о входящих и выходящих пользователях.
        /// </summary>
        public ulong UserlogChannel;

        /// <summary>
        ///     ID канала-лога в который отправляются сообщения с ошибками.
        /// </summary>
        public ulong ErrorLog;

        /// <summary>
        ///     ID канала-лога вложений
        /// </summary>
        public ulong AttachmentsLog;

        /// <summary>
        /// </summary>
        public string BanXML;

        /// <summary>
        ///     Игнорируемые каналы (в логе удаленных сообщений)
        /// </summary>
        public string IgnoredChannels;

        /// <summary>
        ///     Роли с правами администратора
        /// </summary>
        public string AdminRoles;

        /// <summary>
        ///     Этому пользователю будут отправляться уведомление об ошибках.
        /// </summary>
        public ulong Developer;

        /// <summary>
        ///     Путь до файла с блокировкой правил.
        /// </summary>
        public string ReportsXML;

        /// <summary>
        ///     Id сообщения правил.
        /// </summary>
        public ulong CodexMessageId;

        /// <summary>
        ///     Id резервного канала правил.
        /// </summary>
        public ulong CodexReserveChannel;

        /// <summary>
        ///     Id роли правил.
        /// </summary>
        public ulong CodexRole;

        /// <summary>
        ///     Id сообщения правил рейда.
        /// </summary>
        public ulong FleetCodexMessageId;

        /// <summary>
        ///     Id роли правил рейда.
        /// </summary>
        public ulong FleetCodexRole;

        /// <summary>
        ///     Id роли капитана рейда.
        /// </summary>
        public ulong FleetCaptainRole;

        /// <summary>
        ///     Id роли бана принятия правил.
        /// </summary>
        public ulong PurgeCodexRole;

        /// <summary>
        ///     Id роли мута.
        /// </summary>
        public ulong MuteRole;

        /// <summary>
        ///     Id роли голосового мута.
        /// </summary>
        public ulong VoiceMuteRole;

        /// <summary>
        /// Id сообщения эмиссарства.
        /// </summary>
        public ulong EmissaryMessageId;

        /// <summary>
        ///     Id роли эмиссарства.
        /// </summary>
        public ulong EmissaryGoldhoadersRole;

        /// <summary>
        ///     Id роли эмиссарства.
        /// </summary>
        public ulong EmissaryTradingCompanyRole;

        /// <summary>
        ///     Id роли эмиссарства.
        /// </summary>
        public ulong EmissaryOrderOfSoulsRole;

        /// <summary>
        ///     Id роли эмиссарства.
        /// </summary>
        public ulong EmissaryAthenaRole;

        /// <summary>
        ///     Id роли эмиссарства.
        /// </summary>
        public ulong EmissaryReaperBonesRole;

        /// <summary>
        ///     Id роли охотников.
        /// </summary>
        public ulong HuntersRole;

        /// <summary>
        ///     Id роли арены.
        /// </summary>
        public ulong ArenaRole;

        /// <summary>
        ///     Id канала с поиском игроков.
        /// </summary>
        public ulong FindChannel;

        /// <summary>
        ///     Id канала с созданием рейда.
        /// </summary>
        public ulong FleetCreationChannel;

        /// <summary>
        ///     Путь до файла с вышедшими пользователями.
        /// </summary>
        public string UsersLeftXML;

        /// <summary>
        ///     Путь до файла с ценами на донат.
        /// </summary>
        public string PriceListXML;

        /// <summary>
        ///     Путь до файла с голосованиями.
        /// </summary>
        public string VotesXML;

        public ulong VotesChannel;

        /// <summary>
        ///     ID канала "Найти корабль"
        /// </summary>
        public ulong FindShip;
    }

    public enum CommandType
    {
        Warn
    }
}
