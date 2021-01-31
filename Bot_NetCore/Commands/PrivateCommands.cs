using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bot_NetCore.Entities;
using Bot_NetCore.Exceptions;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.Logging;
using NotFoundException = DSharpPlus.Exceptions.NotFoundException;

namespace Bot_NetCore.Commands
{
    [Group("private")]
    [Aliases("p")]
    [Description("Команды приватных кораблей.")]
    [RequireGuild]
    public class PrivateCommands : BaseCommandModule
    {
        [Command("new")]
        [Description("Отправляет запрос на создание приватного корабля.")]
        public async Task New(CommandContext ctx, [Description("Уникальное имя корабля")] [RemainingText] string name)
        {
            // check if user already has a ship
            if (PrivateShip.GetOwnedShip(ctx.Member.Id) != null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Ты уже являешься владельцем корабля.");
                return;
            }

            var requestTime = DateTime.Now;
            
            // check if there is a ship with the same name
            if (PrivateShip.Get(name) != null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Корабль с таким именем уже существует.");
                return;
            }

            // create a new ship
            var ship = PrivateShip.Create(name, requestTime, 0);
            ship.AddMember(ctx.Member.Id, PrivateShipMemberRole.Captain, false);
            
            // create a request message
            var requestsChannel = ctx.Guild.GetChannel(Bot.BotSettings.PrivateRequestsChannel);
            var requestText = "**Запрос на создание корабля**\n\n" +
                              $"**От:** {ctx.Member.Mention} ({ctx.Member.Id})\n" +
                              $"**Название:** {name}\n" +
                              $"**Время:** {DateTime.Now}\n\n" +
                              $"Используйте :white_check_mark: для подтверждения или :no_entry: для отказа.";
            var message = await requestsChannel.SendMessageAsync(requestText);
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":no_entry:"));

            ship.RequestMessage = message.Id;

            // notify user
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Запрос успешно отправлен");
        }

        [Command("invite")]
        [Aliases("i")]
        [Description("Приглашает участника на ваш корабль")]
        public async Task Invite(CommandContext ctx, [Description("Участник")] DiscordMember member)
        {
            var ship = PrivateShip.GetOwnedShip(ctx.Member.Id);
            if (ship == null || ship.GetCaptain().Status == false)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Ты не являешься владельцем корабля или твой запрос ещё не был подтвержден.");
                return;
            }

            if (ship.GetMembers().Any(m => m.MemberId == member.Id))
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Пользователь уже приглашен или является участником корабля.");
                return;
            }

            ship.AddMember(member.Id, PrivateShipMemberRole.Member, false);
            try
            {
                await member.SendMessageAsync(
                    $"Ты был приглашён присоединиться к кораблю **{ship.Name}**. Используй в канале для команд " +
                    $"`!p yes {ship.Name}`, чтобы принять приглашение, или `!p no {ship.Name}`, чтобы отклонить его.");
            }
            catch (UnauthorizedException)
            {
                
            }

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Приглашение успешно отправлено.");
        }

        [Command("list")]
        [Description("Отправляет список членов вашего корабля")]
        public async Task List(CommandContext ctx)
        {
            var ship = PrivateShip.GetOwnedShip(ctx.Member.Id);
            if (ship == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь владельцем корабля!");
                return;
            }

            var members = ship.GetMembers().OrderByDescending(m => m.Role).ToList();

            await ctx.Channel.TriggerTypingAsync();

            var memberList = new List<string>();
            foreach (var member in members)
            {
                DiscordMember discordMember = null;
                try
                {
                    discordMember = await ctx.Guild.GetMemberAsync(member.MemberId);
                }
                catch (NotFoundException)
                {
                    continue;
                }

                var type = PrivateShipMember.RoleEnumToStringRu(member.Role);

                memberList.Add($"{type} {discordMember.DisplayName}#{discordMember.Discriminator}.");
            }

            var interactivity = ctx.Client.GetInteractivity();
            var membersPagination = Utility.GeneratePagesInEmbeds(memberList, $"Список членов экипажа вашего корабля.");

            if (memberList.Count() > 1)
                await interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, membersPagination, timeoutoverride: TimeSpan.FromMinutes(5));
            else
                await ctx.RespondAsync(embed: membersPagination.First().Embed);
        }

        [Command("yes")]
        [Aliases("y")]
        [Description("Принимает приглашение на корабль")]
        public async Task Yes(CommandContext ctx, [Description("Корабль")] [RemainingText]
                string name)
        {
            var ship = PrivateShip.Get(name);
            if (ship == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Этот корабль не был найден.");
                return;
            }

            var selectedMembers = (from member in ship.GetMembers()
                where member.MemberId == ctx.Member.Id
                select member).ToList();
            if (!selectedMembers.Any())
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Ты не был приглашён присоединиться к этому кораблю.");
                return;
            }

            var shipMember = selectedMembers.First();
            if (shipMember.Status)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Ты уже являешься участником этого корабля.");
                return;
            }

            shipMember.Status = true;

            await ctx.Guild.GetChannel(ship.Channel).AddOverwriteAsync(ctx.Member, Permissions.UseVoice);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Добро пожаловать на борт корабля **{name}**!");
        }

        [Command("no")]
        [Aliases("n")]
        [Description("Отклоняет приглашение на корабль")]
        public async Task No(CommandContext ctx, [Description("Корабль")] [RemainingText]
            string name)
        {
            var ship = PrivateShip.Get(name);
            if (ship == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Этот корабль не был найден.");
                return;
            }

            var selectedMembers = (from member in ship.GetMembers()
                where member.MemberId == ctx.Member.Id
                select member).ToList();
            if (!selectedMembers.Any())
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Ты не был приглашён присоединиться к этому кораблю.");
                return;
            }
            
            ship.RemoveMember(ctx.Member.Id);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Ты успешно отклонил приглашение.");
        }

        [Command("kick")]
        [Description("Выгоняет участника с корабля")]
        public async Task Kick(CommandContext ctx, [Description("Участник")] DiscordMember member)
        {
            var ship = PrivateShip.GetOwnedShip(ctx.Member.Id);
            if (ship == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Ты не являешься владельцем корабля.");
                return;
            }

            if (ctx.Member == member)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Нельзя выгнать самого себя.");
                return;
            }

            var members = ship.GetMembers();
            var selected = members.Find(m => m.MemberId == member.Id);
            if (selected == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Этого участника нет на корабле.");
                return;
            }

            ship.RemoveMember(selected.MemberId);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Ты успешно выгнал участника с корабля.");
            try
            {
                if (selected.Status)
                {
                    await ctx.Guild.GetChannel(ship.Channel).AddOverwriteAsync(member);
                    await member.SendMessageAsync($"Капитан **{ctx.Member.DisplayName}#{ctx.Member.Discriminator}** " +
                                                  $"выгнал тебя с корабля **{ship.Name}**");
                }
                else
                    await member.SendMessageAsync($"Капитан **{ctx.Member.DisplayName}#{ctx.Member.Discriminator}** " +
                                                  $"отменил твоё приглашение на корабль **{ship.Name}**");
            }
            catch (UnauthorizedException)
            {
                
            }
        }

        [Command("leave")]
        [Aliases("l")]
        [Description("Удаляет вас из списка членов корабля")]
        public async Task Leave(CommandContext ctx, [Description("Корабль")] [RemainingText]
            string name)
        {
            var ship = PrivateShip.Get(name);
            if (ship == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Корабль не был найден.");
                return;
            }

            var shipMember = ship.GetMembers().Find(m => m.MemberId == ctx.Member.Id);
            if (shipMember == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Ты не являешься участником этого корабля.");
                return;
            }

            if (shipMember.Role == PrivateShipMemberRole.Captain)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Прежде чем покинуть корабль, передай полномочия " +
                    $"капитана с помощью команды `!p transfer @участник`.");
                return;
            }

            ship.RemoveMember(ctx.Member.Id);

            if (shipMember.Status)
            {
                await ctx.Guild.GetChannel(ship.Channel).AddOverwriteAsync(ctx.Member);
                await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Ты покинул корабль **{ship.Name}**.");
            }
            else
            {
                await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Ты успешно отклонил приглашение присоединиться к кораблю.");
            }
        }

        [Command("rename")]
        [Description("Переименовывает корабль")]
        public async Task Rename(CommandContext ctx, [RemainingText] [Description("Новое название")]
            string name)
        {
            var ship = PrivateShip.GetOwnedShip(ctx.Member.Id);
            if (ship == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Ты не являешься владельцем корабля");
                return;
            }

            ship.Name = name;
            name = "☠" + name + "☠";
            await ctx.Guild.GetChannel(ship.Channel).ModifyAsync(x => x.Name = name);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно переименован корабль");
        }

        [Command("transfer")]
        [Description("Передаёт права на корабль")]
        public async Task Transfer(CommandContext ctx, [Description("Новый капитан")] DiscordMember member)
        {
            var ship = PrivateShip.GetOwnedShip(ctx.Member.Id);
            if (ship == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Ты не являешься владельцем корабля");
                return;
            }

            if (ctx.Member == member)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Нельзя передать права самому себе");
                return;
            }

            var members = ship.GetMembers();
            var oldCaptain = members.Find(m => m.Role == PrivateShipMemberRole.Captain);
            var newCaptain = members.Find(m => m.MemberId == member.Id);

            if (newCaptain == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Нельзя передать права пользователю, которого нет на твоём корабле");
                return;
            }

            newCaptain.Role = PrivateShipMemberRole.Captain;
            if (oldCaptain != null) oldCaptain.Role = PrivateShipMemberRole.Member;

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Ты успешно передал должность капитана");
            try
            {
                await member.SendMessageAsync($"Ты был назначен капитаном корабля **{ship.Name}**");
                return;
            }
            catch (UnauthorizedException)
            {
                
            }
        }

        /* Секция для админ-команд */

        [Command("adelete")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task ADelete(CommandContext ctx, [RemainingText] string name)
        {
            if (!ShipList.Ships.ContainsKey(name))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не найден корабль с названием **{name}**!");
                return;
            }

            var ship = ShipList.Ships[name];

            var channel = ctx.Guild.GetChannel(ship.Channel);

            ulong owner = 0;
            foreach (var member in ship.Members.Values)
                if (member.Type == MemberType.Owner)
                {
                    owner = member.Id;
                    break;
                }

            ship.Delete();
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

            try
            {
                await channel.DeleteAsync();
            }
            catch (DSharpPlus.Exceptions.NotFoundException) { await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не был найден канал для удаления..."); }
            catch (NullReferenceException) { await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не был найден канал для удаления..."); }

            var doc = XDocument.Load("data/actions.xml");
            foreach (var action in doc.Element("actions").Elements("action"))
                if (owner != 0 && Convert.ToUInt64(action.Value) == owner)
                    action.Remove();
            doc.Save("data/actions.xml");

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удален корабль!");

            DiscordUser user = null;
            try
            {
                user = await ctx.Client.GetUserAsync(owner);
            }
            catch (DSharpPlus.Exceptions.NotFoundException) { }

            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Удаление корабля**\n\n" +
                $"**Модератор:** {ctx.Member}\n" +
                $"**Корабль:** {name}\n" +
                $"**Владелец:** {user}\n" +
                $"**Дата:** {DateTime.Now}");
        }

        [Command("shipinfo")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task ShipInfo(CommandContext ctx, [RemainingText] string name)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            if (!ShipList.Ships.ContainsKey(name))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не найден корабль с названием **{name}**!");
                return;
            }

            //Get ship data
            var ship = ShipList.Ships[name];

            var embed = new DiscordEmbedBuilder();
            embed.Title = ship.Name;

            embed.AddField("Статус", ship.Status ? "Подтвержден" : "Не подтвержден");

            //Канал
            embed.AddField("Канал в памяти", ship.Channel.ToString(), true);
            try
            {
                var channel = ctx.Channel.Guild.Channels.FirstOrDefault(x => x.Value.Id == ship.Channel);
                embed.AddField("Канал", $"{channel.Value.Id} \n {channel.Value.Name}", true);
            }
            catch (NullReferenceException)
            {
                embed.AddField("Канал", "Не найден", true);
            }

            var msgContent = "";


            embed.Color = new DiscordColor("#00FF00");

            var message = await ctx.RespondAsync(content: msgContent, embed: embed.Build());

            // first retrieve the interactivity module from the client
            var interactivity = ctx.Client.GetInteractivity();

            // list emoji
            var listEmoji = DiscordEmoji.FromName(ctx.Client, ":scroll:");

            await message.CreateReactionAsync(listEmoji);

            // wait for a reaction
            var em = await interactivity.WaitForReactionAsync(xe => xe.Emoji.Name == listEmoji.Name, message, ctx.User, TimeSpan.FromSeconds(30));

            try
            {
                //Чистим реакции, они больше не кликабельны
                await message.DeleteAllReactionsAsync();

                //Список пользователей
                if (em.Result.Emoji.Name == listEmoji.Name)
                {
                    List<string> members = new List<string>();
                    await ctx.Channel.TriggerTypingAsync();
                    ship.Members.ToList().ForEach(m => members.Add($"<@{m.Value.Id}> | {m.Value.Type} | {m.Value.Status}"));

                    var members_pagination = Utility.GeneratePagesInEmbeds(members, $"Список членов экипажа.");

                    if (members_pagination.Count() > 1)
                        await interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, members_pagination, timeoutoverride: TimeSpan.FromMinutes(5));
                    else
                        await ctx.RespondAsync(embed: members_pagination.First().Embed);
                }
            }
            catch
            {
                await message.DeleteAllReactionsAsync();
            }
        }

        [Command("usershipinfo")]
        [RequirePermissions(Permissions.KickMembers)]
        public async Task UserShipInfo(CommandContext ctx, DiscordMember member)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            //Get ship data
            var ownedShips = ShipList.Ships.Values.Where(s => s.Members.Values.Any(m => m.Type == MemberType.Owner && m.Id == member.Id));

            if (ownedShips.Count() == 0)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось найти корабли во владении!");

                //Не найдены приватные корабли, пробуем почистить список в actions.xml
                var doc = XDocument.Load("data/actions.xml");
                foreach (var action in doc.Element("actions").Elements("action"))
                    if (Convert.ToUInt64(action.Value) == member.Id && action.Attribute("type").Value == "ship")
                        action.Remove();
                doc.Save("data/actions.xml");

                return;
            }
            else
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У пользователя есть корабль во владении! \n" +
                                       $"Название корабля: {ownedShips.FirstOrDefault().Name}");
            }
        }

    }
}
