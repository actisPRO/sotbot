using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using SeaOfThieves.Entities;

namespace SeaOfThieves.Commands
{
    public class UtilsCommands
    {
        public bool keepRainbow;

        [Command("printroles")]
        [Description("Выводит список ролей на сервере")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task PrintRoles(CommandContext ctx)
        {
            foreach (var role in ctx.Guild.Roles)
                await ctx.Guild.GetChannel(Bot.BotSettings.RolesChannel)
                    .SendMessageAsync($"• **{role.Name}** `{role.Id}`");
        }

        [Command("roleid")]
        [RequirePermissions(Permissions.ManageRoles)]
        [Hidden]
        public async Task RoleId(CommandContext ctx, DiscordRole role)
        {
            await ctx.RespondAsync(Convert.ToString(role.Id));
        }

        [Command("whois")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task WhoIs(CommandContext ctx, ulong id)
        {
            try
            {
                var member = await ctx.Guild.GetMemberAsync(id);
                await ctx.RespondAsync(member.Mention);
            }
            catch (NotFoundException)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не найден.");
            }
        }

        [Command("generateDonatorMessage")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task GenerateDonatorMessage(CommandContext ctx, ulong channelId)
        {
            try
            {
                var message = await ctx.Guild.GetChannel(channelId).SendMessageAsync("**Топ донатов**");
                var doc = XDocument.Load("settings.xml");
                doc.Element("Settings").Element("DonatorMessage").Value = Convert.ToString(message.Id);
                Bot.ReloadSettings();
            }
            catch (Exception e)
            {
                await ctx.RespondAsync("**ERRORED**");
            }
        }

        [Command("updateDonatorMessageLegacy")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task UpdateDonatorMessageLegacy(CommandContext ctx)
        {
            var donators = new Dictionary<ulong, double>(); //список донатеров, который будем сортировать
            foreach (var donator in DonatorList.Donators.Values)
                if (!donator.Hidden)
                    donators.Add(donator.Member, donator.Balance);

            var ordered = donators.OrderBy(x => -x.Value);
            var message = "**Топ донатов**\n\n```ruby\n";

            var i = 0;
            var prevValue = double.MaxValue;
            foreach (var el in ordered)
            {
                if (el.Value < prevValue)
                {
                    prevValue = el.Value;
                    i++;
                }

                var mention = "";
                try
                {
                    var donatorMemberEntity = await ctx.Guild.GetMemberAsync(el.Key);

                    mention = donatorMemberEntity.Username + "#" + donatorMemberEntity.Discriminator;
                }
                catch (NotFoundException) //пользователь мог покинуть сервер 
                {
                    mention = "Участник покинул сервер";
                }

                message += $"{i}. {mention} — {el.Value}₽\n";

                if (message.Length >= 1950)
                {
                    
                }
            }

            message += "```";
            //TODO: settings.xml
            var messageEntity = await ctx.Guild.GetChannel(459657130786422784)
                .GetMessageAsync(Bot.BotSettings.DonatorMessage);
            await messageEntity.ModifyAsync(message);
            //Console.WriteLine("Message length: " + message.Length);
        }

        [Command("dgenlist")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task GenerateDonatorMessage(CommandContext ctx)
        {
            var channel = ctx.Guild.GetChannel(Bot.BotSettings.DonatorChannel);

            var fso = File.Open("donators_messages.txt", FileMode.OpenOrCreate);
            var sr = new StreamReader(fso);

            var messageId = sr.ReadLine();
            while (messageId != null)
            {
                try
                {
                    await channel.DeleteMessageAsync(await channel.GetMessageAsync(Convert.ToUInt64(messageId)));
                }
                catch (NotFoundException) { }

                messageId = sr.ReadLine();
            }
            
            sr.Close();
            fso.Close();
            
            var donators = new Dictionary<ulong, double>(); //список донатеров, который будем сортировать
            foreach (var donator in DonatorList.Donators.Values)
                if (!donator.Hidden)
                    donators.Add(donator.Member, donator.Balance);

            var ordered = donators.OrderBy(x => -x.Value);

            int messageCount = ordered.Count() / 10;
            if (ordered.Count() % 10 != 0) ++messageCount;

            int position = 0, balance = Int32.MaxValue, str = 1;
            string message = "";

            var fs = File.Create("donators_messages.txt");
            var sw = new StreamWriter(fs);
            
            foreach (var el in ordered)
            {
                if (str % 10 == 0)
                {
                    var sendedMessage = await channel.SendMessageAsync(message);
                    sw.WriteLine(sendedMessage.Id);
                    message = "";
                }

                if ((int) Math.Floor(el.Value) < balance)
                {
                    ++position;
                    balance = (int) Math.Floor(el.Value);
                }

                var user = await ctx.Client.GetUserAsync(el.Key);

                message += $"**{position}.** {user.Username}#{user.Discriminator} - *{el.Value}₽*\n";
                ++str;
            }

            if (str % 10 != 0)
            {
                var sendedMessage = await channel.SendMessageAsync(message);
                sw.WriteLine(sendedMessage.Id);
            }
            
            sw.Close();
            fs.Close();
        }

        [Command("codexgen")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task CodexGenerateMessage(CommandContext ctx)
        {
            var channel = ctx.Guild.GetChannel(Bot.BotSettings.CodexChannel);
            var message = $"**Я прочитал правила и обязуюсь их выполнять.**";
            var messageEnt = await channel.SendMessageAsync(message);
            
            using (var fs = File.Create("codex_message"))
                using (var sw = new StreamWriter(fs))
                    sw.WriteLine(messageEnt.Id);

            await messageEnt.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji}");
        }

        [Command("throw")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Throw(CommandContext ctx)
        {
            throw new IOException("Test exception.");
        }

        [Command("rainbow")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Rainbow(CommandContext ctx)
        {
            keepRainbow = true;
            var role = ctx.Guild.GetRole(586522215046971393);
            while (keepRainbow)
            {
                await ctx.Guild.UpdateRoleAsync(role, color: DiscordColor.Red);
                Thread.Sleep(1000);
                await ctx.Guild.UpdateRoleAsync(role, color: DiscordColor.Orange);
                Thread.Sleep(1000);
                await ctx.Guild.UpdateRoleAsync(role, color: DiscordColor.Yellow);
                Thread.Sleep(1000);
                await ctx.Guild.UpdateRoleAsync(role, color: DiscordColor.Green);
                Thread.Sleep(1000);
                await ctx.Guild.UpdateRoleAsync(role, color: DiscordColor.Blue);
                Thread.Sleep(1000);
                await ctx.Guild.UpdateRoleAsync(role, color: DiscordColor.Cyan);
                Thread.Sleep(1000);
                await ctx.Guild.UpdateRoleAsync(role, color: DiscordColor.Purple);
                Thread.Sleep(1000);
            }
        }

        [Command("stoprainbow")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task StopRainbow(CommandContext ctx)
        {
            keepRainbow = false;
        }

        [Command("time")]
        public async Task Time(CommandContext ctx)
        {
            await ctx.RespondAsync($"Текущее время на сервере: **{DateTime.Now}**.");
        }
    }
}