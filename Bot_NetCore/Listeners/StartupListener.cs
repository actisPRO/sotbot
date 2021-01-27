using System;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;

namespace Bot_NetCore.Listeners
{
    public static class StartupListener
    {
        [AsyncListener(EventTypes.Ready)]
        public static async Task ClientOnReady(DiscordClient client, ReadyEventArgs e)
        {
            client.Logger.LogInformation(BotLoggerEvents.Bot, "SoT", $"Sea Of Thieves Bot, version {Bot.BotSettings.Version}");
            client.Logger.LogInformation(BotLoggerEvents.Bot, "Made by Actis and VanguardFx"); // и еще немного ЧСВ

            var guild = client.Guilds[Bot.BotSettings.Guild];

            var member = await guild.GetMemberAsync(client.CurrentUser.Id);
            await member.ModifyAsync(x => x.Nickname = $"SeaOfThieves {Bot.BotSettings.Version}");

            Bot.ShipNamesStats = FastShipStats.LoadFromFile("generated/stats/ship_names.csv");
        }

        [AsyncListener(EventTypes.GuildAvailable)]
        public static async Task ClientOnGuildAvailable(DiscordClient client, GuildCreateEventArgs e)
        {
            await Bot.UpdateBotStatusAsync(client, e.Guild);

            try
            {
                VoiceListener.ReadFindChannelMesages();
            }
            catch { }

            foreach (var entry in e.Guild.VoiceStates.Where(x => x.Value.Channel != null && x.Value.Channel.Id != e.Guild.AfkChannel.Id).ToList())
            {
                if (!VoiceListener.VoiceTimeCounters.ContainsKey(entry.Key))
                    VoiceListener.VoiceTimeCounters.Add(entry.Key, DateTime.Now);
            }

        }
    }
}
