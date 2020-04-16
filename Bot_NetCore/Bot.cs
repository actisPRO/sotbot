using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using DSharpPlus.Net.WebSocket;
using SeaOfThieves.Commands;
using SeaOfThieves.Entities;

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
        ///     DiscordClient бота.
        /// </summary>
        public DiscordClient Client { get; set; }

        /// <summary>
        ///     Модуль команд.
        /// </summary>
        public CommandsNextModule Commands { get; set; }

        /// <summary>
        ///     Структура с настройками бота.
        /// </summary>
        public static Settings BotSettings { get; private set; }

        public static void Main(string[] args)
        {
            var bot = new Bot();

            Console.WriteLine(@"
                      ██████╗     █████╗     ██████╗ 
                      ╚════██╗   ██╔══██╗   ██╔═████╗
                       █████╔╝   ╚██████║   ██║██╔██║
                      ██╔═══╝     ╚═══██║   ████╔╝██║
                      ███████╗██╗ █████╔╝██╗╚██████╔╝
                      ╚══════╝╚═╝ ╚════╝ ╚═╝ ╚═════╝ 
            "); //Font Name: ANSI Shadow

            ReloadSettings(); // Загрузим настройки

            ShipList.ReadFromXML(BotSettings.ShipXML);
            DonatorList.ReadFromXML(BotSettings.DonatorXML);
            UserList.ReadFromXML(BotSettings.WarningsXML);
            BanList.ReadFromXML(BotSettings.BanXML);

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

            Commands.RegisterCommands<CreationCommands>();
            Commands.RegisterCommands<UtilsCommands>();
            Commands.RegisterCommands<PrivateCommands>();
            Commands.RegisterCommands<DonatorCommands>();
            Commands.RegisterCommands<ModerationCommands>();

            Client.Ready += ClientOnReady;
            Client.GuildMemberAdded += ClientOnGuildMemberAdded;
            Client.GuildMemberRemoved += ClientOnGuildMemberRemoved;
            Client.MessageDeleted += ClientOnMessageDeleted;
            Client.VoiceStateUpdated += ClientOnVoiceStateUpdated;
            Client.MessageCreated += ClientOnMessageCreated;
            Client.MessageReactionAdded += ClientOnMessageReactionAdded;
            Client.MessageReactionRemoved += ClientOnMessageReactionRemoved;

            Commands.CommandExecuted += CommandsOnCommandExecuted;
            Commands.CommandErrored += CommandsOnCommandErrored;

            await Client.ConnectAsync();

            await Task.Delay(-1);
        }

        private async Task ClientOnMessageReactionRemoved(MessageReactionRemoveEventArgs e)
        {
            var doc = XDocument.Load("users.xml");
            var root = doc.Root;

            foreach (var user in root.Elements())
                if (Convert.ToUInt64(user.Element("Id").Value) == e.User.Id)
                    if (user.Element("Status").Value == "False")
                        return;

            ulong messageId = 0;
            try
            {
                using (var fs = File.OpenRead("codex_message"))
                using (var sr = new StreamReader(fs))
                {
                    messageId = Convert.ToUInt64(sr.ReadLine());
                }
            }
            catch (FileNotFoundException)
            {
                return;
            }

            if (e.Message.Id == messageId)
                await e.Channel.Guild.RevokeRoleAsync(await e.Channel.Guild.GetMemberAsync(e.User.Id),
                    e.Channel.Guild.GetRole(BotSettings.CodexRole), "");

            foreach (var user in root.Elements())
                if (Convert.ToUInt64(user.Element("Id").Value) == e.User.Id)
                {
                    user.Remove();
                    break;
                }

            doc.Save("users.xml");
        }

        private async Task ClientOnMessageReactionAdded(MessageReactionAddEventArgs e)
        {
            if (e.User.IsBot) return;

            //first check if message is codex confirmation
            ulong messageId = 0;
            try
            {
                using (var fs = File.OpenRead("codex_message"))
                using (var sr = new StreamReader(fs))
                {
                    messageId = Convert.ToUInt64(sr.ReadLine());
                }
            }
            catch (FileNotFoundException)
            {
                return;
            }

            if (e.Message.Id == messageId)
            {
                var doc = XDocument.Load("users.xml");
                var root = doc.Root;

                foreach (var user in root.Elements())
                    if (Convert.ToUInt64(user.Element("Id").Value) == e.User.Id)
                        if (user.Element("Status").Value == "False")
                            return;

                await e.Channel.Guild.GrantRoleAsync(await e.Channel.Guild.GetMemberAsync(e.User.Id),
                    e.Channel.Guild.GetRole(BotSettings.CodexRole));

                var newEl = new XElement("Users", new XElement("Id", e.User.Id), new XElement("Date", DateTime.Now),
                    new XElement("Status", true));
                root.Add(newEl);

                doc.Save("users.xml");

                return;
            }

            //then check if it is private ship confirmation message
            foreach (var ship in ShipList.Ships.Values)
            {
                if (ship.Status) continue;

                if (e.Message.Id == ship.CreationMessage)
                {
                    if (e.Emoji == DiscordEmoji.FromName((DiscordClient) e.Client, ":white_check_mark:"))
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
                    }
                    else if (e.Emoji == DiscordEmoji.FromName((DiscordClient) e.Client, ":no_entry:"))
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
        private Task ClientOnGuildMemberRemoved(GuildMemberRemoveEventArgs e)
        {
            e.Guild.GetChannel(BotSettings.UserlogChannel)
                .SendMessageAsync(
                    $"**Участник покинул сервер:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id})");
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Приветственное сообщение + лог посещений + проверка на бан
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private async Task ClientOnGuildMemberAdded(GuildMemberAddEventArgs e)
        {
            if (BanList.BannedMembers.ContainsKey(e.Member.Id))
            {
                var date = DateTime.Now.ToUniversalTime();
                var bannedUser = BanList.BannedMembers[e.Member.Id];
                if (date < bannedUser.UnbanDateTime)
                {
                    await e.Member.SendMessageAsync(
                        $"Ваша блокировка истекает {bannedUser.UnbanDateTime}. Причина блокировки: " +
                        $"{bannedUser.Reason}");
                    await e.Member.RemoveAsync("Banned user tried to join");
                    return;
                }

                bannedUser.Unban();
                BanList.SaveToXML(BotSettings.BanXML);
            }

            var ctx = e; // здесь я копипастил, а рефакторить мне лень.

            await ctx.Member.SendMessageAsync($"**Привет, {ctx.Member.Mention}!\n**" +
                                              "Мы рады что ты присоединился к нашему серверу :wink:!\n\n" +
                                              "Прежде чем приступать к игре, прочитай, пожалуйста, правила в канале " +
                                              "`👮-пиратский-кодекс-👮` и гайд по боту в канале `📚-гайд-📚`.\n" +
                                              "Если у тебя есть какие-то вопросы, не стесняйся писать администраторам.\n\n" +
                                              "**Удачной игры!**");

            await e.Guild.GetChannel(BotSettings.UserlogChannel)
                .SendMessageAsync(
                    $"**Участник присоединился:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id})");
        }

        /// <summary>
        ///     Отправляем в консоль сообщения об ошибках при выполнении команды.
        /// </summary>
        private async Task CommandsOnCommandErrored(CommandErrorEventArgs e)
        {
            if (e.Command.Name == "dgenlist" && e.Exception.GetType() == typeof(NotFoundException)) return; //костыль
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Warning, "SoT", $"{e.Command.Name} errored.",
                DateTime.Now.ToUniversalTime());

            await e.Context.RespondAsync(
                $"{BotSettings.ErrorEmoji} Возникла ошибка при выполнении команды **{e.Command.Name}**! Попробуйте ещё раз; если " +
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

        private Task CommandsOnCommandExecuted(CommandExecutionEventArgs e)
        {
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT",
                $"{e.Context.User.Username}#{e.Context.User.Discriminator} ran the command " +
                $"({e.Command.Name}).", DateTime.Now.ToUniversalTime());

            return Task.CompletedTask;
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
                            return;
                        }

                    // если проверка успешно пройдена, добавим пользователя
                    // в словарь кулдаунов
                    ShipCooldowns[e.User] = DateTime.Now.AddSeconds(BotSettings.FastCooldown);

                    DiscordChannel created = null;
                    // Проверяем канал в котором находится пользователь
                    if (e.Channel.Id == BotSettings.AutocreateSloop) //Шлюп
                        created = await e.Guild.CreateChannelAsync(
                            $"{BotSettings.AutocreateSymbol} Шлюп {e.User.Username}", ChannelType.Voice,
                            e.Guild.GetChannel(BotSettings.AutocreateCategory), BotSettings.Bitrate, 2);
                    else if (e.Channel.Id == BotSettings.AutocreateBrigantine) // Бригантина
                        created = await e.Guild.CreateChannelAsync(
                            $"{BotSettings.AutocreateSymbol} Бриг {e.User.Username}", ChannelType.Voice,
                            e.Guild.GetChannel(BotSettings.AutocreateCategory), BotSettings.Bitrate, 3);
                    else // Галеон
                        created = await e.Guild.CreateChannelAsync(
                            $"{BotSettings.AutocreateSymbol} Галеон {e.User.Username}", ChannelType.Voice,
                            e.Guild.GetChannel(BotSettings.AutocreateCategory), BotSettings.Bitrate, 4);

                    var member = await e.Guild.GetMemberAsync(e.User.Id);

                    await member.PlaceInAsync(created);

                    e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT",
                        $"{e.User.Username}#{e.User.Discriminator} created channel via autocreation.",
                        DateTime.Now.ToUniversalTime());
                }
            }
            catch (NullReferenceException) // исключение выбрасывается если пользователь покинул канал
            {
                // нам здесь ничего не надо делать, просто пропускаем
            }

            // удалим пустые каналы

            var autocreatedChannels = new List<DiscordChannel>(); // это все автосозданные каналы
            foreach (var channel in e.Guild.Channels)
                if (channel.Name.StartsWith(BotSettings.AutocreateSymbol) && channel.Type == ChannelType.Voice
                                                                          && channel.ParentId ==
                                                                          BotSettings.AutocreateCategory)
                    autocreatedChannels.Add(channel);

            var notEmptyChannels = new List<DiscordChannel>(); // это все НЕ пустые каналы
            foreach (var voiceState in e.Guild.VoiceStates) notEmptyChannels.Add(voiceState.Channel);

            var forDeletionChannels = autocreatedChannels.Except(notEmptyChannels); // это пустые каналы
            foreach (var channel in forDeletionChannels) await channel.DeleteAsync(); // мы их удаляем
        }

        /// <summary>
        ///     Сообщение в лог о готовности клиента
        /// </summary>
        private async Task ClientOnReady(ReadyEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT", $"Sea Of Thieves Bot, version {BotSettings.Version}",
                DateTime.Now.ToUniversalTime());
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT", "Made by Actis",
                DateTime.Now.ToUniversalTime()); // и еще немного ЧСВ

            var member = await e.Client.Guilds[BotSettings.Guild].GetMemberAsync(e.Client.CurrentUser.Id);
            await member.ModifyAsync($"SeaOfThieves {BotSettings.Version}");
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
                                for (var i = 2; i < args.Length - 1; ++i)
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
        ///     Путь до файла с предупреждениями.
        /// </summary>
        public string WarningsXML;

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

        public ulong CodexChannel;

        public ulong CodexRole;
    }

    public enum CommandType
    {
        Warn
    }
}