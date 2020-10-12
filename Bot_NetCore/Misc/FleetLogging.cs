using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace Bot_NetCore.Misc
{
    class FleetLogging
    {
        public static async Task LogFleetCreationAsync(DiscordGuild guild, DiscordMember member, DiscordChannel fleetCategory)
        {
            var embed = new DiscordEmbedBuilder
            {
                Title = $"{fleetCategory.Name} создан",
                Description = $"`{fleetCategory.Id}`",
                Color = DiscordColor.SpringGreen
            };

            var channels = "";
            foreach (var channel in fleetCategory.Children.OrderBy(x => x.Position))
                channels += $"**{channel.Name}** \t\t `{channel.Id}` \n";
            embed.AddField("Каналы", channels);


            embed.WithAuthor($"{member.Username}#{member.Discriminator}", iconUrl: member.AvatarUrl);
            embed.WithTimestamp(DateTime.Now);

            await guild.GetChannel(Bot.BotSettings.FleetLogChannel).SendMessageAsync(embed: embed.Build());

        }

        public static async Task LogFleetDeletionAsync(DiscordGuild guild, DiscordChannel fleetCategory)
        {
            var embed = new DiscordEmbedBuilder
            {
                Title = $"{fleetCategory.Name} удалён",
                Description = $"`{fleetCategory.Id}`",
                Color = DiscordColor.IndianRed
            };

            var channels = "";
            foreach (var channel in fleetCategory.Children.OrderBy(x => x.Position))
                channels += $"**{channel.Name}** \t\t `{channel.Id}` \n";
            embed.AddField("Каналы", channels);

            embed.WithTimestamp(DateTime.Now);

            await guild.GetChannel(Bot.BotSettings.FleetLogChannel).SendMessageAsync(embed: embed.Build());

            //Move new text channels to log
            var textChannels = fleetCategory.Children.Where(x => x.Type == ChannelType.Text);
            foreach (var channel in textChannels)
                await channel.ModifyAsync(x =>
                {
                    x.Name = $"{DateTime.Now:dd/MM} {channel.Name}";
                    x.Parent = guild.GetChannel(Bot.BotSettings.FleetLogCategory);
                });

            //Delete old fleet text channels
            var oldChannels = guild.GetChannel(Bot.BotSettings.FleetLogCategory).Children
                .Where(x =>
                {
                    try
                    {
                        return (DateTime.Now - DateTime.ParseExact(x.Name.Substring(0, 4), "ddMM", CultureInfo.InvariantCulture)).Days > 3;
                    }
                    catch (ArgumentNullException)
                    {
                        return false;
                    }
                    catch (FormatException)
                    {
                        return false;
                    }
                });

            foreach (var oldChannel in oldChannels)
                await oldChannel.DeleteAsync();
        }
    }
}