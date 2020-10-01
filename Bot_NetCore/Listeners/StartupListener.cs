using System;
using System.Threading.Tasks;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.EventArgs;

namespace Bot_NetCore.Listeners
{
    public static class StartupListener
    {
        [AsyncListener(EventTypes.Ready)]
        public static async Task ClientOnReady(ReadyEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT", $"Sea Of Thieves Bot, version {Bot.BotSettings.Version}",
                DateTime.Now);
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT", "Made by Actis",
                DateTime.Now); // и еще немного ЧСВ

            var guild = e.Client.Guilds[Bot.BotSettings.Guild];

            var member = await guild.GetMemberAsync(e.Client.CurrentUser.Id);
            await member.ModifyAsync(x => x.Nickname = $"SeaOfThieves {Bot.BotSettings.Version}");
        }

        [AsyncListener(EventTypes.GuildAvailable)]
        public static async Task ClientOnGuildAvailable(GuildCreateEventArgs e)
        {
            await Bot.UpdateBotStatusAsync(e.Client, e.Guild);
        }
    }
}
