using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
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
        public async Task GenerateDonatorMessage(CommandContext ctx, [Description("Параметр")] string param, [Description("Значение")] string value)
        {
            try
            {
                Bot.EditSettings(param, value);
                Bot.ReloadSettings();

                await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно изменен параметр `{param}: {value}`");
            }
            catch (Exception ex)
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
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task WhoIs(CommandContext ctx, DiscordMember member)
        {
            try
            {
                var embed = new DiscordEmbedBuilder();
                embed.WithAuthor($"{member.Username}#{member.Discriminator}", icon_url: member.AvatarUrl);
                embed.AddField("ID", member.Id.ToString(), true);
                embed.WithColor(DiscordColor.Blurple);

                embed.AddField("Имя на сервере", member.Username);

                var warnings = 0;
                if (UserList.Users.ContainsKey(member.Id)) warnings = UserList.Users[member.Id].Warns.Count;
                embed.AddField("Предупреждения", warnings.ToString(), true);

                var donate = 0;
                if (DonatorList.Donators.ContainsKey(member.Id)) donate = (int)DonatorList.Donators[member.Id].Balance;
                embed.AddField("Донат", donate.ToString(), true);

                var moderator = "Нет";
                if (Bot.IsModerator(member)) moderator = "Да";
                embed.AddField("Модератор", moderator, true);

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

                await ctx.RespondAsync(embed: embed.Build());
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
                //TODO: Можно изменить на
                //var message = await ctx.Guild.GetChannel(channelId).SendMessageAsync("**Топ донатов**");
                //Bot.EditSettings("DonatorMessage", message.Id.ToString());
                //Bot.ReloadSettings();
                var message = await ctx.Guild.GetChannel(channelId).SendMessageAsync("**Топ донатов**");
                var doc = XDocument.Load("settings.xml");
                doc.Element("Settings").Element("DonatorMessage").Value = Convert.ToString(message.Id);
                Bot.ReloadSettings();
            }
            catch (Exception)
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

            int i = 1;
            //Пока убрал смену позиции каналов, так как вызывает долгую задержу при выполнении команды
            //int chillPosition = ctx.Guild.GetChannel(Bot.BotSettings.FleetChillChannel).Position;
            foreach (var fleetChannel in ctx.Guild.GetChannel(Bot.BotSettings.FleetCategory).Children)
                if (fleetChannel.Type == ChannelType.Voice && fleetChannel.Id != Bot.BotSettings.FleetChillChannel) //Убираем из списка очистки текстовые каналы и голосовой канал Chill
                    if (fleetChannel.Id == Bot.BotSettings.FleetLobby) //Учитываем присутствие канала лобби
                    {
                        await fleetChannel.ModifyAsync(name: "Общий", user_limit: 99); //Дискорд не показывает сколько людей находится в канале, так что 99
                        //await fleetChannel.ModifyPositionAsync(chillPosition + 1);
                    }
                    else
                    {
                        await fleetChannel.ModifyAsync(name: $"Рейд#{i}", user_limit: Bot.BotSettings.FleetUserLimiter);
                        //await fleetChannel.ModifyPositionAsync(chillPosition + i + 1);
                        i++;
                    }
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно сброшены каналы рейда!");
        }

        [Command("invitesLeaderboard")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task InvitesLeaderboard(CommandContext ctx) //Команда для создания/обновления лидерборда
        {
            await ctx.Channel.DeleteMessageAsync(await ctx.Channel.GetMessageAsync(ctx.Channel.LastMessageId));

            await InvitesLeaderboard(ctx.Guild);
        }

        [Command("invitesLeaderboardAll")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task InvitesLeaderboardAll(CommandContext ctx) //Выводит список всех пригласивших, в том числе и спрятанных
        {
            await ctx.Channel.DeleteMessageAsync(await ctx.Channel.GetMessageAsync(ctx.Channel.LastMessageId));

            var interactivity = ctx.Client.GetInteractivityModule();

            List<string> inviters = new List<string>();

            InviterList.Inviters.ToList()
                .OrderByDescending(x => x.Value.Referrals.Count).ToList()
                .ForEach(async x =>
                {
                    try
                    {
                        var inviter = await ctx.Guild.GetMemberAsync(x.Key);
                        string state = x.Value.Active == true ? "Активен" : "Отключен";
                        inviters.Add($"{inviter.DisplayName}#{inviter.Discriminator} пригласил {x.Value.Referrals.Count} Отображение: {state}");
                    }
                    catch (NotFoundException)
                    {
                        inviters.Add("Пользователь не найден");
                    }
                });

            var inviters_pagination = Utility.GeneratePagesInEmbeds(inviters);

            await interactivity.SendPaginatedMessage(ctx.Channel, ctx.User, inviters_pagination, timeoutoverride: TimeSpan.FromMinutes(5));
        }

        [Command("updateLeaderboardMember")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task UpdateLeaderboardMember(CommandContext ctx, [Description("Участник")] DiscordMember member) //Обновляет статус отображения пользователя в leaderboard
        {
            try
            {
                await ctx.Channel.DeleteMessageAsync(await ctx.Channel.GetMessageAsync(ctx.Channel.LastMessageId));

                InviterList.Inviters.Where(x => x.Key == member.Id).ToList()
                    .ForEach(x => x.Value.UpdateState(!x.Value.Active));
                InviterList.SaveToXML(Bot.BotSettings.InviterXML);

                await InvitesLeaderboard(ctx.Guild);
            }
            catch (Exception ex)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Произошла ошибка при выполнении команды! {ex.Message}");
            }
        }

        [Command("codexgen")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task CodexGenerateMessage(CommandContext ctx)
        {
            var channel = ctx.Guild.GetChannel(Bot.BotSettings.CodexChannel);
            var message = "**Я прочитал правила и обязуюсь их выполнять.**";
            var messageEnt = await channel.SendMessageAsync(message);

            using (var fs = File.Create("codex_message"))
            using (var sw = new StreamWriter(fs))
            {
                sw.WriteLine(messageEnt.Id);
            }

            await messageEnt.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji}");
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

        [Command("listinvites")]
        [Hidden]
        public async Task ListInvites(CommandContext ctx, DiscordMember member)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            try
            {
                var interactivity = ctx.Client.GetInteractivityModule();

                var inviter = InviterList.Inviters[member.Id];
                List<string> referrals = new List<string>();

                foreach (ulong referralId in inviter.Referrals)
                {
                    try
                    {
                        var referral = await ctx.Guild.GetMemberAsync(referralId);
                        referrals.Add($"{referral.Id} - {referral.Username}#{referral.Discriminator} ({referral.Nickname})");
                    }
                    catch (NotFoundException)
                    {
                        referrals.Add($"Пользователь не найден");
                    }

                }

                var referrals_pagination = Utility.GeneratePagesInEmbeds(referrals);

                await interactivity.SendPaginatedMessage(ctx.Channel, ctx.User, referrals_pagination, timeoutoverride: TimeSpan.FromMinutes(5));
            }
            catch (KeyNotFoundException)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не был найден указанный участник!");
            }
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

            try
            {
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
                await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":x:"));
            }
            catch (Exception ex)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Произошла ошибка при выполнении команды! {ex.Message}");
            }

        }

        public static async Task<Task> InvitesLeaderboard(DiscordGuild guild)
        {
            var channel = guild.GetChannel(Bot.BotSettings.InvitesLeaderboardChannel);

            var inviters = InviterList.Inviters.ToList()
                .OrderByDescending(x => x.Value.Referrals.Count).ToList()
                .FindAll(x =>
                {
                    try
                    {
                        if (!x.Value.Active)
                            return false;
                        guild.GetMemberAsync(x.Key);
                        return true;
                    }
                    catch (NotFoundException)
                    {
                        return false;
                    }
                })
                .Take(10).ToList();

            var embed = new DiscordEmbedBuilder
            {
                Color = new DiscordColor("#CC00CC"),
                Title = "Топ рефералов",
            };

            int i = 0;
            foreach (var el in inviters)
            {
                try
                {
                    if (el.Value.Referrals.Count > 0)
                    {
                        var user = await guild.GetMemberAsync(el.Key);
                        i++;
                        embed.AddField(
                            $"{i}. {user.DisplayName}#{user.Discriminator}",
                            $"пригласил {el.Value.Referrals.Count} пользователей");
                    }
                }
                catch (NotFoundException)
                {
                    //Пользователь не найден, так что пропускаем и не добавляем в статистику
                }
            }

            embed.WithFooter("Чтобы попасть в топ, создайте собственную ссылку приглашения");

            //Публикуем и проверяем на уже существующую таблицу топ 10
            var messages = await channel.GetMessagesAsync();
            ulong messageId = 0;
            if (messages.Count > 0)
                messageId = messages.ToList().Where(x => (x.Author.Id == guild.CurrentMember.Id)).First().Id;

            if (messageId == 0)
                await channel.SendMessageAsync(embed: embed.Build());
            else
                await channel.GetMessageAsync(messageId).Result.ModifyAsync(embed: embed.Build());

            return Task.CompletedTask;
        }
    }
}