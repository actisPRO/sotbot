using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using ShipAPI;

namespace SeaOfThieves.Commands
{
    public class PrivateCommands
    {
        [Command("new")]
        [Description("Отправляет запрос на создание приватного корабля.")]
        public async Task New(CommandContext ctx, [Description("Уникальное имя корабля")][RemainingText]
            string name)
        {
            var doc = XDocument.Load("actions.xml");
            foreach (var action in doc.Element("actions").Elements("action"))
            {
                if (Convert.ToUInt64(action.Value) == ctx.Member.Id)
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не можете снова создать корабль!");
                    return;
                }
            }

            var ship = Ship.Create(name, 0, 0);
            ship.AddMember(ctx.Member.Id, MemberType.Owner);
                
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);
            
            doc.Element("actions").Add(new XElement("action", ctx.Member.Id, new XAttribute("type", "ship")));
            doc.Save("actions.xml");

            await ctx.Guild.GetChannel(Bot.BotSettings.PrivateRequestsChannel)
                .SendMessageAsync($"**Запрос на создание корабля**\n\n" +
                                  $"**От:** {ctx.Member.Mention} ({ctx.Member.Id})\n" +
                                  $"**Название:** {name}\n" +
                                  $"**Время:** {DateTime.Now.ToUniversalTime()}\n\n" +
                                  $"Отправьте `{Bot.BotSettings.Prefix}confirm {name}` для подтверждения, или " +
                                  $"`{Bot.BotSettings.Prefix}decline {name}` для отказа.");

            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Успешно отправлен запрос на создание корабля **{name}**!");
        }

        [Command("confirm")]
        [Description("Подтверждает создание корабля")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task Confirm(CommandContext ctx, [Description("Название корабля")][RemainingText]
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
            
            var role = await ctx.Guild.CreateRoleAsync($"☠{name}☠", null, new DiscordColor("7e41ad"), true, true);
            var channel = await ctx.Guild.CreateChannelAsync($"☠{name}☠", ChannelType.Voice, 
                ctx.Guild.GetChannel(Bot.BotSettings.PrivateCategory), Bot.BotSettings.Bitrate);
            
            await channel.AddOverwriteAsync(role, Permissions.UseVoice, Permissions.None);
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
        public async Task Decline(CommandContext ctx, [Description("Название корабля")][RemainingText]
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
            {
                if (Convert.ToUInt64(action.Value) == member.Id)
                {
                    action.Remove();
                }
            }
            doc.Save("actions.xml");
            
            await member.SendMessageAsync(
                $"{Bot.BotSettings.OkEmoji} Запрос на создание корабля **{name}** был отклонен администратором **{ctx.Member.Username}**");
            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Вы успешно отклонили запрос на создание корабля **{name}**!");
        }

        [Command("invite"), Aliases("i")]
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
            
            ship.AddMember(member.Id);
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
                var discordMember = await ctx.Guild.GetMemberAsync(member.Id);
                var status = "";

                if (member.Status)
                {
                    status = "приглашён";
                }
                else
                {
                    status = "член экипажа";
                }
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

        [Command("yes"), Aliases("y")]
        [Description("Принимает приглашение на корабль")]
        public async Task Yes(CommandContext ctx, [Description("Корабль")][RemainingText] string name)
        {
            var ship = ShipList.Ships[name];
            if (!ship.IsInvited(ctx.Member.Id))
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Вы не были приглашены присоединиться к этому кораблю!");
                return;
            }
            
            ship.SetMemberStatus(ctx.Member.Id, true);
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

            await ctx.Member.GrantRoleAsync(ctx.Guild.GetRole(ship.Role));

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Добро пожаловать на борт корабля **{name}**!");
        }

        [Command("no"), Aliases("n")]
        [Description("Отклоняет приглашение на корабль")]
        public async Task No(CommandContext ctx, [Description("Корабль")][RemainingText] string name)
        {
            var ship = ShipList.Ships[name];
            if (!ship.IsInvited(ctx.Member.Id))
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Вы не были приглашены присоединиться к этому кораблю!");
                return;
            }
            
            ship.RemoveMember(ctx.Member.Id);
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Вы успешно отклонили приглашение на корабль **{name}**!");
        }

        [Command("kick")]
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
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Этот участник не является членом вашего корабля!");
                return;
            }

            if (!ship.Members[member.Id].Status)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Используйте команду `!uninvite [DiscordMember]`, чтобы отозвать приглашение!");
                return;
            }
            
            ship.RemoveMember(member.Id);
            await member.RevokeRoleAsync(ctx.Guild.GetRole(ship.Role));
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);

            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Вы выгнали участника **{member.Username}** с корабля **{ship.Name}**!");
            await member.SendMessageAsync($"Капитан **{ctx.Member.Username}** выгнал вас с корабля **{ship.Name}**!");
        }

        [Command("leave"), Aliases("l")]
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
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы должны передать права владельца корабля прежде чем покинуть его!");
                return;
            }

            if (!ShipList.Ships[name].Members[ctx.Member.Id].Status)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Чтобы отклонить приглашение используйте команду `!no`!");
                return;
            }
            
            ShipList.Ships[name].RemoveMember(ctx.Member.Id);
            ShipList.SaveToXML(Bot.BotSettings.ShipXML);
            
            await ctx.Member.RevokeRoleAsync(ctx.Guild.GetRole(ShipList.Ships[name].Role));

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Вы покинули корабль **{name}**!");
        }
    }
}