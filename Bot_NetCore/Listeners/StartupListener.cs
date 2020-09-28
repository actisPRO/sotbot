using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Bot_NetCore.Commands;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using Microsoft.VisualBasic.FileIO;

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
            await Bot.UpdateMembersCountAsync(e.Client, e.Guild.MemberCount);
        }
    }
}
