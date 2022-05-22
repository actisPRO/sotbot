﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Misc;
using DataTablePrettyPrinter;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Commands
{
    [RequireGuild]
    public class UtilsCommands : BaseCommandModule
    {
        public bool keepRainbow;

        [Command("config")]
        [Description("Изменяет конфиг бота")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task Config(CommandContext ctx, [Description("Параметр")] string param, [Description("Значение")] string value)
        {
            try
            {
                Bot.EditSettings(param, value);

                await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно изменен параметр `{param}: {value}`");
            }
            catch (NullReferenceException ex)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось изменить параметр `{param}: {value}` | `{ex.Message}`");
            }
        }

        [Command("printroles")]
        [Description("Выводит список ролей на сервере")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task PrintRoles(CommandContext ctx)
        {
            foreach (var role in ctx.Guild.Roles.Values)
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
        
        //[Command("resetfleet")]
        //public async Task ResetFleetChannels(CommandContext ctx) //Команда для сброса названий и слотов каналов рейда после "рейдеров"
        //{
        //    if (!Bot.IsModerator(ctx.Member) || ctx.Member.Roles.Contains(ctx.Guild.GetRole(Bot.BotSettings.FleetCaptainRole))) //Проверка на права модератора или роль капитана.
        //    {
        //        await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
        //        return;
        //    }

        //    //Сбрасываем позицию канала Chill, если вдруг изменена (Позиция 0)
        //    if (ctx.Guild.GetChannel(Bot.BotSettings.FleetChillChannel).Position != 0)
        //        await ctx.Guild.GetChannel(Bot.BotSettings.FleetChillChannel).ModifyPositionAsync(0);

        //    //Обновляем общий канал и его позицию, если изменена (Позиция 1)
        //    var fleetLobby = ctx.Guild.GetChannel(Bot.BotSettings.FleetLobby);
        //    if (ctx.Guild.GetChannel(Bot.BotSettings.FleetLobby).Position != 1)
        //        await fleetLobby.ModifyAsync(x =>
        //        {
        //            x.Name = "Общий";
        //            x.Position = 1;
        //            x.Userlimit = 99;
        //        });
        //    else
        //        await fleetLobby.ModifyAsync(x =>
        //        {
        //            x.Name = "Общий";
        //            x.Userlimit = 99;
        //        });

        //    //Выбираем остальные каналы и сортуруем по ID.
        //    var channels = ctx.Guild.GetChannel(Bot.BotSettings.FleetCategory).Children
        //        .Where(x => x.Type == ChannelType.Voice &&
        //                    x.Id != Bot.BotSettings.FleetChillChannel &&
        //                    x.Id != Bot.BotSettings.FleetLobby)
        //        .OrderBy(x => x.Id);

        //    //Сбрасываем остальные каналы.
        //    int i = 0;
        //    int fleetNum = 0;
        //    foreach (var fleetChannel in channels)
        //    {
        //        if (i % 5 == 0)
        //            fleetNum++;

        //        //Обновляем канал и позицию в списке, если изменена (Позиция i + 2)
        //        if (fleetChannel.Position != i + 2)
        //            await fleetChannel.ModifyAsync(x =>
        //            {
        //                x.Name = $"Рейд#{(i % 5) + 1} - №{fleetNum}";
        //                x.Position = i + 2;
        //                x.Userlimit = Bot.BotSettings.FleetUserLimiter;
        //            });
        //        else
        //            await fleetChannel.ModifyAsync(x =>
        //            {
        //                x.Name = $"Рейд#{(i % 5) + 1} - №{fleetNum}";
        //                x.Userlimit = Bot.BotSettings.FleetUserLimiter;
        //            });
        //        i++;
        //    }
        //    await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно сброшены каналы рейда!");
        //}

        [Command("codexmessage")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task CodexMessage(CommandContext ctx, DiscordMessage message)
        {
            if (ctx.Channel.LastMessageId.HasValue)
                await ctx.Channel.DeleteMessageAsync(await ctx.Channel.GetMessageAsync(ctx.Channel.LastMessageId.Value));

            //Обновляем настроки бота
            if (Bot.BotSettings.CodexMessageId != message.Id)
                Bot.EditSettings("CodexMessageId", message.Id.ToString());

            //Убираем все реакции с сообщения
            await message.DeleteAllReactionsAsync();

            //Добавляем реакции к сообщению
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
        }

        [Command("throw")]
        [RequirePermissions(Permissions.Administrator)]
        public Task Throw(CommandContext ctx)
        {
            throw new IOException("Test exception.");
        }

        /*[Command("rainbow")]
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
        public Task StopRainbow(CommandContext ctx)
        {
            keepRainbow = false;
            return Task.CompletedTask;
        }*/

        [Command("time")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task Time(CommandContext ctx)
        {
            await ctx.RespondAsync($"Текущее время на сервере: **{DateTime.Now}**.");
        }

        [Command("showsettings")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task ShowSettings(CommandContext ctx)
        {
            var interactivity = ctx.Client.GetInteractivity();

            List<string> settings = new List<string>();

            foreach (var field in typeof(Settings).GetFields())
            {
                if (field.Name == "Token" ||
                    field.Name == "DatabaseHost" ||
                    field.Name == "DatabaseName" ||
                    field.Name == "DatabaseUser" ||
                    field.Name == "DatabasePassword") continue;
                settings.Add($"**{field.Name}** = {field.GetValue(Bot.BotSettings)}");
            }

            var settingsPagination = Utility.GeneratePagesInEmbeds(settings, "**Текущие настройки бота**");

            if (settingsPagination.Count() > 1)
                //await interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, settingsPagination, timeoutoverride: TimeSpan.FromMinutes(5));
                await interactivity.SendPaginatedMessageAsync(
                    channel: await ctx.Member.CreateDmChannelAsync(),
                    user: ctx.User,
                    pages: settingsPagination,
                    behaviour: PaginationBehaviour.Ignore,
                    deletion: ButtonPaginationBehavior.DeleteButtons,
                    token: default);
            else
                await ctx.RespondAsync(embed: settingsPagination.First().Embed);
        }

        [Command("emissarymessage")]
        [Description("Обновляет привязку к сообщению эмиссаров (вводится в канале с сообщением)")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task UpdateEmissaryMessage(CommandContext ctx, DiscordMessage message)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            if (ctx.Channel.LastMessageId.HasValue)
                await ctx.Channel.DeleteMessageAsync(await ctx.Channel.GetMessageAsync(ctx.Channel.LastMessageId.Value));

            //Обновляем настроки бота
            if (Bot.BotSettings.EmissaryMessageId != message.Id)
                Bot.EditSettings("EmissaryMessageId", message.Id.ToString());

            //Убираем все реакции с сообщения
            await message.DeleteAllReactionsAsync();

            //Добавляем реакции к сообщению
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":moneybag:"));
            await Task.Delay(400);
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":pig:"));
            await Task.Delay(400);
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":skeleton:"));
            await Task.Delay(400);
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":gem:"));
            await Task.Delay(400);
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":skull_and_crossbones:"));
            await Task.Delay(400);
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":fish:"));
            await Task.Delay(400);
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":crossed_swords:"));
            await Task.Delay(400);
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":x:"));

        }

        [Command("serverstatus")]
        [Description("Обновляет статус игровых серверов")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task ServerStatus(CommandContext ctx, string status = "")
        {
            var name = status.ToLower() switch
            {
                "on" => "🟢сервера-работают🟢",
                "off" => "🔴сервера-отключены🔴",
                "issues" => "🟠замечены-проблемы🟠",
                "investigating" => "🟡rare-знают-о-проблеме🟡",
                _ => "error"
            };

            if (name == "error")
            {
                await ctx.RespondAsync("**Доступные статусы**\n\n" +
                                       "`!serverstatus on` — `🟢сервера-работают🟢`\n" +
                                       "`!serverstatus off` — `🔴сервера-отключены🔴`\n" +
                                       "`!serverstatus issues` — `🟠замечены-проблемы🟠`\n" +
                                       "`!serverstatus investigating` — `🟡rare-знают-о-проблеме🟡`\n");
                return;
            }

            await ctx.Guild.GetChannel(Bot.BotSettings.ServerStatusChannel).ModifyAsync(x => x.Name = name);
        }

        [Command("copyrole")]
        [Description("Копирует указанную роль и настраивает права в каналах для новой роли")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task CopyRole(CommandContext ctx, DiscordRole oldRole)
        {
            await ctx.RespondAsync("Копирование роли запущено, это может занять некоторое время.");

            var newRole = await ctx.Guild.CreateRoleAsync(oldRole.Name + "_CPY", oldRole.Permissions, oldRole.Color, oldRole.IsHoisted, oldRole.IsMentionable);

            newRole = ctx.Guild.GetRole(newRole.Id);
            await newRole.ModifyPositionAsync(oldRole.Position);

            var resultString = "";
            await ctx.TriggerTypingAsync();

            foreach(var channel in await ctx.Guild.GetChannelsAsync())
                foreach(var permission in channel.PermissionOverwrites)
                    if(permission.Type == OverwriteType.Role)
                    {
                        var permRole = await permission.GetRoleAsync();
                        if (permRole.Id == oldRole.Id)
                        {
                            await channel.AddOverwriteAsync(newRole, permission.Allowed, permission.Denied);
                            resultString += $"```{channel} \nAllowed: {permission.Allowed.ToPermissionString()} \nDenied: {permission.Denied.ToPermissionString()}```";
                        }
                    }

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно создана копия роли. Роль: {newRole.Mention} \n {resultString}");
        }

        [Command("createfleetpoll")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task CreateFleetPoll(CommandContext ctx)
        {
            await ctx.Message.DeleteAsync();

            var fleetPollResetTime = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 12, 0, 0);

            var embed = new DiscordEmbedBuilder
            {
                Color = new DiscordColor(0x58FF9B),
                Title = "Голосование за рейд",
                Description = $"Вы можете проголосовать за тип рейда который пройдёт **{fleetPollResetTime.AddDays(2):dd/MM}**.\n\n" +
                    "Таким образом капитанам рейда будет легче узнать какой тип рейда больше всего востребован.\n‎"

            }
            .WithThumbnail("https://cdn.discordapp.com/attachments/772989975301324890/772990308052107284/RAID.gif")
            .WithFooter($"Обновление голосования")
            .WithTimestamp(fleetPollResetTime.AddDays(1))
            .AddField(":one: Эмиссарский", ":black_circle: **0**", true)
            .AddField(":two: FOTD", ":black_circle: **0**", true)
            .AddField(":three: Меги", ":black_circle: **0**", true);

            var msg = await ctx.RespondAsync(embed: embed.Build());

            var emojis = new DiscordEmoji[]
            {
                DiscordEmoji.FromName(ctx.Client, ":one:"),
                DiscordEmoji.FromName(ctx.Client, ":two:"),
                DiscordEmoji.FromName(ctx.Client, ":three:")
            };

            foreach (var emoji in emojis)
            {
                await msg.CreateReactionAsync(emoji);
                await Task.Delay(400);
            }

            Bot.EditSettings("FleetVotingMessage", msg.Id.ToString());
        }

        [Command("sudo")]
        [Description("Выполняет команду за пользователя.")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Sudo(CommandContext ctx, [Description("Пользователь")] DiscordMember member, [RemainingText, Description("Команда для выполнения")] string command)
        {
            //Взято с примеров команд 

            await ctx.TriggerTypingAsync();

            // get the command service, we need this for sudo purposes
            var cmds = ctx.CommandsNext;

            // retrieve the command and its arguments from the given string
            var cmd = cmds.FindCommand(command, out var customArgs);

            // create a fake CommandContext
            var fakeContext = cmds.CreateFakeContext(member, ctx.Channel, command, ctx.Prefix, cmd, customArgs);

            // and perform the sudo
            await cmds.ExecuteCommandAsync(fakeContext);
        }

        [Command("sql")]
        [Description("Выполняет SQL-запрос. НЕБЕЗОПАСНО!")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Sql(CommandContext ctx, [Description("SQL-запрос"), RemainingText] string sqlRequest)
        {
            await ctx.TriggerTypingAsync();
            
            await using var connection = new MySqlConnection(Bot.ConnectionString);
            await using var cmd = new MySqlCommand();
            cmd.CommandText = sqlRequest;
            cmd.Connection = connection;
            
            await cmd.Connection.OpenAsync();
            var reader = await cmd.ExecuteReaderAsync();

            var table = new DataTable();
            table.Load(reader);

            string message;
            if (table.Rows.Count != 0)
                message = $"**Результат выполнения SQL-запроса:**\n```{table.ToPrettyPrintedString()}```";
            else
                message = $"{Bot.BotSettings.OkEmoji} SQL-запрос ничего не вернул.";

            await ctx.RespondAsync(message);
        }
    }
}
