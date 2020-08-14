using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using SeaOfThieves.Entities;

namespace SeaOfThieves.Commands
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
                    latestPrices.WantedPrice, latestPrices.RoleNamePrice, latestPrices.FriendsPrice);
            }

            switch (name)
            {
                case "color":
                    PriceList.Prices[DateTime.Today].ColorPrice = newPrice;
                    break;
                case "wanted":
                    PriceList.Prices[DateTime.Today].WantedPrice = newPrice;
                    break;
                case "role_rename":
                    PriceList.Prices[DateTime.Today].RoleNamePrice = newPrice;
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
            embed.AddField("Role rename", prices.RoleNamePrice.ToString(), true);
            embed.AddField("Friends", prices.FriendsPrice.ToString(), true);

            await ctx.RespondAsync(embed: embed.Build());
        }
        
        [Command("add")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Add(CommandContext ctx, DiscordMember member, int balance)
        {
            var res = new Donator(member.Id, 0, DateTime.Today, balance);
            var prices = PriceList.Prices[PriceList.GetLastDate(DateTime.Now)];
            var message = $"Спасибо за поддержку нашего сообщества! **Ваш баланс: {balance} ₽.\n" +
                          $"Доступные функции:**\n";
            
            if (balance >= prices.ColorPrice)
            {
                var role = await ctx.Guild.CreateRoleAsync($"{member.Username} Style");
                res.SetRole(role.Id);
                await role.ModifyPositionAsync(ctx.Guild.GetRole(Bot.BotSettings.DonatorSpacerRole).Position - 1);
                await member.GrantRoleAsync(role);
                message += $"• `{Bot.BotSettings.Prefix}donator color hex-код-цвета` — изменяет цвет вашего ника.\n";
            }

            DonatorList.SaveToXML(Bot.BotSettings.DonatorXML);

            if (balance >= prices.WantedPrice)
                message += $"• `{Bot.BotSettings.Prefix}donator roleadd` — выдаёт вам роль `💣☠️WANTED☠️💣`.\n" +
                           $"• `{Bot.BotSettings.Prefix}donator rolerm` — снимает с вас роль `💣☠️WANTED☠️💣`.\n";
            if (balance >= prices.RoleNamePrice)
                message += $"• `{Bot.BotSettings.Prefix}donator rename` — изменяет название вашей роли донатера.\n";
            if (balance >= prices.FriendsPrice)
                message += $"• `{Bot.BotSettings.Prefix}donator friend` — добавляет другу ваш цвет (до 5 друзей).\n" +
                           $"• `{Bot.BotSettings.Prefix}donator unfriend` — убирает у друга ваш цвет.";
            
            await member.SendMessageAsync(message);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно добавлен донатер!");
        }

        [Command("balance")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Balance(CommandContext ctx, DiscordMember member, int newBalance)
        {
            if (!DonatorList.Donators.ContainsKey(member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не является донатером!");
                return;
            }
            var prices = PriceList.Prices[PriceList.GetLastDate(DateTime.Now)];

            var oldBalance = DonatorList.Donators[member.Id].Balance;
            DonatorList.Donators[member.Id].SetBalance(newBalance);
            DonatorList.Donators[member.Id].Date = DateTime.Today;
            DonatorList.SaveToXML(Bot.BotSettings.DonatorXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Вы успешно изменили баланс.");

            var message = $"Ваш баланс был изменён. **Новый баланс: {newBalance} ₽\n" +
                          $"Доступные функции:**\n";

            if (newBalance >= prices.ColorPrice)
                message += $"• `{Bot.BotSettings.Prefix}donator color hex-код-цвета` — изменяет цвет вашего ника.\n";
            if (newBalance >= prices.WantedPrice)
                message += $"• `{Bot.BotSettings.Prefix}donator roleadd` — выдаёт вам роль `💣☠️WANTED☠️💣`.\n" +
                           $"• `{Bot.BotSettings.Prefix}donator rolerm` — снимает с вас роль `💣☠️WANTED☠️💣`.\n";
            if (newBalance >= prices.RoleNamePrice)
                message += $"• `{Bot.BotSettings.Prefix}donator rename` — изменяет название вашей роли донатера.\n";
            if (newBalance >= prices.FriendsPrice)
                message += $"• `{Bot.BotSettings.Prefix}donator friend` — добавляет другу ваш цвет (до 5 друзей).\n" +
                           $"• `{Bot.BotSettings.Prefix}donator unfriend` — убирает у друга ваш цвет.";

            await member.SendMessageAsync(message);
        }

        [Command("remove")]
        [Aliases("rm")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Remove(CommandContext ctx, DiscordMember member)
        {
            if (!DonatorList.Donators.ContainsKey(member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не является донатером!");
                return;
            }

            try
            {
                await member.RevokeRoleAsync(ctx.Guild.GetRole(Bot.BotSettings.DonatorRole),
                    "Donator deletion");
                await ctx.Member.RevokeRoleAsync(ctx.Guild.GetRole(DonatorList.Donators[member.Id].ColorRole));
            }
            catch (NullReferenceException)
            {
                
            }
            DonatorList.Donators[member.Id].Remove();
            DonatorList.SaveToXML(Bot.BotSettings.DonatorXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удалён донатер!");
        }
        
        [Command("color")]
        [Description("Устанавливает донатерский цвет. Формат: 000000")]
        public async Task Color(CommandContext ctx, string color)
        {
            if (!DonatorList.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            var prices = PriceList.Prices[PriceList.GetLastDate(DonatorList.Donators[ctx.Member.Id].Date)];

            if (DonatorList.Donators[ctx.Member.Id].Balance < prices.ColorPrice)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} К сожалению, эта функция недоступна вам из-за низкого баланса.");
                return;
            }

            var discordColor = new DiscordColor(000000);
            try
            {
                discordColor = new DiscordColor(color);
            }
            catch (Exception)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Некорректный формат цвета!");
                return;
            }

            var role = ctx.Guild.GetRole(DonatorList.Donators[ctx.Member.Id].ColorRole);
            await role.ModifyAsync(x => x.Color = discordColor);
            await role.ModifyPositionAsync(ctx.Guild.GetRole(Bot.BotSettings.DonatorSpacerRole).Position - 1);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно изменен цвет донатера!");
        }

        [Command("rename")]
        [Description("Измененяет название роли донатера.")]
        public async Task Rename(CommandContext ctx, [RemainingText] string newName)
        {
            if (!DonatorList.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            var prices = PriceList.Prices[PriceList.GetLastDate(DonatorList.Donators[ctx.Member.Id].Date)];

            if (DonatorList.Donators[ctx.Member.Id].Balance < prices.RoleNamePrice)
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

            var role = ctx.Guild.GetRole(DonatorList.Donators[ctx.Member.Id].ColorRole);
            await role.ModifyAsync(x => x.Name = newName);
            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Успешно изменено название роли донатера на **{newName}**");
        }

        [Command("friend")]
        [Description("Добавляет вашему другу цвет донатера (ваш)")]
        public async Task Friend(CommandContext ctx, DiscordMember member)
        {
            if (!DonatorList.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            var prices = PriceList.Prices[PriceList.GetLastDate(DonatorList.Donators[ctx.Member.Id].Date)];

            if (DonatorList.Donators[ctx.Member.Id].Balance < prices.FriendsPrice)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} К сожалению, эта функция недоступна вам из-за низкого баланса.");
                return;
            }

            if (DonatorList.Donators[ctx.Member.Id].Friends.Count == 5)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы можете добавить только 5 друзей!");
                return;
            }

            DonatorList.Donators[ctx.Member.Id].AddFriend(member.Id);
            await member.GrantRoleAsync(ctx.Guild.GetRole(DonatorList.Donators[ctx.Member.Id].ColorRole));
            DonatorList.SaveToXML(Bot.BotSettings.DonatorXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Вы успешно добавили вашему другу цвет!");
        }

        [Command("unfriend")]
        [Description("Убирает цвет у друга")]
        public async Task Unfriend(CommandContext ctx, DiscordMember member)
        {
            //Убрал
            /*
            if (!DonatorList.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            var prices = PriceList.Prices[PriceList.GetLastDate(DonatorList.Donators[ctx.Member.Id].Date)];

            if (DonatorList.Donators[ctx.Member.Id].Balance < prices.FriendsPrice)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} К сожалению, эта функция недоступна вам из-за низкого баланса.");
                return;
            }
            */

            if (DonatorList.Donators[member.Id].Friends.Contains(ctx.Member.Id))
            {
                DonatorList.Donators[member.Id].RemoveFriend(ctx.Member.Id);
                await ctx.Member.RevokeRoleAsync(ctx.Guild.GetRole(DonatorList.Donators[member.Id].ColorRole));
                await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удален цвет вашего друга!");
            }

            if (DonatorList.Donators[ctx.Member.Id].Friends.Contains(member.Id))
            {
                DonatorList.Donators[ctx.Member.Id].RemoveFriend(member.Id);
                await member.RevokeRoleAsync(ctx.Guild.GetRole(DonatorList.Donators[ctx.Member.Id].ColorRole));
                await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удален цвет у вашего друга!");
            }

            DonatorList.SaveToXML(Bot.BotSettings.DonatorXML);
        }

        [Command("friends")]
        [Description("Выводит список друзей")]
        public async Task Friends(CommandContext ctx)
        {
            if (!DonatorList.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            var prices = PriceList.Prices[PriceList.GetLastDate(DonatorList.Donators[ctx.Member.Id].Date)];

            if (DonatorList.Donators[ctx.Member.Id].Balance < prices.FriendsPrice)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} К сожалению, эта функция недоступна вам из-за низкого баланса.");
                return;
            }

            var i = 0;
            var friendsMsg = "";
            foreach (var friend in DonatorList.Donators[ctx.Member.Id].Friends)
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
            if (!DonatorList.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            var prices = PriceList.Prices[PriceList.GetLastDate(DonatorList.Donators[ctx.Member.Id].Date)];

            if (DonatorList.Donators[ctx.Member.Id].Balance < prices.WantedPrice)
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
            if (!DonatorList.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            var prices = PriceList.Prices[PriceList.GetLastDate(DonatorList.Donators[ctx.Member.Id].Date)];

            if (DonatorList.Donators[ctx.Member.Id].Balance < prices.WantedPrice)
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
            if (!DonatorList.Donators.ContainsKey(member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Участник не является донатером!");
                return;
            }

            DonatorList.Donators[member.Id].UpdateHidden(hidden);
            DonatorList.SaveToXML(Bot.BotSettings.DonatorXML);
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
            foreach (var donator in DonatorList.Donators.Values)
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
    }
}
