using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;
using SeaOfThieves.Entities;

namespace SeaOfThieves.Commands
{
    public class UtilsCommands
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
        [Hidden]
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
                embed.WithAuthor($"{member.Username}#{member.Discriminator}", icon_url: member.AvatarUrl);
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

            await channel.DeleteMessagesAsync(await channel.GetMessagesAsync(100, channel.LastMessageId));
            await channel.DeleteMessageAsync(await channel.GetMessageAsync(channel.LastMessageId));

            var fso = File.Open("donators_messages.txt", FileMode.OpenOrCreate);
            var sr = new StreamReader(fso);

            var messageId = sr.ReadLine();
            while (messageId != null)
            {
                try
                {
                    await channel.DeleteMessageAsync(await channel.GetMessageAsync(Convert.ToUInt64(messageId)));
                }
                catch (NotFoundException)
                {
                }

                messageId = sr.ReadLine();
            }

            sr.Close();
            fso.Close();

            var donators = new Dictionary<ulong, double>(); //список донатеров, который будем сортировать
            foreach (var donator in DonatorList.Donators.Values)
                if (!donator.Hidden)
                    donators.Add(donator.Member, donator.Balance);

            var ordered = donators.OrderBy(x => -x.Value);

            var messageCount = ordered.Count() / 10;
            if (ordered.Count() % 10 != 0) ++messageCount;

            int position = 0, balance = int.MaxValue, str = 1;
            var message = "";

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

                if ((int)Math.Floor(el.Value) < balance)
                {
                    ++position;
                    balance = (int)Math.Floor(el.Value);
                }

                try
                {
                    var user = await ctx.Client.GetUserAsync(el.Key);
                    message += $"**{position}.** {user.Username}#{user.Discriminator} - *{el.Value}₽*\n";
                    ++str;
                }
                catch (NotFoundException)
                {
                }
            }

            if (str % 10 != 0)
            {
                var sendedMessage = await channel.SendMessageAsync(message);
                sw.WriteLine(sendedMessage.Id);
            }

            sw.Close();
            fs.Close();

            await ctx.Message.DeleteAsync();
        }

        [Command("resetfleet")]
        [Hidden]
        public async Task ResetFleetChannels(CommandContext ctx) //Команда для сброса названий и слотов каналов рейда после "рейдеров"
        {
            if (!Bot.IsModerator(ctx.Member)) //Проверка на права модератора. (копипаст с команды clearchannel)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            int startPosition = ctx.Guild.GetChannel(Bot.BotSettings.FleetChillChannel).Position + 1; //Начало отсчета от канала Chill

            //Сперва обновляем общий канал и его позицию, если изменена
            var fleetLobby = ctx.Guild.GetChannel(Bot.BotSettings.FleetLobby);
            if (fleetLobby.Position != startPosition)
                await fleetLobby.ModifyAsync(name: "Общий", position: startPosition, user_limit: 99);
            else
                await fleetLobby.ModifyAsync(name: "Общий", user_limit: 99);

            //Обновляем остальные каналы
            int i = 1;
            foreach (var fleetChannel in ctx.Guild.GetChannel(Bot.BotSettings.FleetCategory).Children)
            {
                //Убираем из списка очистки текстовые каналы и голосовой канал Chill
                if (fleetChannel.Type == ChannelType.Voice && fleetChannel.Id != Bot.BotSettings.FleetChillChannel && fleetChannel.Id != Bot.BotSettings.FleetLobby)
                {
                    //Обновляем канал и позицию в списке, если изменена
                    if (fleetChannel.Position != startPosition + i)
                        await fleetChannel.ModifyAsync(name: $"Рейд#{i}", position: startPosition + 1, user_limit: Bot.BotSettings.FleetUserLimiter);
                    else
                        await fleetChannel.ModifyAsync(name: $"Рейд#{i}", user_limit: Bot.BotSettings.FleetUserLimiter);
                    i++;
                }
            }
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно сброшены каналы рейда!");
        }

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
        public Task StopRainbow(CommandContext ctx)
        {
            keepRainbow = false;
            return Task.CompletedTask;
        }

        [Command("time")]
        public async Task Time(CommandContext ctx)
        {
            await ctx.RespondAsync($"Текущее время на сервере: **{DateTime.Now}**.");
        }

        [Command("showsettings")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task ShowSettings(CommandContext ctx)
        {
            var message = "**Текущие настройки бота:**\n";
            foreach (var field in typeof(Settings).GetFields())
            {
                if (field.Name == "Token") continue;
                message += $"**{field.Name}** = {field.GetValue(Bot.BotSettings)}\n";
            }

            await ctx.RespondAsync(message);
        }

        [Command("emissarymessage")]
        [Description("Обновляет привязку к сообщению эмиссаров (вводится в канале с сообщением)")]
        [Hidden]
        public async Task UpdateEmissaryMessage(CommandContext ctx, DiscordMessage message)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            await ctx.Channel.DeleteMessageAsync(await ctx.Channel.GetMessageAsync(ctx.Channel.LastMessageId));

            //Обновляем настроки бота
            if(Bot.BotSettings.EmissaryMessageId != message.Id)
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

        [Command("gcte")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task GiveCodexRoleToEveryone(CommandContext ctx)
        {
            var members = await ctx.Guild.GetAllMembersAsync();
            for (int i = 0; i < members.Count; ++i)
            {
                Console.WriteLine("Member " + i);
                try
                {
                    await members[i].GrantRoleAsync(ctx.Guild.GetRole(Bot.BotSettings.CodexRole));
                }
                catch (Exception)
                {
                    
                }
            }
        }
    }
}
