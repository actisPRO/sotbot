using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Bot_NetCore.Commands;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.Logging;

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
        
        /// <summary>
        ///     Соединение с БД
        /// </summary>
        public static string ConnectionString { get; private set; }

        public static void Main(string[] args)
        {
            var bot = new Bot();

            Console.WriteLine(@"   
                ██████╗    ██╗███████╗
                ╚════██╗  ███║╚════██║
                 █████╔╝  ╚██║    ██╔╝
                ██╔═══╝    ██║   ██╔╝ 
                ███████╗██╗██║   ██║  
                ╚══════╝╚═╝╚═╝   ╚═╝  
            "); //Font Name: ANSI Shadow

            ReloadSettings(); // Загрузим настройки

            ShipList.ReadFromXML(BotSettings.ShipXML);
            InviterList.ReadFromXML(BotSettings.InviterXML);
            UsersLeftList.ReadFromXML(BotSettings.UsersLeftXML);
            PriceList.ReadFromXML(BotSettings.PriceListXML);
            Vote.Read(BotSettings.VotesXML);
            Note.Read(BotSettings.NotesXML);
            Donator.Read(BotSettings.DonatorXML);
            Subscriber.Read(BotSettings.SubscriberXML);
            
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");            
            
            bot.RunBotAsync().GetAwaiter().GetResult();
        }

        public async Task RunBotAsync()
        {
            var cfg = new DiscordConfiguration
            {
                Token = BotSettings.Token,
                AutoReconnect = true,
                TokenType = TokenType.Bot
            };

            Client = new DiscordClient(cfg);

            var ccfg = new CommandsNextConfiguration
            {
                StringPrefixes = new[] { BotSettings.Prefix },
                EnableDms = true,
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

            //Команды
            Commands.RegisterCommands(Assembly.GetExecutingAssembly());

            //Кастомнуа справка команд
            Commands.SetHelpFormatter<HelpFormatter>();

            //Ивенты
            AsyncListenerHandler.InstallListeners(Client, this);

            ConnectionString =
                $"Server={Bot.BotSettings.DatabaseHost}; Port=3306; Database={Bot.BotSettings.DatabaseName}; Uid={Bot.BotSettings.DatabaseUser}; Pwd={Bot.BotSettings.DatabasePassword}; CharSet=utf8mb4;";

            await Client.ConnectAsync();

            if (!Directory.Exists("generated")) Directory.CreateDirectory("generated");
            if (!File.Exists("generated/attachments_messages.csv")) File.Create("generated/attachments_messages.csv");
            if (!File.Exists("generated/find_channel_invites.csv")) File.Create("generated/find_channel_invites.csv");
            if (!File.Exists("generated/top_inviters.xml")) File.Create("generated/top_inviters.xml");
            
            await Task.Delay(-1);
        }

        /// <summary>
        ///     Обновляет статус бота, отображая к.во участников на сервере.
        /// </summary>
        /// <param name="client">Клиент бота</param>
        /// <param name="number">Число пользователей на сервере</param>
        /// <returns></returns>
        public static async Task UpdateBotStatusAsync(DiscordClient client, DiscordGuild guild)
        {
            await client.UpdateStatusAsync(new DiscordActivity("личные сообщения.\n" +
                $"Участников: {guild.MemberCount} \n" +
                $"Активных: {guild.VoiceStates.Values.Where(x => x.Channel != null).Count()}", ActivityType.Watching));
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
                if (param == "Token") throw new Exception("Невозможно изменить данный параметр.");
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

        public static async void RunCommand(DiscordClient client, CommandType type, string[] args,
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
        ///     ID категории поддержки.
        /// </summary>
        public ulong SupportCategory;

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
        ///     ID категории-логов рейдов.
        /// </summary>
        public ulong FleetLogCategory;


        /// <summary>
        ///     ID канала-лога перемещений пользователей в каналах.
        /// </summary>
        public ulong FleetLogChannel;


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
        ///     ID роли модераторов.
        /// </summary>
        public ulong ModeratorsRole;

        /// <summary>
        ///     Этому пользователю будут отправляться уведомление об ошибках.
        /// </summary>
        public ulong Developer;

        /// <summary>
        ///     ID роли хелпреа.
        /// </summary>
        public ulong HelperRole;

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
        ///     ID канала капитанов.
        /// </summary>
        public ulong FleetCaptainsChannel;

        /// <summary>
        ///     ID сообщения с короткими правилами рейда.
        /// </summary>
        public ulong FleetShortCodexMessage;

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

        /// <summary>
        ///     ID канала с активными или недавно закончившимися голосованиями
        /// </summary>
        public ulong VotesChannel;

        /// <summary>
        ///     ID канала "Найти корабль"
        /// </summary>
        public ulong FindShip;

        /// <summary>
        ///     Путь до файла с заметками о пользователях.
        /// </summary>
        public string NotesXML;

        /// <summary>
        ///     ID канала с архивом голосований
        /// </summary>
        public ulong VotesArchive;

        /// <summary>
        ///     ID категории ролей-цветов
        /// </summary>
        public ulong ColorSpacerRole;

        /// <summary>
        ///     Путь до файла с подписчиками
        /// </summary>
        public string SubscriberXML;

        /// <summary>
        ///     ID роли топового реферрала.
        /// </summary>
        public ulong TopMonthRole;

        /// <summary>
        ///     ID канал со статусом игровых серверов
        /// </summary>
        public ulong ServerStatusChannel;

        /// <summary>
        ///     URL веб-интерфейса (/)
        /// </summary>
        public string WebURL;

        /// <summary>
        ///     Адрес MySQL-сервера
        /// </summary>
        public string DatabaseHost;

        /// <summary>
        ///     Имя базы данных
        /// </summary>
        public string DatabaseName;

        /// <summary>
        ///     Имя пользователя БД
        /// </summary>
        public string DatabaseUser;

        /// <summary>
        ///     Пароль пользователя БДы
        /// </summary>
        public string DatabasePassword;

        /// <summary>
        ///     URL изображения для поиска игроков.
        /// </summary>
        public string ThumbnailFull;

        /// <summary>
        ///     URL изображения для поиска игроков.
        /// </summary>
        public string ThumbnailOne;

        /// <summary>
        ///     URL изображения для поиска игроков.
        /// </summary>
        public string ThumbnailTwo;

        /// <summary>
        ///     URL изображения для поиска игроков.
        /// </summary>
        public string ThumbnailThree;

        /// <summary>
        ///     URL изображения для поиска игроков.
        /// </summary>
        public string ThumbnailNA;

        /// <summary>
        ///     URL изображения для поиска игроков.
        /// </summary>
        public string ThumbnailRaid;

        /// <summary>
        ///     Включен-ли Секретный Санта?
        /// </summary>
        public bool SecretSantaEnabled;

        /// <summary>
        ///     Дата, до которой доступна регистрация на Секретного Санту
        /// </summary>
        public DateTime SecretSantaLastJoinDate;

        /// <summary>
        ///     Дата, до который участник должен присоединиться к серверу, чтобы участвовать в Секретном Санте.
        /// </summary>
        public DateTime LastPossibleJoinDate;

        /// <summary>
        ///     Роль участника Секретного Санты
        /// </summary>
        public ulong SecretSantaRole;

        /// <summary>
        ///     Количество дней которое пользователеь должен провести на сервере до того как принять правила.
        /// </summary>
        public int FleetDateOffset;

        /// <summary>
        ///     Количество часов которое пользователеь должен провести в голосовых каналах до того как принять правила.
        /// </summary>
        public int FleetVoiceTimeOffset;
    }

    public enum CommandType
    {
        Warn
    }
}
