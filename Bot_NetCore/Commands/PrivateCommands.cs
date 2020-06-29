using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;
using SeaOfThieves.Entities;

namespace SeaOfThieves.Commands
{
    public class PrivateCommands
    {
        [Command("new")]
        [Description("Отправляет запрос на создание приватного корабля.")]
        public async Task New(CommandContext ctx, [Description("Уникальное имя корабля")] [RemainingText]
            string name)
        {
            var doc = XDocument.Load("actions.xml");
            foreach (var action in doc.Element("actions").Elements("action"))
                if (Convert.ToUInt64(action.Value) == ctx.Member.Id)
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не можете снова создать корабль!");
                    return;
                }

            var message = await ctx.Guild.GetChannel(Bot.BotSettings.PrivateRequestsChannel)
                .SendMessageAsync("**Запрос на создание корабля**\n\n" +
                                  $"**От:** {ctx.Member.Mention} ({ctx.Member.Id})\n" +
                                  $"**Название:** {name}\n" +
                                  $"**Время:** {DateTime.Now.ToUniversalTime()}\n\n" +
                                  $"Используйте реакцию или отправьте `{Bot.BotSettings.Prefix}confirm {name}` для подтверждения, или " +
                                  $"`{Bot.BotSettings.Prefix}decline {name}` для отказа.");
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":no_entry:"));

            var ship = Ship.Create(name, 0, 0, message.Id);
            ship.AddMember(ctx.Member.Id, MemberType.Owner);

            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

            doc.Element("actions").Add(new XElement("action", ctx.Member.Id, new XAttribute("type", "ship")));
            doc.Save("actions.xml");

            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Успешно отправлен запрос на создание корабля **{name}**!");
        }

        [Command("confirm")]
        [Description("Подтверждает создание корабля")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task Confirm(CommandContext ctx, [Description("Название корабля")] [RemainingText]
            string name)
        {
            if (!ShipList.Ships.ContainsKey(name))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не найден корабль с названием **{name}**!");
                return;
            }

            if (ShipList.Ships[name].Status)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Корабль с названием **{name}** уже активирован!");
                return;
            }

            var role = await ctx.Guild.CreateRoleAsync($"☠{name}☠", null, null, false, true);
            var channel = await ctx.Guild.CreateChannelAsync($"☠{name}☠", ChannelType.Voice,
                ctx.Guild.GetChannel(Bot.BotSettings.PrivateCategory), Bot.BotSettings.Bitrate);

            await channel.AddOverwriteAsync(role, Permissions.UseVoice, Permissions.None);
            await channel.AddOverwriteAsync(ctx.Guild.GetRole(Bot.BotSettings.CodexRole), Permissions.AccessChannels, Permissions.None);
            await channel.AddOverwriteAsync(ctx.Guild.EveryoneRole, Permissions.None, Permissions.UseVoice);

            var member = await ctx.Guild.GetMemberAsync(ShipList.Ships[name].Members.ToArray()[0].Value.Id);

            await member.GrantRoleAsync(role);

            ShipList.Ships[name].SetChannel(channel.Id);
            ShipList.Ships[name].SetRole(role.Id);
            ShipList.Ships[name].SetStatus(true);
            ShipList.Ships[name].SetMemberStatus(member.Id, true);

            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

            await member.SendMessageAsync(
                $"{Bot.BotSettings.OkEmoji} Запрос на создание корабля **{name}** был подтвержден администратором **{ctx.Member.Username}**");
            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Вы успешно подтвердили запрос на создание корабля **{name}**!");
        }

        [Command("decline")]
        [Description("Отклоняет создание корабля")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task Decline(CommandContext ctx, [Description("Название корабля")] [RemainingText]
            string name)
        {
            if (!ShipList.Ships.ContainsKey(name))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не найден корабль с названием **{name}**!");
                return;
            }

            if (ShipList.Ships[name].Status)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Корабль с названием **{name}** уже активирован!");
                return;
            }

            var member = await ctx.Guild.GetMemberAsync(ShipList.Ships[name].Members.ToArray()[0].Value.Id);

            ShipList.Ships[name].Delete();
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

            var doc = XDocument.Load("actions.xml");
            foreach (var action in doc.Element("actions").Elements("action"))
                if (Convert.ToUInt64(action.Value) == member.Id)
                    action.Remove();
            doc.Save("actions.xml");

            await member.SendMessageAsync(
                $"{Bot.BotSettings.OkEmoji} Запрос на создание корабля **{name}** был отклонен администратором **{ctx.Member.Username}**");
            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Вы успешно отклонили запрос на создание корабля **{name}**!");
        }

        [Command("invite")]
        [Aliases("i")]
        [Description("Приглашает участника на ваш корабль")]
        public async Task Invite(CommandContext ctx, [Description("Участник")] DiscordMember member)
        {
            var ship = ShipList.GetOwnedShip(ctx.Member.Id);
            if (ship == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь владельцем корабля!");
                return;
            }

            if (ctx.Member == member)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Нельзя пригласить самого себя!");
                return;
            }

            try
            {
                ship.AddMember(member.Id);
            }
            catch (MemberExistsException)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Участник уже был приглашен или является членом корабля!");
                return;
            }
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

            await member.SendMessageAsync(
                $"Вы были приглашены на корабль **{ship.Name}** участником **{ctx.Member.Username}**! Отправьте " +
                $"`{Bot.BotSettings.Prefix}yes {ship.Name}` для принятия приглашения, или `{Bot.BotSettings.Prefix}no {ship.Name}` для отказа.");
            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Успешно отправлено приглашение участнику {member.Username}!");
        }

        [Command("list")]
        [Description("Отправляет список членов вашего корабля")]
        public async Task List(CommandContext ctx)
        {
            var ship = ShipList.GetOwnedShip(ctx.Member.Id);
            if (ship == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь владельцем корабля!");
                return;
            }

            var fs = File.Create(ship.Name + ".txt");
            var sw = new StreamWriter(fs);

            foreach (var member in ship.Members.Values)
            {
                var type = "";

                DiscordMember discordMember = null;
                try
                {
                    discordMember = await ctx.Guild.GetMemberAsync(member.Id);
                }
                catch (NotFoundException)
                {
                    continue;
                }

                var status = "";

                if (!member.Status)
                    status = "приглашён";
                else
                    status = "член экипажа";
                switch (member.Type)
                {
                    case MemberType.Owner:
                        type = "Капитан";
                        break;
                    case MemberType.Member:
                        type = "Матрос";
                        break;
                }

                await sw.WriteLineAsync($"{type} {discordMember.DisplayName}#{discordMember.Discriminator}. " +
                                        $"Статус: {status}. Номер: {member.Id}.");
            }

            sw.Close();
            fs.Close();

            await ctx.Member.SendFileAsync(ship.Name + ".txt",
                $"{Bot.BotSettings.OkEmoji} Список членов экипажа вашего корабля.");
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Список членов экипажа отправлен в личные сообщения!");

            File.Delete(ship.Name + ".txt"); //дабы не плодить мусор
        }

        [Command("yes")]
        [Aliases("y")]
        [Description("Принимает приглашение на корабль")]
        public async Task Yes(CommandContext ctx, [Description("Корабль")] [RemainingText]
            string name)
        {
            Ship ship = null;
            try
            {
                ship = ShipList.Ships[name];
            }
            catch (KeyNotFoundException)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Корабль **{name}** не был найден! Проверьте правильность названия и попробуйте снова!");
                return;
            }

            if (!ship.IsInvited(ctx.Member.Id))
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Вы не были приглашены присоединиться к этому кораблю!");
                return;
            }

            var shipCount = 0;
            foreach (var _ship in ShipList.Ships.Values)
                foreach (var _member in _ship.Members.Values)
                    if (_member.Id == ctx.Member.Id && _member.Status)
                        ++shipCount;

            if (shipCount >= Bot.BotSettings.MaxPrivateShips)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} К сожалению, максимальное число приватных кораблей для вас - {Bot.BotSettings.MaxPrivateShips}");
                return;
            }


            ship.SetMemberStatus(ctx.Member.Id, true);
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

            await ctx.Member.GrantRoleAsync(ctx.Guild.GetRole(ship.Role));

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Добро пожаловать на борт корабля **{name}**!");
        }

        [Command("no")]
        [Aliases("n")]
        [Description("Отклоняет приглашение на корабль")]
        public async Task No(CommandContext ctx, [Description("Корабль")] [RemainingText]
            string name)
        {
            Ship ship = null;
            try
            {
                ship = ShipList.Ships[name];
            }
            catch (KeyNotFoundException)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Корабль **{name}** не был найден! Проверьте правильность названия и попробуйте снова!");
                return;
            }

            if (!ship.IsInvited(ctx.Member.Id))
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Вы не были приглашены присоединиться к этому кораблю!");
                return;
            }

            ship.RemoveMember(ctx.Member.Id);
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Вы успешно отклонили приглашение на корабль **{name}**!");
        }

        [Command("kickout")]
        [Description("Выгоняет участника с корабля")]
        public async Task Kick(CommandContext ctx, [Description("Участник")] DiscordMember member)
        {
            var ship = ShipList.GetOwnedShip(ctx.Member.Id);
            if (ship == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь владельцем корабля!");
                return;
            }

            if (ctx.Member == member)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Нельзя выгнать самого себя!");
                return;
            }

            if (!ship.Members.ContainsKey(member.Id))
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Этот участник не является членом вашего корабля!");
                return;
            }

            if (!ship.Members[member.Id].Status)
            {
                ship.RemoveMember(member.Id);
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.OkEmoji} Вы выгнали участника **{member.Username}** с корабля **{ship.Name}**!");
                return;
            }

            ship.RemoveMember(member.Id);
            await member.RevokeRoleAsync(ctx.Guild.GetRole(ship.Role));
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Вы выгнали участника **{member.Username}** с корабля **{ship.Name}**!");
            await member.SendMessageAsync($"Капитан **{ctx.Member.Username}** выгнал вас с корабля **{ship.Name}**!");
        }

        [Command("leave")]
        [Aliases("l")]
        [Description("Удаляет вас из списка членов корабля")]
        public async Task Leave(CommandContext ctx, [Description("Корабль")] [RemainingText]
            string name)
        {
            if (!ShipList.Ships[name].Members.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь членом этого корабля!");
                return;
            }

            if (ShipList.Ships[name].Members[ctx.Member.Id].Type == MemberType.Owner)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Вы должны передать права владельца корабля прежде чем покинуть его!");
                return;
            }

            if (!ShipList.Ships[name].Members[ctx.Member.Id].Status)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Чтобы отклонить приглашение используйте команду `!no`!");
                return;
            }

            ShipList.Ships[name].RemoveMember(ctx.Member.Id);
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

            await ctx.Member.RevokeRoleAsync(ctx.Guild.GetRole(ShipList.Ships[name].Role));

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Вы покинули корабль **{name}**!");
        }

        [Command("rename")]
        [Description("Переименовывает корабль")]
        public async Task Rename(CommandContext ctx, [RemainingText] [Description("Новое название")]
            string name)
        {
            var ship = ShipList.GetOwnedShip(ctx.Member.Id);
            if (ship == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь владельцем корабля!");
                return;
            }

            ship.Rename(name);
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);
            ShipList.ReadFromXML(Bot.BotSettings.ShipXML); //костыль адовый

            name = "☠" + name + "☠";

            await ctx.Guild.UpdateRoleAsync(ctx.Guild.GetRole(ship.Role), name);
            await ctx.Guild.GetChannel(ship.Channel).ModifyAsync(name);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно переименован корабль!");
        }

        [Command("prune")]
        [Description("Очищает корабль от участников, покинувших сервер.")]
        public async Task Prune(CommandContext ctx)
        {
            var ship = ShipList.GetOwnedShip(ctx.Member.Id);
            if (ship == null)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь владельцем корабля!");
                return;
            }

            var toBePruned = new List<ulong>();
            foreach (var member in ship.Members)
                try
                {
                    var m = await ctx.Guild.GetMemberAsync(member.Value.Id);
                }
                catch (NotFoundException)
                {
                    toBePruned.Add(member.Value.Id);
                }

            var i = 0;
            foreach (var member in toBePruned)
            {
                ship.RemoveMember(member);
                ++i;
            }

            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Успешно завершена очистка! Было удалено **{i}** человек.");
        }

        /* Секция для админ-команд */

        [Command("adelete")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task ADelete(CommandContext ctx, [RemainingText] string name)
        {
            if (!ShipList.Ships.ContainsKey(name))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не найден корабль с названием **{name}**!");
                return;
            }

            var ship = ShipList.Ships[name];

            var role = ctx.Guild.GetRole(ship.Role);
            var channel = ctx.Guild.GetChannel(ship.Channel);

            DiscordMember owner = null;
            foreach (var member in ship.Members.Values)
                if (member.Type == MemberType.Owner)
                {
                    owner = await ctx.Guild.GetMemberAsync(member.Id);
                    break;
                }

            ship.Delete();
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

            await ctx.Guild.DeleteRoleAsync(role);
            await channel.DeleteAsync();

            var doc = XDocument.Load("actions.xml");
            foreach (var action in doc.Element("actions").Elements("action"))
                if (owner != null && Convert.ToUInt64(action.Value) == owner.Id)
                    action.Remove();
            doc.Save("actions.xml");

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удален корабль!");

            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Удаление корабля**\n\n" +
                $"**Модератор:** {ctx.Member}\n" +
                $"**Корабль:** {name}\n" +
                $"**Владелец:** {owner}\n" +
                $"**Дата:** {DateTime.Now.ToUniversalTime()} UTC");
        }

        [Command("apurgereq")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task APurgeRequest(CommandContext ctx, int days = 3, bool force = false,
            [RemainingText] string forceReason = "Не указана")
        {
            var doc = XDocument.Load("active.xml");
            var root = doc.Root;

            foreach (var ship in ShipList.Ships.Values)
                //if (ship.Name != "test") continue;
                foreach (var member in ship.Members.Values)
                    if (member.Type == MemberType.Owner)
                        try
                        {
                            var owner = await ctx.Member.Guild.GetMemberAsync(member.Id);

                            if (force)
                            {
                                try
                                {
                                    await owner.SendMessageAsync(
                                        $"Ваш корабль **{ship.Name} будет автоматически удалён через {days} дня. " +
                                        $"Причина: {forceReason}.");
                                }
                                catch (UnauthorizedException)
                                {
                                }

                                root.Add(new XElement("Owner", new XAttribute("status", "ToDelete"), owner.Id));
                                continue;
                            }

                            if (ship.Members.Count < 4)
                            {
                                try
                                {
                                    await owner.SendMessageAsync(
                                        $"Ваш корабль **{ship.Name}** будет автоматически удалён через {days} дня, поскольку " +
                                        "в нём меньше, чем 4 человека.");
                                }
                                catch (UnauthorizedException)
                                {
                                }

                                root.Add(new XElement("Owner", new XAttribute("status", "ToDelete"), owner.Id));
                                continue;
                            }

                            try
                            {
                                await owner.SendMessageAsync(
                                    $"Поскольку вы являетесь владельцем корабля **{ship.Name}**, вы должны " +
                                    "подтвердить его активность командой `!active`, иначе он будет удален " +
                                    $"через {days} дня.");
                            }
                            catch (UnauthorizedException)
                            {
                            }

                            root.Add(new XElement("Owner", new XAttribute("status", "False"), owner.Id));
                        }
                        catch (NotFoundException)
                        {
                            root.Add(new XElement("Owner", new XAttribute("status", "ToDelete"), member.Id));
                        }

            doc.Save("active.xml");

            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Уведомления успешно разосланы. Удаление можно будет начать " +
                $"**{DateTime.Now.AddDays(days)}**");
        }

        [Command("apurgestart")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task APurgeStart(CommandContext ctx)
        {
            var doc = XDocument.Load("active.xml");
            var root = doc.Root;
            var elsToDelete = new List<XElement>();
            foreach (var ownerEl in root.Elements())
            {
                if (ownerEl.Attribute("status").Value != "True")
                {
                    var ship = ShipList.GetOwnedShip(Convert.ToUInt64(ownerEl.Value));
                    ctx.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot Purge", $"Ship deletion: {ship.Name}",
                        DateTime.Now);
                    try
                    {
                        await ctx.Guild.DeleteRoleAsync(ctx.Guild.GetRole(ship.Role));
                    }
                    catch (NotFoundException)
                    {
                    }
                    catch (NullReferenceException)
                    {
                    }

                    try
                    {
                        await ctx.Guild.GetChannel(ship.Channel).DeleteAsync();
                    }
                    catch (NotFoundException)
                    {
                    }
                    catch (NullReferenceException)
                    {
                    }

                    var owner = Convert.ToUInt64(ownerEl.Value);
                    var adoc = XDocument.Load("actions.xml");
                    foreach (var action in adoc.Element("actions").Elements("action"))
                        if (Convert.ToUInt64(action.Value) == owner)
                            action.Remove();
                    adoc.Save("actions.xml");

                    ship.Delete();
                }

                elsToDelete.Add(ownerEl);
            }

            for (var i = 0; i < elsToDelete.Count; ++i) elsToDelete[i].Remove();

            doc.Save("active.xml");
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Неактивные корабли успешно удалены!");
        }

        [Command("active")]
        public async Task Active(CommandContext ctx)
        {
            var doc = XDocument.Load("active.xml");
            var root = doc.Root;
            foreach (var ownerEl in root.Elements())
                if (ownerEl.Value == ctx.Member.Id.ToString())
                {
                    if (ownerEl.Attribute("status").Value == "False")
                    {
                        ownerEl.Attribute("status").Value = "True";
                        await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Вы успешно подтвердили активность!");
                        break;
                    }

                    if (ownerEl.Attribute("status").Value == "ToDelete")
                    {
                        await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} На вашем корабле меньше 4 человек!");
                        break;
                    }
                }

            doc.Save("active.xml");
        }

        [Command("shipinfo")]
        public async Task ShipInfo(CommandContext ctx, DiscordMember shipOwner)
        {
            //Временное ограничение, потом открою для модеров
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }

            //Get ship data
            var ownedShips = ShipList.Ships.Values.Where(s => s.Members.Values.Any(m => m.Type == MemberType.Owner && m.Id == shipOwner.Id));

            if (ownedShips.Count() == 0)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось найти корабли во владении!");

                //Не найдены приватные корабли, пробуем почистить список в actions.xml
                var doc = XDocument.Load("actions.xml");
                foreach (var action in doc.Element("actions").Elements("action"))
                    if (Convert.ToUInt64(action.Value) == shipOwner.Id && action.Attribute("type").Value == "ship")
                        action.Remove();
                doc.Save("actions.xml");

                return;
            }

            ownedShips.ToList().ForEach(async ship =>
            {
                var roleNeedFixes = false;
                var channelNeedFixes = false;

                var embed = new DiscordEmbedBuilder();
                embed.Title = ship.Name;

                embed.AddField("Статус", ship.Status ? "Подтвержден" : "Не подтвержден");

                //Роль
                embed.AddField("Роль", "_");
                embed.AddField("Роль в памяти", ship.Role.ToString(), true);
                try
                {
                    var role = ctx.Channel.Guild.GetRole(ship.Role);
                    embed.AddField("Роль в ДС", $"{role.Id} \n {role.Name}", true);
                }
                catch (NullReferenceException)
                {
                    embed.AddField("Роль в ДС", "Не найдена", true);
                    roleNeedFixes = true;
                }

                //Канал
                embed.AddField("Канал", "_");
                embed.AddField("Канал в памяти", ship.Channel.ToString(), true);
                try
                {
                    var channel = ctx.Channel.Guild.Channels.FirstOrDefault(x => x.Id == ship.Channel);
                    embed.AddField("Канал", $"{channel.Id} \n {channel.Name}", true);
                }
                catch (NullReferenceException)
                {
                    embed.AddField("Канал", "Не найден", true);
                    channelNeedFixes = true;
                }

                //Пользователи
                var users = "";
                ship.Members.ToList().ForEach(m => users += $"<@{m.Value.Id}> | {m.Value.Type} | {m.Value.Status} \n");
                embed.AddField("Пользователи", users);

                var msgContent = "";
                msgContent += roleNeedFixes ? "Не найдена роль " : "";
                msgContent += channelNeedFixes ? "Не найден канал " : "";

                if (roleNeedFixes || channelNeedFixes)
                    embed.Color = new DiscordColor("#FF0000");
                else
                    embed.Color = new DiscordColor("#00FF00");

                var message = await ctx.RespondAsync(content: msgContent, embed: embed.Build());

                if (roleNeedFixes || channelNeedFixes)
                {
                    // first retrieve the interactivity module from the client
                    var interactivity = ctx.Client.GetInteractivityModule();

                    // ok emoji
                    var okEmoji = DiscordEmoji.FromName(ctx.Client, ":tools:");

                    await message.CreateReactionAsync(okEmoji);

                    // wait for a reaction
                    var em = await interactivity.WaitForMessageReactionAsync(xe => xe.Name == okEmoji.Name, message, ctx.User, TimeSpan.FromSeconds(30));

                    await message.DeleteAllReactionsAsync();

                    try
                    {
                        if (em.Emoji.Name == okEmoji.Name)
                        {
                            //Create Role if needed
                            if (roleNeedFixes)
                            {
                                var role = await ctx.Guild.CreateRoleAsync($"☠{ship.Name}☠", null, null, false, true);
                                //await shipOwner.GrantRoleAsync(role);
                                ship.Members.Where(x => x.Value.Status).ToList()
                                    .ForEach(async x => {
                                        try
                                        {
                                            var member = await ctx.Guild.GetMemberAsync(x.Value.Id);
                                            await member.GrantRoleAsync(role);
                                            //await Task.Delay(500);
                                        }
                                        catch (NotFoundException)
                                        {
                                            ship.Members.Remove(x.Key);

                                        }
                                    });
                                ship.Role = role.Id;
                            }

                            //Create Channel if needed
                            if (channelNeedFixes)
                            {
                                var channel = await ctx.Guild.CreateChannelAsync($"☠{ship.Name}☠", ChannelType.Voice,
                                       ctx.Guild.GetChannel(Bot.BotSettings.PrivateCategory), Bot.BotSettings.Bitrate);

                                var role = ctx.Channel.Guild.GetRole(ship.Role);
                                await channel.AddOverwriteAsync(role, Permissions.UseVoice, Permissions.None);
                                await channel.AddOverwriteAsync(ctx.Guild.GetRole(Bot.BotSettings.CodexRole), Permissions.AccessChannels, Permissions.None);
                                await channel.AddOverwriteAsync(ctx.Guild.EveryoneRole, Permissions.None, Permissions.UseVoice);

                                ship.Channel = channel.Id;
                            }

                            //Sync Role and Channel if needed
                            if (roleNeedFixes && channelNeedFixes == false)
                            {
                                var role = ctx.Channel.Guild.GetRole(ship.Role);
                                await ctx.Channel.Guild.GetChannel(ship.Channel).AddOverwriteAsync(role, Permissions.UseVoice, Permissions.None);
                            }

                            //Save Data
                            ShipList.Ships[ship.Name].SetChannel(ship.Channel);
                            ShipList.Ships[ship.Name].SetRole(ship.Role);

                            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

                            await message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
                        }
                    }
                    catch
                    {
                        await ctx.RespondAsync("Время ответа вышло, заново введите команду `shipInfo`");
                    }
                }
            });
        }

    }
}
