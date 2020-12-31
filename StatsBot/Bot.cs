using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace StatsBot
{
    class Bot
    {
        public DiscordClient Client { get; private set; }
        public CommandsNextExtension CommandsExtension { get; private set; }

        public static string ConnectionString = "";
        
        public static Dictionary<ulong, ExtendedData> Data = new Dictionary<ulong, ExtendedData>();
        
        public static void Main(string[] args)
        {
            var bot = new Bot();
            ConnectionString = GetConnectionString();
            bot.RunBotAsync().GetAwaiter().GetResult();
        }

        public async Task RunBotAsync()
        {
            var token = "";
            using (var sr = new StreamReader("token.txt"))
                token = await sr.ReadToEndAsync();
            
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
                EnableMentionPrefix = true
            };
            CommandsExtension = Client.UseCommandsNext(ccfg);
            CommandsExtension.RegisterCommands<Commands>();

            Client.Ready += (sender, args) =>
            {
                using (TextFieldParser parser = new TextFieldParser("global_stats_full.csv"))
                {
                    parser.TextFieldType = FieldType.Delimited;
                    parser.SetDelimiters(",");
                    while (!parser.EndOfData)
                    {
                        string[] fields = parser.ReadFields();
                        var time = fields[4].Split(':');
                        var timeSpan = new TimeSpan(Convert.ToInt32(time[0]),
                            Convert.ToInt32(time[1]),
                            Convert.ToInt32(time[2]));
                        
                        var piece = new ExtendedData()
                        {
                            Id = Convert.ToUInt64(fields[0]),
                            Username = fields[1],
                            ReactionsReceived = Convert.ToInt32(fields[2]),
                            Messages = Convert.ToInt32(fields[3]),
                            VoiceTime = timeSpan,
                            Warnings = Convert.ToInt32(fields[5])
                        };

                        Data[piece.Id] = piece;
                    }
                }
                sender.Logger.LogInformation("Bot is ready");

                return Task.CompletedTask;
            };

            CommandsExtension.CommandExecuted += (sender, args) =>
            {
                Client.Logger.LogInformation(args.Context.Member + " выполнил команду " + args.Command.Name);
                return Task.CompletedTask;
            };

            CommandsExtension.CommandErrored += (sender, args) =>
            {
                try
                {
                    sender.Client.Logger.LogError($"Command {args.Command.Name} errored: {args.Exception.Message}\n{args.Exception.StackTrace}\n");
                }
                catch (Exception)
                {
                    
                }
                return Task.CompletedTask;
            };

            await Client.ConnectAsync();
            await Task.Delay(-1);
        }

        public static string GetConnectionString()
        {
            using (var sr = new StreamReader("mysql.txt"))
            {
                string ip = sr.ReadLine();
                string db = sr.ReadLine();
                string user = sr.ReadLine();
                string password = sr.ReadLine();
                
                return
                    $"Server={ip}; Port=3306; Database={db}; Uid={user}; Pwd={password}; CharSet=utf8mb4;";
            }
        }
    }
}