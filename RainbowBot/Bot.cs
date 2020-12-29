using System;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;

namespace RainbowBot
{
    class Bot
    {
        public DiscordClient Client { get; private set; }
        public CommandsNextExtension CommandsExtension { get; private set; }

        public static Settings BotSettings;

        public static void Main(string[] args)
        {
            var bot = new Bot();
            bot.RunBotAsync().GetAwaiter().GetResult();
        }

        public async Task RunBotAsync()
        {
            BotSettings = LoadSettings();
            var token = BotSettings.Token;
            
            var cfg = new DiscordConfiguration()
            {
                Token = token,
                AutoReconnect = true,
                TokenType = TokenType.Bot
            };
            Client = new DiscordClient(cfg);

            var ccfg = new CommandsNextConfiguration()
            {
                StringPrefixes = new[] { "!" },
                EnableDms = false,
                CaseSensitive = false,
                EnableMentionPrefix = true,
                EnableDefaultHelp = false
            };
            CommandsExtension = Client.UseCommandsNext(ccfg);
            CommandsExtension.RegisterCommands<Commands>();

            Client.Ready += (sender, args) =>
            {
                sender.Logger.LogInformation("Bot is ready");
                return Task.CompletedTask;
            };

            CommandsExtension.CommandErrored += (sender, args) =>
            {
                if (args.Exception is CommandNotFoundException) return Task.CompletedTask;

                sender.Client.Logger.LogError($"Command {args.Command.Name} errored:\n{args.Exception.StackTrace}\n");
                return Task.CompletedTask;
            };
            
            var updateRainbow = new Timer((int) Math.Round(BotSettings.Cooldown * 1000));
            updateRainbow.Elapsed += UpdateRainbowOnElapsed;
            updateRainbow.AutoReset = true;
            updateRainbow.Enabled = true;

            await Client.ConnectAsync();
            await Task.Delay(-1);
        }

        public int RainbowColor = 0;
        private static DiscordColor[] Colors = new DiscordColor[]
        {
            DiscordColor.Red,
            DiscordColor.Orange,
            DiscordColor.Yellow,
            DiscordColor.Green,
            DiscordColor.Cyan,
            DiscordColor.HotPink
        };
        private async void UpdateRainbowOnElapsed(object sender, ElapsedEventArgs e)
        {
            if (BotSettings.IsEnabled)
            {
                try
                {
                    var role = Client.Guilds[Bot.BotSettings.Guild].GetRole(Bot.BotSettings.RoleId);
                    if (RainbowColor >= Colors.Length) RainbowColor = 0;
                    await role.ModifyAsync(color: Colors[RainbowColor]);
                    ++RainbowColor;
                }
                catch (NullReferenceException)
                {
                    
                }
            }
        }

        public Settings LoadSettings()
        {
            var serializer = new XmlSerializer(typeof(Settings));
            using (var fs = new FileStream("settings.xml", FileMode.Open))
            {
                var reader = XmlReader.Create(fs);
                return (Settings) serializer.Deserialize(reader);
            }
        }
        
        public Settings EditSettings(string param, string value)
        {
            try
            {
                if (param == "Token") throw new Exception("Невозможно изменить данный параметр.");
                var doc = XDocument.Load("settings.xml", LoadOptions.PreserveWhitespace);
                var elem = doc.Element("Settings").Element(param);

                elem.Value = value;
                doc.Save("settings.xml");
                
                return LoadSettings();
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
    }
    public struct Settings
    {
        public string Token;
        public ulong Guild;
        public bool IsPublic;
        public bool IsEnabled;
        public ulong RoleId;
        public float Cooldown;
    }
}