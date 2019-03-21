using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
        [Command("printroles")]
        [Description("Выводит список ролей на сервере")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task PrintRoles(CommandContext ctx)
        {
            foreach (var role in ctx.Guild.Roles)
            {
                await ctx.Guild.GetChannel(Bot.BotSettings.RolesChannel)
                    .SendMessageAsync($"• **{role.Name}** `{role.Id}`");
            }
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

        [Command("updateDonatorMessage")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task UpdateDonatorMessage(CommandContext ctx)
        {
            Dictionary<ulong, double> donators = new Dictionary<ulong, double>(); //список донатеров, который будем сортировать
            foreach (var donator in DonatorList.Donators.Values)
            {
                if (!donator.Hidden)
                {
                    donators.Add(donator.Member, donator.Balance);
                }
            }

            var ordered = donators.OrderBy(x => -x.Value);
            var message = "**Топ донатов**\n\n";

            int i = 0;
            var prevValue = Double.MaxValue;
            foreach (var el in ordered)
            {
                if (el.Value < prevValue)
                {
                    prevValue = el.Value;
                    i++;
                }

                string mention = "";
                try
                {
                    var donatorMemberEntity = await ctx.Guild.GetMemberAsync(el.Key);
                    mention = donatorMemberEntity.Mention;
                }
                catch (NotFoundException) //пользователь мог покинуть сервер 
                {
                    mention = "*Участник покинул сервер*";
                }
                message += $"**{i}.** {mention} — {el.Value}₽\n";
            }
            
            //TODO: settings.xml
            var messageEntity = await ctx.Guild.GetChannel(459657130786422784).GetMessageAsync(Bot.BotSettings.DonatorMessage);
            await messageEntity.ModifyAsync(message);
        }
    }
}