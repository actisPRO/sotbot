using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;

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

        public static async Task LogFleetDeletionAsync(DiscordClient client, DiscordGuild guild, DiscordChannel fleetCategory)
        {
            client.Logger.LogInformation(BotLoggerEvents.Event, $"Удаление рейда {fleetCategory.Name} - {fleetCategory.Id}");

            var embed = new DiscordEmbedBuilder
            {
                Title = $"{fleetCategory.Name} удалён",
                Description = $"`{fleetCategory.Id}`",
                Color = DiscordColor.IndianRed
            };

            var channels = "";
            foreach (var channel in fleetCategory.Children.OrderBy(x => x.Position).OrderBy(x => x.Type))
                channels += $"**{channel.Name}** \t\t `{channel.Id}` \n";
            embed.AddField("Каналы", channels);

            embed.WithTimestamp(DateTime.Now);

            await guild.GetChannel(Bot.BotSettings.FleetLogChannel).SendMessageAsync(embed: embed.Build());

            var fleetLogCategory = guild.GetChannel(Bot.BotSettings.FleetLogCategory);

            //Move new text channels to log
            var textChannels = fleetCategory.Children.Where(x => x.Type == ChannelType.Text);
            foreach (var channel in textChannels)
            {
                var lastPosition = fleetLogCategory.Children.OrderBy(x => x.Position).LastOrDefault().Position;

                await channel.ModifyAsync(x =>
                {
                    x.Name = $"{DateTime.Now:dd} {channel.Name}";
                    x.Parent = guild.GetChannel(Bot.BotSettings.FleetLogCategory);
                    x.Topic = DateTime.Now.ToString();
                });

                await channel.ModifyPositionAsync(lastPosition + 1);

                new Task(async () =>
                   {
                       channel.PermissionOverwrites.ToList().ForEach(async x =>
                       {
                           await x.DeleteAsync();
                           await Task.Delay(400);
                       });

                       //Sync with category permissions
                       foreach (var permission in guild.GetChannel(Bot.BotSettings.FleetLogCategory).PermissionOverwrites)
                       {
                           if (permission.Type == OverwriteType.Role)
                               await channel.AddOverwriteAsync(await permission.GetRoleAsync(), permission.Allowed, permission.Denied);
                           else
                               await channel.AddOverwriteAsync(await permission.GetMemberAsync(), permission.Allowed, permission.Denied);

                           await Task.Delay(400);
                       }
                   }
                ).Start();
            }

            //Delete old fleet text channels
            var oldChannels = guild.GetChannel(Bot.BotSettings.FleetLogCategory).Children
                .Where(x =>
                {
                    try
                    {
                        return (DateTime.Now - DateTime.Parse(x.Topic)).TotalHours > 36;
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
