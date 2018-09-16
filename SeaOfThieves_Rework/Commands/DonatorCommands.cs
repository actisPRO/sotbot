using System;
using System.Runtime.Serialization.Formatters;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using SeaOfThieves.Entities;

namespace SeaOfThieves.Commands
{
    public class DonatorCommands
    {
        [Command("donatoradd")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task DonatorAdd(CommandContext ctx, DiscordMember member, int balance)
        {
            var role = await ctx.Guild.CreateRoleAsync($"{member.Username} Style");
            await ctx.Guild.UpdateRolePositionAsync(role, ctx.Guild.GetRole(Bot.BotSettings.BotRole).Position - 1);

            var res = new Donator(member.Id, role.Id, balance);
            DonatorList.SaveToXML(Bot.BotSettings.DonatorXML);

            var over100Message = ".";
            if (balance >= 100)
            {
                over100Message = ", `!droleadd` для выдачи роли Wanted, `!drolerm` для снятия роли Wanted.";
            }

            await member.GrantRoleAsync(role);
            await member.SendMessageAsync(
                $"Администратор **{ctx.Member.Username}** добавил вас в качестве донатера. Ваш баланс: **{balance} рублей**. Используйте команду " +
                $"`!dcolor код_цвета` для изменения цвета{over100Message}");
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно добавлен донатер!");
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

            DiscordColor discordColor = new DiscordColor(000000);
            try
            {
                discordColor = new DiscordColor(color);
            }
            catch (Exception e)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Некорректный формат цвета!");
                return;
            }

            var role = ctx.Guild.GetRole(DonatorList.Donators[ctx.Member.Id].ColorRole);
            await ctx.Guild.UpdateRoleAsync(role, color: discordColor);
            await ctx.Guild.UpdateRolePositionAsync(role, ctx.Guild.GetRole(Bot.BotSettings.BotRole).Position - 1);
            
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно изменен цвет донатера!");
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
    }
}