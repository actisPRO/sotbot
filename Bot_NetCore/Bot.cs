using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using SeaOfThieves.Commands;
using SeaOfThieves.Entities;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnassignedField.Global

namespace SeaOfThieves
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
        public CommandsNextModule Commands { get; set; }

        /// <summary>
        ///     Модуль взаимодействия.
        /// </summary>
        public InteractivityModule Interactivity { get; set; }

        /// <summary>
        ///     Структура с настройками бота.
        /// </summary>
        public static Settings BotSettings { get; private set; }

        public static void Main(string[] args)
        {
            var bot = new Bot();

            Console.WriteLine(@"
                        ██████╗     ██╗ ██╗
                        ╚════██╗   ███║███║
                         █████╔╝   ╚██║╚██║
                        ██╔═══╝     ██║ ██║
                        ███████╗██╗ ██║ ██║
                        ╚══════╝╚═╝ ╚═╝ ╚═╝                
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

            DonatorList.SaveToXML(BotSettings.DonatorXML); // Если вдруг формат был изменен, перезапишем XML-файлы.
            UserList.SaveToXML(BotSettings.WarningsXML);

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
                StringPrefix = BotSettings.Prefix,
                EnableDms = true,
                EnableMentionPrefix = true,
                EnableDefaultHelp = true
            };

            Commands = Client.UseCommandsNext(ccfg);

            var icfg = new InteractivityConfiguration
            {
                PaginationBehaviour = TimeoutBehaviour.Ignore,
                PaginationTimeout = TimeSpan.FromMinutes(2)
            };

            Interactivity = Client.UseInteractivity(icfg);

            Commands.RegisterCommands<CreationCommands>();
            Commands.RegisterCommands<UtilsCommands>();
            Commands.RegisterCommands<PrivateCommands>();
            Commands.RegisterCommands<DonatorCommands>();
            Commands.RegisterCommands<ModerationCommands>();
            Commands.RegisterCommands<InviteCommands>();

            Client.Ready += ClientOnReady;
            Client.GuildMemberAdded += ClientOnGuildMemberAdded;
            Client.GuildMemberRemoved += ClientOnGuildMemberRemoved;
            Client.MessageDeleted += ClientOnMessageDeleted;
            Client.VoiceStateUpdated += ClientOnVoiceStateUpdated;
            Client.MessageCreated += ClientOnMessageCreated;
            Client.MessageReactionAdded += ClientOnMessageReactionAdded;
            //Client.MessageReactionRemoved += ClientOnMessageReactionRemoved; //Не нужный ивент
            Client.UnknownEvent += ClientOnUnknownEvent;
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

            await Task.Delay(-1);
        }

        /// <summary>
        ///     Очистка из канала поиска игроков сообщений опубликованных более чем 15 минут
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ClearChannelMessagesOnElapsed(object sender, ElapsedEventArgs e)
        {
            var guild = Client.Guilds[BotSettings.Guild];

            var channels = new Dictionary<DiscordChannel, TimeSpan>
            {
                { guild.GetChannel(BotSettings.FindChannel), new TimeSpan(0, 15, 0) },            //15 минут для канала поиска
                { guild.GetChannel(BotSettings.FleetCreationChannel), new TimeSpan(24, 0, 0) }   //24 часа для канала создания рейда
            };

            foreach (var channel in channels)
            {
                var messages = await channel.Key.GetMessagesAsync(100);
                var toDelete = messages.ToList()
                    .Where(x => !x.Pinned).ToList()                                                                           //Не закрепленные сообщения
                    .Where(x => DateTimeOffset.UtcNow.Subtract(x.CreationTimestamp.Add(channel.Value)).TotalSeconds > 0);     //Опубликованные ранее определенного времени

                if (toDelete.Count() > 0)
                    try
                    {
                        await channel.Key.DeleteMessagesAsync(toDelete);
                        Client.DebugLogger.LogMessage(LogLevel.Info, "Bot", $"Канал {channel.Key.Name} был очищен.", DateTime.Now);
                    }
                    catch (Exception ex)
                    {
                        Client.DebugLogger.LogMessage(LogLevel.Info, "Bot", $"Ошибка при удалении сообщений в {channel.Key.Name}. \n{ex.Message}", DateTime.Now);
                    }
            }
        }

        private Task CommandsOnCommandExecuted(CommandExecutionEventArgs e)
        {
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Info,
                    "Bot",
                    $"Пользователь {e.Context.Member.Id}#{e.Context.Member.Discriminator} ({e.Context.Member.Id}) выполнил команду {e.Command.Name}",
                    DateTime.Now);
            return Task.CompletedTask; //Пришлось добавить, выдавало ошибку при компиляции
        }

#nullable enable //Выдавало warning
        private async void DebugLoggerOnLogMessageReceived(object? sender, DebugLogMessageEventArgs e)
        {
            if (!Directory.Exists("logs")) Directory.CreateDirectory("logs");

            var fileName = "logs/" + DateTime.Now.ToString("dd-MM-yyyy");

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
            //Check for expired bans
            var toUnban = from ban in BanList.BannedMembers.Values
                          where ban.UnbanDateTime.ToUniversalTime() <= DateTime.Now.ToUniversalTime()
                          select ban;

            var guild = await Client.GetGuildAsync(BotSettings.Guild);
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

                await guild.GetChannel(BotSettings.ModlogChannel).SendMessageAsync(
                    "**Снятие Бана**\n\n" +
                    $"**Модератор:** {Client.CurrentUser.Username}\n" +
                    $"**Пользователь:** {await Client.GetUserAsync(ban.Id)}\n" +
                    $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC\n");
            }

            BanList.SaveToXML(BotSettings.BanXML);

            Client.DebugLogger.LogMessage(LogLevel.Info, "Bot", "Бан-лист был обновлён.", DateTime.Now);

            //Check for expired mutes
            var count = ReportList.Mutes.Count;
            ReportList.Mutes.Values.Where(x => x.Expired()).ToList()
                .ForEach(async x =>
                {
                    ReportList.Mutes.Remove(x.Id);
                    try
                    {
                        await guild.RevokeRoleAsync(await guild.GetMemberAsync(x.Id), guild.GetRole(BotSettings.MuteRole), "Unmuted");
                    }
                    catch (NotFoundException)
                    {
                        //Пользователь не найден
                    }
                });
            if (count != ReportList.Mutes.Count)
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
                if (!user.Roles.Any(x => x.Id == BotSettings.CodexRole))
                    await user.GrantRoleAsync(e.Channel.Guild.GetRole(BotSettings.CodexRole));

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
                    await user.GrantRoleAsync(e.Channel.Guild.GetRole(BotSettings.FleetCodexRole));

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
                    $"{e.User.Username}#{e.User.Discriminator} acquired new emissary role.",
                    DateTime.Now.ToUniversalTime());

                e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                    $"Пользователь {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) подтвердил прочтение правил.",
                    DateTime.Now);
                return;
            }

            //then check if it is private ship confirmation message
            foreach (var ship in ShipList.Ships.Values)
            {
                if (ship.Status) continue;

                if (e.Message.Id == ship.CreationMessage)
                {
                    if (e.Emoji == DiscordEmoji.FromName((DiscordClient)e.Client, ":white_check_mark:"))
                    {
                        var name = ship.Name;
                        var role = await e.Channel.Guild.CreateRoleAsync($"☠{name}☠", null, null, false, true);
                        var channel = await e.Channel.Guild.CreateChannelAsync($"☠{name}☠", ChannelType.Voice,
                            e.Channel.Guild.GetChannel(BotSettings.PrivateCategory), BotSettings.Bitrate);

                        await channel.AddOverwriteAsync(role, Permissions.UseVoice, Permissions.None);
                        await channel.AddOverwriteAsync(e.Channel.Guild.EveryoneRole, Permissions.None,
                            Permissions.UseVoice);

                        var member =
                            await e.Channel.Guild.GetMemberAsync(ShipList.Ships[name].Members.ToArray()[0].Value.Id);

                        await member.GrantRoleAsync(role);

                        ShipList.Ships[name].SetChannel(channel.Id);
                        ShipList.Ships[name].SetRole(role.Id);
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
            if (e.Message.Content.StartsWith(">"))
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
                            RunCommand((DiscordClient) e.Client, CommandType.Warn, args, e.Message);
                            return;
                        default:
                            return;
                    }
                }
        }

        /// <summary>
        ///     Отлавливаем удаленные сообщения и отправляем в лог
        /// </summary>
        private Task ClientOnMessageDeleted(MessageDeleteEventArgs e)
        {
            if (!GetMultiplySettingsSeparated(BotSettings.IgnoredChannels).Contains(e.Channel.Id)
                ) // в лог не должны отправляться сообщения,
                // удаленные из лога
                e.Guild.GetChannel(BotSettings.FulllogChannel)
                    .SendMessageAsync("**Удаление сообщения**\n" +
                                      $"**Автор:** {e.Message.Author.Username}#{e.Message.Author.Discriminator} ({e.Message.Author.Id})\n" +
                                      $"**Канал:** {e.Channel}\n" +
                                      $"**Содержимое: ```{e.Message.Content}```**");
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Лог посещений
        /// </summary>
        private async Task ClientOnGuildMemberRemoved(GuildMemberRemoveEventArgs e)
        {
            // Сохранение ролей участника
            var roles = e.Member.Roles;
            var rolesToSave = new List<ulong>();
            foreach (var role in roles)
            {
                if (role.Id != BotSettings.CodexRole && role.Id != e.Guild.EveryoneRole.Id)
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
                    $"**Участник покинул сервер:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id})");

            //Если пользователь не был никем приглашен, то при выходе он будет сохранен.
            if (!InviterList.Inviters.ToList().Any(i => i.Value.Referrals.ContainsKey(e.Member.Id)))
                InviterList.Inviters[0].AddReferral(e.Member.Id, false);

            //При выходе обновляем реферала на неактив.
            InviterList.Inviters.ToList().Where(i => i.Value.Referrals.ContainsKey(e.Member.Id)).ToList()
                                         .ForEach(i => i.Value.UpdateReferral(e.Member.Id, false));

            InviterList.SaveToXML(BotSettings.InviterXML);

            await InviteCommands.UpdateLeaderboard(e.Guild);

            e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                $"Участник {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}) покинул сервер.",
                DateTime.Now);
        }

        /// <summary>
        ///     Приветственное сообщение + лог посещений + проверка на бан
        /// </summary>
        private async Task ClientOnGuildMemberAdded(GuildMemberAddEventArgs e)
        {
            var invites = Invites.AsReadOnly().ToList(); //Сохраняем список старых инвайтов в локальную переменную
            var guildInvites = await e.Guild.GetInvitesAsync(); //Запрашиваем новый список инвайтов
            Invites = guildInvites.ToList(); //Обновляю список инвайтов

            await e.Member.SendMessageAsync($"**Привет, {e.Member.Mention}!\n**" +
                                            "Мы рады что ты присоединился к нашему серверу :wink:!\n\n" +
                                            "Прежде чем приступать к игре, прочитай, пожалуйста, правила в канале " +
                                            "`👮-пиратский-кодекс-👮` и гайд по боту в канале `📚-гайд-📚`.\n" +
                                            "Если у тебя есть какие-то вопросы, не стесняйся писать администраторам.\n\n" +
                                            "**Удачной игры!**");

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

                UsersLeftList.Users[e.Member.Id] = null;
                UsersLeftList.SaveToXML(BotSettings.UsersLeftXML);
            }
            
            try
            {
                //Находит обновившийся инвайт по количеству приглашений
                //Вызывает NullReferenceException в случае если ссылка только для одного использования
                var updatedInvite = guildInvites.ToList().Find(g => invites.Find(i => i.Code == g.Code).Uses < g.Uses);

                //Если не удалось определить инвайт, значит его нет в новых так как к.во использований ограничено и он был удален
                if(updatedInvite == null)
                {
                    updatedInvite = invites.Where(p => guildInvites.All(p2 => p2.Code != p.Code))                       //Ищем удаленный инвайт
                                           .Where(x => (x.CreatedAt.AddSeconds(x.MaxAge) > DateTimeOffset.UtcNow))      //Проверяем если он не истёк
                                           .FirstOrDefault();                                                           //С такими условиями будет только один такой инвайт
                }

                await e.Guild.GetChannel(BotSettings.UserlogChannel)
                    .SendMessageAsync(
                        $"**Участник присоединился:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}) используя " +
                        $"приглашение {updatedInvite.Code} от участника {updatedInvite.Inviter.Username}#{updatedInvite.Inviter.Discriminator}");

                e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                    $"Участник {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}) присоединился к серверу. Приглашение: {updatedInvite.Code} от участника {updatedInvite.Inviter.Username}#{updatedInvite.Inviter.Discriminator}",
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
                await InviteCommands.UpdateLeaderboard(e.Guild);
            }
            catch (Exception ex)
            {
                await e.Guild.GetChannel(BotSettings.UserlogChannel)
                    .SendMessageAsync(
                        $"**Участник присоединился:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}). При попытке отследить инвайт произошла ошибка.");

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
                              $"**Трассировка стека:** \n```{ex.StackTrace}```";

                await errChannel.SendMessageAsync(message);
            }
        }

        /// <summary>
        ///     Отправляем в консоль сообщения об ошибках при выполнении команды.
        /// </summary>
        private async Task CommandsOnCommandErrored(CommandErrorEventArgs e)
        {
            if (e.Command.Name == "dgenlist" && e.Exception.GetType() == typeof(NotFoundException)) return; //костыль

            if (e.Exception.GetType() == typeof(ArgumentException) &&
                e.Exception.Message.Contains("Could not convert specified value to given type."))
            {
                await e.Context.RespondAsync(
                    $"{BotSettings.ErrorEmoji} Не удалось выполнить команду. Проверьте правильность введенных параметров.");
                return;
            }

            if (e.Exception.GetType() == typeof(ArgumentException) &&
                e.Exception.Message == "Not enough arguments supplied to the command.")
            {
                await e.Context.RespondAsync(
                    $"{BotSettings.ErrorEmoji} Не удалось выполнить команду: вы ввели не все параметры.");
                return;
            }

            e.Context.Client.DebugLogger.LogMessage(LogLevel.Warning, "SoT",
                $"Участник {e.Context.Member.Username}#{e.Context.Member.Discriminator} " +
                $"({e.Context.Member.Id}) пытался запустить команду {e.Command.Name}, но произошла ошибка.",
                DateTime.Now);

            await e.Context.RespondAsync(
                $"{BotSettings.ErrorEmoji} Возникла ошибка при выполнении команды **{e.Command.Name}**! Попробуйте ещё раз, если " +
                "ошибка повторяется - проверьте канал `#📚-гайд-по-боту📚`. " +
                $"**Информация об ошибке:** {e.Exception.Message}");

            var errChannel = e.Context.Guild.GetChannel(BotSettings.ErrorLog);

            var message = $"**Команда:** {e.Command.Name}\n" +
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
                            e.Guild.GetChannel(BotSettings.AutocreateCategory), BotSettings.Bitrate, 2);
                    else if (e.Channel.Id == BotSettings.AutocreateBrigantine) // Бригантина
                        created = await e.Guild.CreateChannelAsync(
                            $"{channelSymbol} Бриг {e.User.Username}", ChannelType.Voice,
                            e.Guild.GetChannel(BotSettings.AutocreateCategory), BotSettings.Bitrate, 3);
                    else // Галеон
                        created = await e.Guild.CreateChannelAsync(
                            $"{channelSymbol} Галеон {e.User.Username}", ChannelType.Voice,
                            e.Guild.GetChannel(BotSettings.AutocreateCategory), BotSettings.Bitrate, 4);

                    var member = await e.Guild.GetMemberAsync(e.User.Id);

                    await member.PlaceInAsync(created);

                    e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                        $"Участник {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) создал канал через автосоздание.",
                        DateTime.Now.ToUniversalTime());
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
                foreach (var channel in e.Guild.Channels)
                    if (channel.Type == ChannelType.Voice
                        && channel.ParentId == BotSettings.AutocreateCategory)
                        autocreatedChannels.Add(channel);

                var notEmptyChannels = new List<DiscordChannel>(); // это все НЕ пустые каналы
                foreach (var voiceState in e.Guild.VoiceStates) notEmptyChannels.Add(voiceState.Channel);

                var forDeletionChannels = autocreatedChannels.Except(notEmptyChannels); // это пустые каналы
                foreach (var channel in forDeletionChannels) await channel.DeleteAsync(); // мы их удаляем
            }
            catch (NotFoundException) // Если пользователь пересоздает канал перейдя с уже автосозданного канала
            {
                // пропускаем
            }

        }

        /// <summary>
        ///     Проверка на создание/удаление инвайтов
        /// </summary>
        private async Task ClientOnUnknownEvent(UnknownEventArgs e)
        {
            if (e.EventName == "INVITE_CREATE" || e.EventName == "INVITE_DELETE")
            {
                var guildInvites = await e.Client.Guilds[BotSettings.Guild].GetInvitesAsync();
                Invites = guildInvites.ToList();
            }
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

            var member = await e.Client.Guilds[BotSettings.Guild].GetMemberAsync(e.Client.CurrentUser.Id);
            await member.ModifyAsync($"SeaOfThieves {BotSettings.Version}");

            var guildInvites = await e.Client.Guilds[BotSettings.Guild].GetInvitesAsync();
            Invites = guildInvites.ToList();
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
                BotSettings = (Settings) serializer.Deserialize(reader);
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
        ///     ID канала-лога в который отправляются сообщения о входящих и выходящих пользователях.
        /// </summary>
        public ulong UserlogChannel;

        public ulong ErrorLog;

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
        ///     Id роли мута.
        /// </summary>
        public ulong MuteRole;

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
    }

    public enum CommandType
    {
        Warn
    }
}
