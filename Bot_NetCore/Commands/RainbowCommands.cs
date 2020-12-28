using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace Bot_NetCore.Commands
{
    [Group("rainbow")]
    [Description("Управление радужной ролью")]
    [Aliases("r")]
    public class RainbowCommands : BaseCommandModule
    {
        [Command("add")]
        [Description("Добавляет радужную роль")]
        public async Task Add(CommandContext ctx)
        {
            if (!Bot.BotSettings.RainbowPublic || !Bot.BotSettings.RainbowEnabled)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} В данный момент нельзя добавить себе эту роль.");
                return;
            }

            var role = ctx.Guild.GetRole(Bot.BotSettings.RainbowRole);
            await ctx.Member.GrantRoleAsync(role);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Теперь у тебя есть радужная роль!");
        }

        [Command("remove")]
        [Description("Удаляет радужную роль")]
        public async Task Remove(CommandContext ctx)
        {
            var role = ctx.Guild.GetRole(Bot.BotSettings.RainbowRole);
            await ctx.Member.RevokeRoleAsync(role);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Радужная роль убрана.");
        }
    }
}