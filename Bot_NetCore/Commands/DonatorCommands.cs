﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;

namespace Bot_NetCore.Commands
{
    [Group("donator")]
    [Aliases("d")]
    [Description("Команды доната.")]
    public class DonatorCommands : BaseCommandModule
    {
        [Command("setprice")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task SetPrice(CommandContext ctx, string name, int newPrice)
        {
            if (!PriceList.Prices.ContainsKey(DateTime.Today))
            {
                var latestPrices = PriceList.Prices[PriceList.GetLastDate(DateTime.Now)];
                PriceList.Prices[DateTime.Today] = new DateServices(DateTime.Today, latestPrices.ColorPrice,
                    latestPrices.WantedPrice, latestPrices.RolePrice, latestPrices.FriendsPrice);
            }

            switch (name)
            {
                case "color":
                    PriceList.Prices[DateTime.Today].ColorPrice = newPrice;
                    break;
                case "wanted":
                    PriceList.Prices[DateTime.Today].WantedPrice = newPrice;
                    break;
                case "role":
                    PriceList.Prices[DateTime.Today].RolePrice = newPrice;
                    break;
                case "friends":
                    PriceList.Prices[DateTime.Today].FriendsPrice = newPrice;
                    break;
                default:
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Неправильно указано имя услуги!");
                    return;
            }

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно изменена цена услуги!");

            PriceList.SaveToXML(Bot.BotSettings.PriceListXML);
        }

        [Command("getprices")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task GetPrices(CommandContext ctx)
        {
            var lastDate = PriceList.GetLastDate(DateTime.Now);
            var prices = PriceList.Prices[lastDate];

            var embed = new DiscordEmbedBuilder();
            embed.Color = DiscordColor.Goldenrod;
            embed.Title = "Текущие цены на донат";
            embed.AddField("Color", prices.ColorPrice.ToString(), true);
            embed.AddField("Wanted", prices.WantedPrice.ToString(), true);
            embed.AddField("Role", prices.RolePrice.ToString(), true);
            embed.AddField("Friends", prices.FriendsPrice.ToString(), true);

            await ctx.RespondAsync(embed: embed.Build());
        }

        [Command("add")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Add(CommandContext ctx, DiscordMember member, int balance)
        {
            if (Donator.Donators.ContainsKey(member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь уже является донатером!");
                return;
            }
            
            var donator = new Donator(member.Id, balance, 0, DateTime.Now, new List<ulong>(), false);
            var prices = PriceList.Prices[PriceList.GetLastDate(DateTime.Now)];
            var message = $"Спасибо за поддержку нашего сообщества! **Ваш баланс: {balance} ₽.\n" +
                          $"Доступные функции:**\n";

            if (balance >= prices.ColorPrice && balance < prices.RolePrice)
            {
                message += $"• `{Bot.BotSettings.Prefix}d color цвет (из списка)` — изменяет цвет вашего ника.\n";
                message += $"• `{Bot.BotSettings.Prefix}d colors` — выводит список доступных цветов.\n";
            }

            if (balance >= prices.RolePrice)
            {
                message += $"• `{Bot.BotSettings.Prefix}d color hex-код цвета` — изменяет цвет вашего ника.\n";
                message += $"• `{Bot.BotSettings.Prefix}d rename` — изменяет название вашей роли донатера.\n";

                var role = await ctx.Guild.CreateRoleAsync($"{member.DisplayName} Style");
                await role.ModifyPositionAsync(ctx.Guild.GetRole(Bot.BotSettings.DonatorSpacerRole).Position - 1);
                await member.GrantRoleAsync(role);
                donator.PrivateRole = role.Id;
            }

            if (balance >= prices.WantedPrice)
                message += $"• `{Bot.BotSettings.Prefix}d roleadd` — выдаёт вам роль `💣☠️WANTED☠️💣`.\n" +
                           $"• `{Bot.BotSettings.Prefix}d rolerm` — снимает с вас роль `💣☠️WANTED☠️💣`.\n";
            
            if (balance >= prices.FriendsPrice)
                message += $"• `{Bot.BotSettings.Prefix}d friend` — добавляет другу ваш цвет (до 5 друзей).\n" +
                           $"• `{Bot.BotSettings.Prefix}d unfriend` — убирает у друга ваш цвет.";

            Donator.Save(Bot.BotSettings.DonatorXML);
            await member.SendMessageAsync(message);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно добавлен донатер!");
        }

        [Command("balance")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Balance(CommandContext ctx, DiscordMember member, int newBalance)
        {
            if (!Donator.Donators.ContainsKey(member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не является донатером!");
                return;
            }
            var prices = PriceList.Prices[PriceList.GetLastDate(DateTime.Now)];
            var donator = Donator.Donators[member.Id];

            var oldBalance = donator.Balance;
            donator.Balance = newBalance;
            donator.Date = DateTime.Today;

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Вы успешно изменили баланс.");

            var message = $"Ваш баланс был изменён. **Новый баланс: {newBalance} ₽\n" +
                          $"Доступные функции:**\n";

            if (newBalance >= prices.ColorPrice && newBalance < prices.RolePrice)
                message += $"• `{Bot.BotSettings.Prefix}donator color цвет (из списка)` — изменяет цвет вашего ника.\n";
            if (donator.PrivateRole != 0)
            {
                await ctx.Guild.GetRole(donator.PrivateRole).DeleteAsync();
                donator.PrivateRole = 0;
            }

            if (newBalance >= prices.RolePrice)
                message += $"• `{Bot.BotSettings.Prefix}donator color hex-код цвета` — изменяет цвет вашего ника.\n" +
                           $"• `{Bot.BotSettings.Prefix}donator rename` — изменяет название вашей роли донатера.\n";
            if (oldBalance < prices.RolePrice)
            {
                var role = await ctx.Guild.CreateRoleAsync($"{member.Username} Style");
                await role.ModifyPositionAsync(ctx.Guild.GetRole(Bot.BotSettings.DonatorSpacerRole).Position - 1);
                await member.GrantRoleAsync(role);
                donator.PrivateRole = role.Id;
            }
            
            if (newBalance >= prices.WantedPrice)
                message += $"• `{Bot.BotSettings.Prefix}donator roleadd` — выдаёт вам роль `💣☠️WANTED☠️💣`.\n" +
                           $"• `{Bot.BotSettings.Prefix}donator rolerm` — снимает с вас роль `💣☠️WANTED☠️💣`.\n";
            if (newBalance >= prices.FriendsPrice)
                message += $"• `{Bot.BotSettings.Prefix}donator friend` — добавляет другу ваш цвет (до 5 друзей).\n" +
                           $"• `{Bot.BotSettings.Prefix}donator unfriend` — убирает у друга ваш цвет.";

            Donator.Save(Bot.BotSettings.DonatorXML);
            await member.SendMessageAsync(message);
        }

        [Command("remove")]
        [Aliases("rm")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Remove(CommandContext ctx, DiscordMember member)
        {
            if (!Donator.Donators.ContainsKey(member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не является донатером!");
                return;
            }

            var donator = Donator.Donators[member.Id];

            if (member.Roles.Contains(ctx.Guild.GetRole(Bot.BotSettings.DonatorRole)))
                await member.RevokeRoleAsync(ctx.Guild.GetRole(Bot.BotSettings.DonatorRole),
                        "Donator deletion");

            if (donator.PrivateRole != 0)
                await ctx.Guild.GetRole(donator.PrivateRole).DeleteAsync("Donator deletion");

            Donator.Donators.Remove(member.Id);
            Donator.Save(Bot.BotSettings.DonatorXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удалён донатер!");
        }

        [Command("color")]
        [Description("Устанавливает донатерский цвет. Формат: #000000 для владельцев приватных ролей, либо название цвета.")]
        public async Task Color(CommandContext ctx, [RemainingText] string color)
        {
            if (!Donator.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            var prices = PriceList.Prices[PriceList.GetLastDate(Donator.Donators[ctx.Member.Id].Date)];
            var donator = Donator.Donators[ctx.Member.Id];

            if (donator.Balance < prices.ColorPrice)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} К сожалению, эта функция недоступна вам из-за низкого баланса.");
            }
            else if (donator.Balance >= prices.ColorPrice && donator.Balance < prices.RolePrice)
            {
                var avaliableRoles = GetColorRolesIds(ctx.Guild);
                if (avaliableRoles == null)
                {
                    await ctx.RespondAsync(
                        $"{Bot.BotSettings.ErrorEmoji} Не заданы цветные роли! Обратитесь к администратору для решения проблемы.");
                    return;
                }

                foreach (var role in avaliableRoles)
                {
                    if (role.Name.ToLower() == color.ToLower())
                    {
                        foreach (var memberRole in ctx.Member.Roles)
                            if (avaliableRoles.Contains(memberRole))
                            {
                                await ctx.Member.RevokeRoleAsync(memberRole);
                                break;
                            }
                                
                        await ctx.Member.GrantRoleAsync(role);
                        await role.ModifyPositionAsync(ctx.Guild.GetRole(Bot.BotSettings.ColorSpacerRole).Position - 1); // костыльное решение невозможности нормально перенести роли
                        await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно изменён цвет!");
                        return;
                    }
                }

                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Неправильно задано имя цвета! Используйте `!d colors`, чтобы получить список доступых цветов. ");
            }
            else if (donator.Balance >= prices.RolePrice)
            {
                var role = ctx.Guild.GetRole(donator.PrivateRole);
                await role.ModifyAsync(x => x.Color = new DiscordColor(color));
                await role.ModifyPositionAsync(ctx.Guild.GetRole(Bot.BotSettings.DonatorSpacerRole).Position - 1);

                await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно изменён цвет!");
            }
        }

        [Command("colors")] //TODO: доделать
        [Description("Выводит список доступных цветов.")]
        public async Task Colors(CommandContext ctx)
        {
            if (!Donator.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            if (!File.Exists("generated/colors.jpeg"))
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Отсутствует файл colors.jpeg, обратитесь к администратору!");
                return;
            }

            await ctx.RespondWithFileAsync("generated/colors.jpeg",
                "Используйте `!d color название цвета`, чтобы получить цвет.");
        }

        [Command("generatecolors")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task GenerateColors(CommandContext ctx)
        {
            if (File.Exists("generated/color_roles.txt"))
                using (var fs = new FileStream("generated/color_roles.txt", FileMode.Open))
                using (var sr = new StreamReader(fs))
                {
                    var role = await sr.ReadLineAsync();
                    while (role != null)
                    {
                        await ctx.Guild.GetRole(Convert.ToUInt64(role)).DeleteAsync();
                        role = await sr.ReadLineAsync();
                    }
                }

            var colors = new Dictionary<string, DiscordColor>
            {
                ["Red"] = DiscordColor.Red,
                ["Dark red"] = DiscordColor.DarkRed,
                ["Orange"] = DiscordColor.Orange,
                ["Lilac"] = DiscordColor.Lilac,
                ["Yellow"] = DiscordColor.Yellow,
                ["Gold"] = DiscordColor.Gold,
                ["Blue"] = DiscordColor.Blue,
                ["Magenta"] = DiscordColor.Magenta,
                ["Aquamarine"] = DiscordColor.Aquamarine,
                ["Blurple"] = DiscordColor.Blurple,
                ["Cyan"] = DiscordColor.Cyan,
                ["Purple"] = DiscordColor.Purple,
                ["Green"] = DiscordColor.Green,
                ["Azure"] = DiscordColor.Azure,
                ["Brown"] = DiscordColor.Brown,
                ["White"] = DiscordColor.White,
            };
            
            var roles = new List<ulong>();
            using (var fs = File.Create("generated/color_roles.txt"))
                using (var sw = new StreamWriter(fs))
                    foreach (var color in colors)
                    {
                        var role = await ctx.Guild.CreateRoleAsync(color.Key, color: color.Value);
                        roles.Add(role.Id);
                        await sw.WriteLineAsync(role.Id.ToString());
                    }

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно сгенерированы цветные роли!");
        }

        [Command("rename")]
        [Description("Измененяет название роли донатера.")]
        public async Task Rename(CommandContext ctx, [RemainingText] string newName)
        {
            if (!Donator.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            var prices = PriceList.Prices[PriceList.GetLastDate(Donator.Donators[ctx.Member.Id].Date)];

            if (Donator.Donators[ctx.Member.Id].Balance < prices.RolePrice)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} К сожалению, эта функция недоступна вам из-за низкого баланса.");
                return;
            }
            //Проверка названия на копирование админ ролей
            try
            {
                if (Bot.GetMultiplySettingsSeparated(Bot.BotSettings.AdminRoles)
                    .Any(x => ctx.Guild.GetRole(x).Name.Equals(newName, StringComparison.InvariantCultureIgnoreCase)))
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Недопустимое название роли **{newName}**");
                    return;
                }
            }
            catch (NullReferenceException ex)
            {
                //Не находит на сервере одну из админ ролей
                throw new NullReferenceException("Impossible to find one of admin roles. Check configuration", ex);
            }

            var role = ctx.Guild.GetRole(Donator.Donators[ctx.Member.Id].PrivateRole);
            await role.ModifyAsync(x => x.Name = newName);
            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Успешно изменено название роли донатера на **{newName}**");
        }

        [Command("friend")]
        [Description("Добавляет вашему другу цвет донатера (ваш)")]
        public async Task Friend(CommandContext ctx, DiscordMember member)
        {
            if (!Donator.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            var prices = PriceList.Prices[PriceList.GetLastDate(Donator.Donators[ctx.Member.Id].Date)];

            if (Donator.Donators[ctx.Member.Id].Balance < prices.FriendsPrice)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} К сожалению, эта функция недоступна вам из-за низкого баланса.");
                return;
            }

            if (Donator.Donators[ctx.Member.Id].Friends.Count == 5)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы можете добавить только 5 друзей!");
                return;
            }

            Donator.Donators[ctx.Member.Id].Friends.Add(member.Id);
            await member.GrantRoleAsync(ctx.Guild.GetRole(Donator.Donators[ctx.Member.Id].PrivateRole));
            Donator.Save(Bot.BotSettings.DonatorXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Вы успешно добавили вашему другу цвет!");
        }

        [Command("unfriend")]
        [Description("Убирает цвет у друга")]
        public async Task Unfriend(CommandContext ctx, DiscordMember member)
        {
            //Удаление роли которую дали
            if (Donator.Donators.ContainsKey(member.Id) &&
                Donator.Donators[member.Id].Friends.Contains(ctx.Member.Id))
            {
                Donator.Donators[member.Id].Friends.Remove(ctx.Member.Id);
                await ctx.Member.RevokeRoleAsync(ctx.Guild.GetRole(Donator.Donators[member.Id].PrivateRole));
                await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удален цвет вашего друга!");
            }

            //Удаление своей роли
            if (Donator.Donators.ContainsKey(ctx.Member.Id) &&
                Donator.Donators[ctx.Member.Id].Friends.Contains(member.Id))
            {
                Donator.Donators[ctx.Member.Id].Friends.Remove(member.Id);
                await member.RevokeRoleAsync(ctx.Guild.GetRole(Donator.Donators[ctx.Member.Id].PrivateRole));
                await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удален цвет у вашего друга!");
            }

            Donator.Save(Bot.BotSettings.DonatorXML);
        }

        [Command("friends")]
        [Description("Выводит список друзей")]
        public async Task Friends(CommandContext ctx)
        {
            if (!Donator.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            var prices = PriceList.Prices[PriceList.GetLastDate(Donator.Donators[ctx.Member.Id].Date)];

            if (Donator.Donators[ctx.Member.Id].Balance < prices.FriendsPrice)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} К сожалению, эта функция недоступна вам из-за низкого баланса.");
                return;
            }

            var i = 0;
            var friendsMsg = "";
            foreach (var friend in Donator.Donators[ctx.Member.Id].Friends)
            {
                DiscordMember discordMember = null;
                try
                {
                    discordMember = await ctx.Guild.GetMemberAsync(friend);
                }
                catch (NotFoundException)
                {
                    continue;
                }
                i++;
                friendsMsg += $"**{i}**. {discordMember.DisplayName}#{discordMember.Discriminator} \n";
            }
            await ctx.RespondAsync("**Список друзей с вашей ролью**\n\n" +
                                   $"{friendsMsg}");
        }

        [Command("roleadd")]
        [Description("Выдает роль донатера.")]
        public async Task RoleAdd(CommandContext ctx)
        {
            if (!Donator.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            var prices = PriceList.Prices[PriceList.GetLastDate(Donator.Donators[ctx.Member.Id].Date)];

            if (Donator.Donators[ctx.Member.Id].Balance < prices.WantedPrice)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} К сожалению, эта функция недоступна вам из-за низкого баланса.");
                return;
            }

            await ctx.Member.GrantRoleAsync(ctx.Guild.GetRole(Bot.BotSettings.DonatorRole));
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдана роль донатера!");
        }

        [Command("rolerm")]
        [Description("Убирает роль донатера.")]
        public async Task RoleRemove(CommandContext ctx)
        {
            if (!Donator.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            var prices = PriceList.Prices[PriceList.GetLastDate(Donator.Donators[ctx.Member.Id].Date)];

            if (Donator.Donators[ctx.Member.Id].Balance < prices.WantedPrice)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} К сожалению, эта функция недоступна вам из-за низкого баланса.");
                return;
            }

            await ctx.Member.RevokeRoleAsync(ctx.Guild.GetRole(Bot.BotSettings.DonatorRole));
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно снята роль донатера!");
        }

        [Command("sethidden")]
        [Description("Скрывает пользователя в списке донатеров")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task SetHidden(CommandContext ctx, DiscordMember member, bool hidden = true)
        {
            if (!Donator.Donators.ContainsKey(member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Участник не является донатером!");
                return;
            }

            Donator.Donators[member.Id].Hidden = hidden;
            Donator.Save(Bot.BotSettings.DonatorXML);
        }

        [Command("genlist")]
        [Description("Генерирует список донатеров")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task GenerateList(CommandContext ctx)
        {
            var channel = ctx.Guild.GetChannel(Bot.BotSettings.DonatorChannel);

            try
            {
                await channel.DeleteMessagesAsync(await channel.GetMessagesAsync(100));
                await channel.DeleteMessageAsync(await channel.GetMessageAsync(channel.LastMessageId));
            }
            catch (ArgumentException)
            {
                // выбрасывается, если нет сообщений в канале
            }

            var donators = new Dictionary<ulong, double>(); //список донатеров, который будем сортировать
            foreach (var donator in Donator.Donators.Values)
                if (!donator.Hidden)
                    donators.Add(donator.Member, donator.Balance);

            var ordered = donators.OrderBy(x => -x.Value);

            int position = 0, balance = int.MaxValue, str = 1;
            var message = "";

            foreach (var el in ordered)
            {
                if (str % 10 == 0)
                {
                    var sendedMessage = await channel.SendMessageAsync(message);
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
                    message += $"**{position}.** {user.Username}#{user.Discriminator} — {el.Value}₽\n";
                    ++str;
                }
                catch (NotFoundException)
                {
                }
            }

            if (str % 10 != 0)
            {
                var sendedMessage = await channel.SendMessageAsync(message);
            }
        }

        private List<DiscordRole> GetColorRolesIds(DiscordGuild guild)
        {
            if (!File.Exists("generated/color_roles.txt"))
                return null;
            
            var colorRoles = new List<DiscordRole>();
            using (var fs = new FileStream("generated/color_roles.txt", FileMode.Open))
            using (var sr = new StreamReader(fs))
            {
                var roleId = sr.ReadLine();
                while (roleId != null)
                {
                    colorRoles.Add(guild.GetRole(Convert.ToUInt64(roleId)));
                    roleId = sr.ReadLine();
                }
            }

            return colorRoles;
        }
    }
}
