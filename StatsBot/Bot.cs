using System;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using Microsoft.Extensions.Logging;

namespace StatsBot
{
    class Bot
    {
        public DiscordClient Client { get; private set; }
        public CommandsNextExtension CommandsExtension { get; private set; }
        
        public static void Main(string[] args)
        {
            var bot = new Bot();
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
                StringPrefixes = new[] { "!s " },
                EnableDms = false,
                CaseSensitive = false,
                EnableMentionPrefix = true
            };
            CommandsExtension = Client.UseCommandsNext(ccfg);
            CommandsExtension.RegisterCommands<Commands>();

            Client.Ready += (sender, args) =>
            {
                sender.Logger.LogInformation("Stats", "Bot is ready.");
                return Task.CompletedTask;
            };

            await Task.Delay(-1);
        }
    }
}