using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;

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
            {
                await channel.ModifyAsync(x =>
                {
                    x.Name = $"{DateTime.Now:dd/MM} {channel.Name}";
                    x.Parent = guild.GetChannel(Bot.BotSettings.FleetLogCategory);
                });

                //Delete old permissions
                while (channel.PermissionOverwrites.Any())
                    await channel.PermissionOverwrites.First().DeleteAsync();

                //Sync with category permissions
                foreach (var permission in guild.GetChannel(Bot.BotSettings.FleetLogCategory).PermissionOverwrites)
                {
                    if(permission.Type == OverwriteType.Role)
                        await channel.AddOverwriteAsync(await permission.GetRoleAsync(), permission.Allowed, permission.Denied);
                    else
                        await channel.AddOverwriteAsync(await permission.GetMemberAsync(), permission.Allowed, permission.Denied);
                }
            }

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