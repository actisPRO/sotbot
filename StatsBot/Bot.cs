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
                sender.Logger.LogInformation("Bot is ready");
                return Task.CompletedTask;
            };

            CommandsExtension.CommandErrored += (sender, args) =>
            {
                sender.Client.Logger.LogError($"Command {args.Command.Name} errored:\n{args.Exception.StackTrace}\n");
                return Task.CompletedTask;
            };

            await Client.ConnectAsync();
            await Task.Delay(-1);
        }
    }
}