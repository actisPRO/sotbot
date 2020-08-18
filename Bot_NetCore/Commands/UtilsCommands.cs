using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;

namespace Bot_NetCore.Commands
{
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

        [Command("whois")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task WhoIs(CommandContext ctx, DiscordMember member)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            try
            {
                var embed = new DiscordEmbedBuilder();
                embed.WithAuthor($"{member.Username}#{member.Discriminator}", iconUrl: member.AvatarUrl);
                embed.AddField("ID", member.Id.ToString(), true);
                embed.WithColor(DiscordColor.Blurple);

                embed.AddField("Имя на сервере", member.Username);

                //Предупреждения
                var warnings = 0;
                if (UserList.Users.ContainsKey(member.Id)) warnings = UserList.Users[member.Id].Warns.Count;
                embed.AddField("Предупреждения", warnings.ToString(), true);

                //Донат
                var donate = 0;
                if (DonatorList.Donators.ContainsKey(member.Id)) donate = (int)DonatorList.Donators[member.Id].Balance;
                embed.AddField("Донат", donate.ToString(), true);

                //Модератор
                var moderator = "Нет";
                if (Bot.IsModerator(member)) moderator = "Да";
                embed.AddField("Модератор", moderator, true);

                //Приватный корабль
                var privateShip = "Нет";
                foreach (var ship in ShipList.Ships.Values)
                    foreach (var shipMember in ship.Members.Values)
                        if (shipMember.Type == MemberType.Owner && shipMember.Id == member.Id)
                        {
                            privateShip = ship.Name;
                            break;
                            ;
                        }

                embed.AddField("Владелец приватного корабля", privateShip);

                //Правила
                var codex = "Не принял";
                if (member.Roles.Any(x => x.Id == Bot.BotSettings.CodexRole))
                    codex = "Принял";
                else if (ReportList.CodexPurges.ContainsKey(member.Id))
                    if (ReportList.CodexPurges[member.Id].Expired())
                        codex = "Не принял*";
                    else
                        codex = Utility.FormatTimespan(ReportList.CodexPurges[member.Id].getRemainingTime());
                embed.AddField("Правила", codex, true);

                //Правила рейда
                var fleetCodex = "Не принял";
                if (member.Roles.Any(x => x.Id == Bot.BotSettings.FleetCodexRole))
                    fleetCodex = "Принял";
                else if (ReportList.FleetPurges.ContainsKey(member.Id))
                    if (ReportList.FleetPurges[member.Id].Expired())
                        fleetCodex = "Не принял*";
                    else
                        fleetCodex = Utility.FormatTimespan(ReportList.FleetPurges[member.Id].getRemainingTime());
                embed.AddField("Правила рейда", fleetCodex, true);

                //Мут
                var mute = "Нет";
                if (ReportList.Mutes.ContainsKey(member.Id))
                    if (!ReportList.Mutes[member.Id].Expired())
                        mute = Utility.FormatTimespan(ReportList.Mutes[member.Id].getRemainingTime());
                embed.AddField("Мут", mute, true);

                //Заметка
                var note = "Отсутствует";
                if (Note.Notes.ContainsKey(member.Id))
                    note = Note.Notes[member.Id].Content;
                embed.AddField("Заметка", note, false);

                embed.WithFooter("(*) Не принял после разблокировки");

                await ctx.RespondAsync(embed: embed.Build());
            }
            catch (NotFoundException)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не найден.");
            }
        }

        [Command("whois")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task WhoIs(CommandContext ctx, DiscordUser user)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            try
            {
                var embed = new DiscordEmbedBuilder();
                embed.WithAuthor($"{user.Username}#{user.Discriminator}", iconUrl: user.AvatarUrl);
                embed.WithColor(DiscordColor.Blurple);

                embed.AddField("ID", user.Id.ToString(), true);
                embed.AddField("Статус", "Покинул сервер", true);

                embed.AddField("Имя на сервере", user.Username);

                //Предупреждения
                var warnings = 0;
                if (UserList.Users.ContainsKey(user.Id)) warnings = UserList.Users[user.Id].Warns.Count;
                embed.AddField("Предупреждения", warnings.ToString(), true);

                //Донат
                var donate = 0;
                if (DonatorList.Donators.ContainsKey(user.Id)) donate = (int)DonatorList.Donators[user.Id].Balance;
                embed.AddField("Донат", donate.ToString(), true);

                //Приватный корабль
                var privateShip = "Нет";
                foreach (var ship in ShipList.Ships.Values)
                    foreach (var shipMember in ship.Members.Values)
                        if (shipMember.Type == MemberType.Owner && shipMember.Id == user.Id)
                        {
                            privateShip = ship.Name;
                            break;
                            ;
                        }

                embed.AddField("Владелец приватного корабля", privateShip);

                //Правила
                var codex = "Нет данных";
                if (ReportList.CodexPurges.ContainsKey(user.Id))
                    if (ReportList.CodexPurges[user.Id].Expired())
                        codex = "Не принял*";
                    else
                        codex = Utility.FormatTimespan(ReportList.CodexPurges[user.Id].getRemainingTime());
                embed.AddField("Правила", codex, true);

                //Правила рейда
                var fleetCodex = "Нет данных";
                if (ReportList.FleetPurges.ContainsKey(user.Id))
                    if (ReportList.FleetPurges[user.Id].Expired())
                        fleetCodex = "Не принял*";
                    else
                        fleetCodex = Utility.FormatTimespan(ReportList.FleetPurges[user.Id].getRemainingTime());
                embed.AddField("Правила рейда", fleetCodex, true);

                //Мут
                var mute = "Нет";
                if (ReportList.Mutes.ContainsKey(user.Id))
                    if (!ReportList.Mutes[user.Id].Expired())
                        mute = Utility.FormatTimespan(ReportList.Mutes[user.Id].getRemainingTime());
                embed.AddField("Мут", mute, true);

                //Заметка
                var note = "Отсутствует";
                if (Note.Notes.ContainsKey(user.Id))
                    note = Note.Notes[user.Id].Content;
                embed.AddField("Заметка", note, false);

                embed.WithFooter("(*) Не принял после разблокировки");

                await ctx.RespondAsync(embed: embed.Build());
            }
            catch (NotFoundException)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не найден.");
            }
        }

        [Command("updateDonatorMessageLegacy")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
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
            await ctx.Channel.DeleteMessageAsync(await ctx.Channel.GetMessageAsync(ctx.Channel.LastMessageId));

            //Обновляем настроки бота
            if (Bot.BotSettings.EmissaryMessageId != message.Id)
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
                if (field.Name == "Token") continue;
                settings.Add($"**{field.Name}** = {field.GetValue(Bot.BotSettings)}");
            }

            var settingsPagination = Utility.GeneratePagesInEmbeds(settings, "**Текущие настройки бота**");

            if (settingsPagination.Count() > 1)
                await interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, settingsPagination, timeoutoverride: TimeSpan.FromMinutes(5));
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

            await ctx.Channel.DeleteMessageAsync(await ctx.Channel.GetMessageAsync(ctx.Channel.LastMessageId));

            //Обновляем настроки бота
            if (Bot.BotSettings.EmissaryMessageId != message.Id)
                Bot.EditSettings("EmissaryMessageId", message.Id.ToString());

            //Убираем все реакции с сообщения
            await message.DeleteAllReactionsAsync();

            //Добавляем реакции к сообщению
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":moneybag:"));
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":pig:"));
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":skull:"));
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":gem:"));
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":skull_crossbones:"));
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":fish:"));
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":crossed_swords:"));
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":x:"));

        }
    }
}
