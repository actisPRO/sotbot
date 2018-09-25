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
    internal sealed class Bot
    {
        public DiscordClient Client { get; set; }
        public CommandsNextModule Commands { get; set; }
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
                                                      ");
            
            ReloadSettings();
            
            ShipList.ReadFromXML(BotSettings.ShipXML);
            DonatorList.ReadFromXML(BotSettings.DonatorXML);
            UserList.ReadFromXML(BotSettings.WarningsXML);
            
            DonatorList.SaveToXML(BotSettings.DonatorXML);
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
                UseInternalLogHandler = true,
            };
            
            Client = new DiscordClient(cfg);
            
            Client.SetWebSocketClient<WebSocketSharpClient>();

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

        private Task ClientOnMessageCreated(MessageCreateEventArgs e)
        {
            var message = e.Message.Content.ToLower();
            // боремся со спамом от одной мошеннической группы
            if (message.Contains("sea") && message.Contains("of") && message.Contains("cheat") && !e.Message.Author.IsBot)
            {
                e.Guild.BanMemberAsync(e.Author.Id, 7, "Auto banned for message, containing 'seaofcheat'");
                e.Guild.GetChannel(BotSettings.ModlogChannel)
                    .SendMessageAsync($"**Блокировка**\n\n" +
                                      $"**Модератор:** {e.Client.CurrentUser.Mention} ({e.Client.CurrentUser.Id})\n" +
                                      $"**Получатель:** {e.Author.Username}#{e.Author.Discriminator} ({e.Author.Id})\n" +
                                      $"**Дата:** {DateTime.Now.ToUniversalTime()}\n" +
                                      $"**Причина:** Автоматическая блокировка за сообщение, содержащее 'sea of cheats'");
            }
            return Task.CompletedTask;
        }

        private Task ClientOnMessageDeleted(MessageDeleteEventArgs e)
        {
            if (!GetMultiplySettingsSeparated(BotSettings.IgnoredChannels).Contains(e.Channel.Id))
            {
                e.Guild.GetChannel(BotSettings.FulllogChannel)
                    .SendMessageAsync($"**Удаление сообщения**\n" +
                                      $"**Автор:** {e.Message.Author.Username}#{e.Message.Author.Discriminator} ({e.Message.Author.Id})\n" +
                                      $"**Канал:** {e.Channel}\n" +
                                      $"**Содержимое: ```{e.Message.Content}```**");
            }
            return Task.CompletedTask;
        }

        private Task ClientOnGuildMemberRemoved(GuildMemberRemoveEventArgs e)
        {
            e.Guild.GetChannel(BotSettings.UserlogChannel)
                            .SendMessageAsync($"**Участник покинул сервер:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id})");
            return Task.CompletedTask;
        }

        private async Task ClientOnGuildMemberAdded(GuildMemberAddEventArgs e)
        {
            var ctx = e; //just for copypaste sorry
            
            await ctx.Member.SendMessageAsync($"**Привет, {ctx.Member.Mention}!\n**" +
                                        $"Мы рады что ты присоединился к нашему серверу :wink:!\n\n" +
                                        $"Прежде чем приступать к игре, прочитай, пожалуйста правила в канале " +
                                        $"`👮-пиратский-кодекс-👮` и гайд по боту в канале `📚-гайд-📚`.\n" +
                                        $"Если у тебя есть какие-то вопросы, не стесняйся писать администраторам.\n\n" +
                                        $"**Удачной игры!**");
            
            await e.Guild.GetChannel(BotSettings.UserlogChannel)
                .SendMessageAsync($"**Участник присоединился:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id})");
        }

        private Task CommandsOnCommandErrored(CommandErrorEventArgs e)
        {
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Warning, "SoT", $"{e.Command.Name} errored with {e.Exception.Message}!",
                DateTime.Now);
#if true
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Warning, "SoT", $"{e.Exception.StackTrace}!",
                DateTime.Now);   
#endif
            return Task.CompletedTask;
        }

        private Task CommandsOnCommandExecuted(CommandExecutionEventArgs e)
        {
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT", $"{e.Context.User.Username}#{e.Context.User.Discriminator} ran the command " +
                                                                          $"({e.Command.Name}).", DateTime.Now.ToUniversalTime());
            
            return Task.CompletedTask;
        }

        // autocreation system
        private async Task ClientOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            try
            {
                if (e.Channel.Id == BotSettings.AutocreateChannel) // we'll create channel only if user enters autocreate channel
                {
                    var created = await e.Guild.CreateChannelAsync($"{BotSettings.AutocreateSymbol} Галеон {e.User.Username}", ChannelType.Voice,
                        e.Channel.Parent, BotSettings.Bitrate, 4);

                    var member = await e.Guild.GetMemberAsync(e.User.Id);

                    await member.PlaceInAsync(created);
                    
                    e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT", $"{e.User.Username}#{e.User.Discriminator} created channel via autocreation.", 
                        DateTime.Now.ToUniversalTime());
                }
            }
            catch (NullReferenceException) // this is called if e.Channel doesn't exist (user left some channel)
            {
                
            }
            
            // delete empty channels

            var autocreatedChannels = new List<DiscordChannel>(); // all autocreated channels
            foreach (var channel in e.Guild.Channels)
            {
                if (channel.Name.StartsWith(BotSettings.AutocreateSymbol) && channel.Type == ChannelType.Voice
                                                                          && channel.ParentId ==
                                                                          BotSettings.AutocreateCategory)
                {
                    autocreatedChannels.Add(channel);
                } 
            }

            var notEmptyChannels = new List<DiscordChannel>(); // all not empty channels
            foreach (var voiceState in e.Guild.VoiceStates)
            {
                notEmptyChannels.Add(voiceState.Channel);
            }

            var forDeletionChannels = autocreatedChannels.Except(notEmptyChannels); // we'll delete this channels
            foreach (var channel in forDeletionChannels)
            {
                await channel.DeleteAsync();
            }
        }

        private async Task ClientOnReady(ReadyEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT", $"Sea Of Thieves Bot, version {BotSettings.Version}", DateTime.Now.ToUniversalTime());
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT", "Made by Actis", DateTime.Now.ToUniversalTime());

            var member = await e.Client.Guilds[BotSettings.Guild].GetMemberAsync(e.Client.CurrentUser.Id);
            await member.ModifyAsync($"SeaOfThieves {BotSettings.Version}");
        }

        public static void ReloadSettings()
        {
            var serializer = new XmlSerializer(typeof(Settings));
            using (var fs = new FileStream("settings.xml", FileMode.Open))
            {
                var reader = XmlReader.Create(fs);
                BotSettings = (Settings) serializer.Deserialize(reader);
            }
        }

        public static List<ulong> GetMultiplySettingsSeparated(string notSeparatedStrings) //предназначено для разделения строки с настройками
                                                                                        //(например, IgnoredChannels)
        {
            string[] separatedStrings = notSeparatedStrings.Split(',');
            List<ulong> result = new List<ulong>();
            foreach (var separatedString in separatedStrings)
            {
                try
                {
                    result.Add(Convert.ToUInt64(separatedString));
                }
                catch
                {
                    continue;
                }
            }

            return result;
        }
    }

    public struct Settings
    {
        public string Version;
        public ulong Guild;
        public string Token;
        public string Prefix;

        public ulong BotRole;

        public string OkEmoji;
        public string ErrorEmoji;

        public ulong RolesChannel;

        public ulong AutocreateCategory;
        public ulong AutocreateChannel;
        public string AutocreateSymbol;
        public int Bitrate;

        public string ShipXML;
        public ulong PrivateCategory;
        public ulong PrivateRequestsChannel;

        public string DonatorXML;
        public ulong DonatorRole;

        public string WarningsXML;
        public ulong ModlogChannel;
        public ulong FulllogChannel;
        public ulong UserlogChannel;

        public string IgnoredChannels;
    }
}