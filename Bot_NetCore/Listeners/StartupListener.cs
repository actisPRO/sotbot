using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Misc;
using Bot_NetCore.Providers;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;

namespace Bot_NetCore.Listeners
{
    public static class StartupListener
    {
        [AsyncListener(EventTypes.Ready)]
        public static async Task ClientOnReady(DiscordClient client, ReadyEventArgs e)
        {
            client.Logger.LogInformation(BotLoggerEvents.Bot,
                $"Sea Of Thieves Bot, version {Bot.BotSettings.Version}");
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
            catch
            {
            }

            foreach (var entry in e.Guild.VoiceStates
                         .Where(x => x.Value.Channel != null && x.Value.Channel.Id != e.Guild.AfkChannel.Id).ToList())
            {
                if (!VoiceListener.VoiceTimeCounters.ContainsKey(entry.Key))
                    VoiceListener.VoiceTimeCounters.Add(entry.Key, DateTime.Now);
            }

            await RegisterEmojiRoleProvidersAsync(client, e.Guild);
        }

        private static async Task RegisterEmojiRoleProvidersAsync(DiscordClient client, DiscordGuild guild)
        {
            client.Logger.LogInformation("Started EmojiRoleProvider registration");
            await RegisterEmissaryRoleProviderAsync(client, guild);
            client.Logger.LogInformation("Finished EmojiRoleProvider registration");
        }

        private static async Task RegisterEmissaryRoleProviderAsync(DiscordClient client, DiscordGuild guild)
        {
            var emissaryProvider = new EmojiRoleProvider
            {
                Channel = guild.GetChannel(696668143430533190),
                MessageId = Bot.BotSettings.EmissaryMessageId,
                Removable = true,
                Roles = new Dictionary<DiscordEmoji, DiscordRole>()
                {
                    {
                        DiscordEmoji.FromName(client, ":moneybag:"),
                        guild.GetRole(Bot.BotSettings.EmissaryGoldhoadersRole)
                    },
                    {
                        DiscordEmoji.FromName(client, ":pig:"),
                        guild.GetRole(Bot.BotSettings.EmissaryTradingCompanyRole)
                    },
                    {
                        DiscordEmoji.FromName(client, ":skull:"),
                        guild.GetRole(Bot.BotSettings.EmissaryOrderOfSoulsRole)
                    },
                    { 
                        DiscordEmoji.FromName(client, ":gem:"), 
                        guild.GetRole(Bot.BotSettings.EmissaryAthenaRole) 
                    },
                    {
                        DiscordEmoji.FromName(client, ":skull_crossbones:"),
                        guild.GetRole(Bot.BotSettings.EmissaryReaperBonesRole)
                    },
                    {
                        DiscordEmoji.FromName(client, ":fish:"), 
                        guild.GetRole(Bot.BotSettings.HuntersRole)
                    }
                }
            };
            await emissaryProvider.ValidateEmojisAsync(client);
            GlobalState.EmojiRoleProviders.Add(emissaryProvider);
        }
    }
}