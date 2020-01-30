using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
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
           ▄████████  ▄████████     ███      ▄█     ▄████████ 
          ███    ███ ███    ███ ▀█████████▄ ███    ███    ███ 
          ███    ███ ███    █▀     ▀███▀▀██ ███▌   ███    █▀  
          ███    ███ ███            ███   ▀ ███▌   ███        
         ▀███████████ ███            ███     ███▌ ▀███████████ 
          ███    ███ ███    █▄      ███     ███           ███ 
          ███    ███ ███    ███     ███     ███     ▄█    ███ 
          ███    █▀  ████████▀     ▄████▀   █▀    ▄████████▀  
                                                      "); //ЧСВ

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

            Client.SetWebSocketClient<WebSocketSharpClient>(); // Для использования с Mono.

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

            Commands.CommandExecuted += CommandsOnCommandExecuted;
            Commands.CommandErrored += CommandsOnCommandErrored;

            await Client.ConnectAsync();

            await Task.Delay(-1);
        }

        /// <summary>
        ///     Обработка подозрительных сообщений.
        /// </summary>
        private Task ClientOnMessageCreated(MessageCreateEventArgs e)
        {
            var message = e.Message.Content.ToLower();
            // боремся со спамом от одной мошеннической группы
            if (message.Contains("sea") && message.Contains("of") && message.Contains("cheat") &&
                !e.Message.Author.IsBot)
            {
                e.Guild.BanMemberAsync(e.Author.Id, 7, "Autobanned for message, containing 'seaofcheat'");
                e.Guild.GetChannel(BotSettings.ModlogChannel)
                    .SendMessageAsync("**Блокировка**\n\n" +
                                      $"**Модератор:** {e.Client.CurrentUser.Mention} ({e.Client.CurrentUser.Id})\n" +
                                      $"**Получатель:** {e.Author.Username}#{e.Author.Discriminator} ({e.Author.Id})\n" +
                                      $"**Дата:** {DateTime.Now.ToUniversalTime()}\n" +
                                      "**Причина:** Автоматическая блокировка за сообщение, содержащее 'sea of cheats'");
            }

            return Task.CompletedTask;
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
                    await e.Member.SendMessageAsync($"Ваша блокировка истекает {bannedUser.UnbanDateTime}. Причина блокировки: " +
                                                    $"{bannedUser.Reason}");
                    await e.Member.RemoveAsync("Banned user tried to join");
                }
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
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Warning, "SoT", $"{e.Command.Name} errored.",
                DateTime.Now.ToUniversalTime());

            await e.Context.RespondAsync(
                $"{BotSettings.ErrorEmoji} Возникла ошибка при выполнении команды **{e.Command.Name}**! Попробуйте ещё раз; если " +
                $"ошибка повторяется - проверьте канал `#📚-гайд-по-боту📚`. " +
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
                if (e.Channel.Id == BotSettings.AutocreateChannel
                ) // мы создаем канал, если пользователь зашел в канал автосоздания
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

                    var created = await e.Guild.CreateChannelAsync(
                        $"{BotSettings.AutocreateSymbol} Галеон {e.User.Username}", ChannelType.Voice,
                        e.Channel.Parent, BotSettings.Bitrate, 4);

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
        ///     ID канала автосоздания.
        /// </summary>
        public ulong AutocreateChannel;

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
        /// 
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
    }
}