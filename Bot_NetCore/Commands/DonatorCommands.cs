using System;
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
    public class DonatorCommands
    {
        [Command("donatoradd")]
        [Aliases("dadd")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task DonatorAdd(CommandContext ctx, DiscordMember member, int balance)
        {
            var res = new Donator(member.Id, 0, balance);
            if (balance >= 50)
            {
                var role = await ctx.Guild.CreateRoleAsync($"{member.Username} Style");
                res.SetRole(role.Id);
                await ctx.Guild.UpdateRolePositionAsync(role, ctx.Guild.GetRole(Bot.BotSettings.DonatorSpacerRole).Position - 1);
                await member.GrantRoleAsync(role);
            }

            DonatorList.SaveToXML(Bot.BotSettings.DonatorXML);

            var over100Message = ".";
            if (balance >= 100)
                over100Message = ", `!droleadd` для выдачи роли Wanted, `!drolerm` для снятия роли Wanted";

            var over250Message = ".";
            if (balance >= 250)
                over250Message =
                    ", `!drename` для переименования своей роли, `!dfriend` для того чтобы выдать свой цвет другу.";

            var over50Message = "";
            if (balance >= 50)
                over50Message = "Используйте команду " +
                                $"`!dcolor код_цвета` для изменения цвета{over100Message}{over250Message}";
            await member.SendMessageAsync(
                $"Администратор **{ctx.Member.Username}** добавил вас в качестве донатера. Ваш баланс: **{balance} рублей**. {over50Message}");
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно добавлен донатер!");
        }

        [Command("dbalance")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task DBalance(CommandContext ctx, DiscordMember member, int newBalance)
        {
            if (!DonatorList.Donators.ContainsKey(member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не является донатером!");
                return;
            }

            var oldBalance = DonatorList.Donators[member.Id].Balance;
            DonatorList.Donators[member.Id].SetBalance(newBalance);
            DonatorList.SaveToXML(Bot.BotSettings.DonatorXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Вы успешно изменили баланс.");
            await member.SendMessageAsync(
                $"Администратор **{ctx.Member.Username}** изменил ваш баланс. Ваш новый баланс: **{newBalance}** рублей.");

            if (oldBalance < 50)
            {
                if (newBalance >= 50) await member.SendMessageAsync("Используйте `!dcolor` для изменения цвета ника.");

                if (newBalance >= 100)
                    await member.SendMessageAsync(
                        "Используйте `!droleadd` для выдачи роли Wanted. `!drolerm` для того чтобы убрать её.");

                if (newBalance >= 250)
                    await member.SendMessageAsync(
                        "Используйте `!drename` для переименования роли донатера. `!dfriend` для выдачи своему другу цвета донатера.");
            }
            else if (oldBalance < 100)
            {
                if (newBalance < 50) await member.SendMessageAsync("Вам стал недоступен функционал `!dcolor`.");

                if (newBalance >= 100)
                    await member.SendMessageAsync(
                        "Используйте `!droleadd` для выдачи роли Wanted. `!drolerm` для того чтобы убрать её.");

                if (newBalance >= 250)
                    await member.SendMessageAsync(
                        "Используйте `!drename` для переименования роли донатера. `!dfriend` для выдачи своему другу цвета донатера.");
            }
            else if (oldBalance < 250)
            {
                if (newBalance < 50) await member.SendMessageAsync("Вам стал недоступен функционал `!dcolor`.");

                if (newBalance < 100)
                    await member.SendMessageAsync(
                        "Вам стал недоступен функционал `!droleadd`, `!drolerm`.");

                if (newBalance >= 250)
                    await member.SendMessageAsync(
                        "Используйте `!drename` для переименования роли донатера. `!dfriend` для выдачи своему другу цвета донатера.");
            }
            else
            {
                if (newBalance < 50) await member.SendMessageAsync("Вам стал недоступен функционал `!dcolor`.");

                if (newBalance < 100)
                    await member.SendMessageAsync(
                        "Вам стал недоступен функционал `!droleadd`, `!drolerm`.");

                if (newBalance < 250)
                    await member.SendMessageAsync(
                        "Вам стал недоступен функционал `!drename`, `!dfriend`.");
            }
        }

        [Command("donatorrm")]
        [Aliases("drm")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task DonatorRemove(CommandContext ctx, DiscordMember member)
        {
            if (!DonatorList.Donators.ContainsKey(member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не является донатером!");
                return;
            }

            try
            {
                await ctx.Guild.RevokeRoleAsync(member, ctx.Guild.GetRole(Bot.BotSettings.DonatorRole),
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
        
        [Command("dcolor")]
        [Description("Устанавливает донатерский цвет. Формат: 000000")]
        public async Task DColor(CommandContext ctx, string color)
        {
            if (!DonatorList.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            if (DonatorList.Donators[ctx.Member.Id].Balance < 50)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Ваш баланс меньше 50 рублей. " +
                                       "Если вы донатили до *17.09.2018*, обратитесь к **Actis** для смены цвета.");
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
            await ctx.Guild.UpdateRoleAsync(role, color: discordColor);
            await ctx.Guild.UpdateRolePositionAsync(role, ctx.Guild.GetRole(Bot.BotSettings.DonatorSpacerRole).Position - 1);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно изменен цвет донатера!");
        }

        [Command("drename")]
        [Description("Измененяет название роли донатера.")]
        public async Task DRename(CommandContext ctx, [RemainingText] string newName)
        {
            if (!DonatorList.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            if (DonatorList.Donators[ctx.Member.Id].Balance < 250)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Ваш баланс меньше 250 рублей!");
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

            await ctx.Guild.UpdateRoleAsync
                (ctx.Guild.GetRole(DonatorList.Donators[ctx.Member.Id].ColorRole), newName);
            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Успешно изменено название роли донатера на **{newName}**");
        }

        [Command("dfriend")]
        [Description("Добавляет вашему другу цвет донатера (ваш)")]
        public async Task DFriend(CommandContext ctx, DiscordMember member)
        {
            if (!DonatorList.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            if (DonatorList.Donators[ctx.Member.Id].Balance < 250)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Ваш баланс меньше 250 рублей!");
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

        [Command("dunfriend")]
        [Description("Убирает цвет у друга")]
        public async Task DUnFriend(CommandContext ctx, DiscordMember member)
        {
            if (!DonatorList.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            if (DonatorList.Donators[ctx.Member.Id].Balance < 250)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Ваш баланс меньше 250 рублей!");
                return;
            }

            await member.RevokeRoleAsync(ctx.Guild.GetRole(DonatorList.Donators[ctx.Member.Id].ColorRole));

            DonatorList.Donators[ctx.Member.Id].RemoveFriend(member.Id);
            DonatorList.SaveToXML(Bot.BotSettings.DonatorXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удален цвет!");
        }

        [Command("droleadd")]
        [Description("Выдает роль донатера.")]
        public async Task DRoleAdd(CommandContext ctx)
        {
            if (!DonatorList.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            if (DonatorList.Donators[ctx.Member.Id].Balance < 100)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Ваш баланс меньше 100 рублей!");
                return;
            }

            await ctx.Member.GrantRoleAsync(ctx.Guild.GetRole(Bot.BotSettings.DonatorRole));
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно выдана роль донатера!");
        }

        [Command("drolerm")]
        [Description("Убирает роль донатера.")]
        public async Task DRoleRm(CommandContext ctx)
        {
            if (!DonatorList.Donators.ContainsKey(ctx.Member.Id))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не являетесь донатером!");
                return;
            }

            if (DonatorList.Donators[ctx.Member.Id].Balance < 100)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Ваш баланс меньше 100 рублей!");
                return;
            }

            await ctx.Member.RevokeRoleAsync(ctx.Guild.GetRole(Bot.BotSettings.DonatorRole));
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно снята роль донатера!");
        }

        [Command("sethidden")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
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
    }
}