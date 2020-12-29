using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;

namespace RainbowBot
{
    public class Commands : BaseCommandModule
    {
        private static List<ulong> WhitelistRoles = new List<ulong>
        {
            406849001611067393, // admins
            514282258958385152, // mods
            474369537505099778  // caps
        };
        
        [Command("add")]
        [Description("Добавляет радужную роль")]
        public async Task Add(CommandContext ctx)
        {
            var roles = from DiscordRole memberRole in ctx.Member.Roles
                where WhitelistRoles.Contains(memberRole.Id)
                select memberRole.Id;
            
            if ((!Bot.BotSettings.IsPublic || !Bot.BotSettings.IsEnabled) &&
                !roles.Any())
            {
                await ctx.RespondAsync($":no_entry: В данный момент нельзя добавить себе эту роль.");
                return;
            }

            var role = ctx.Guild.GetRole(Bot.BotSettings.RoleId);
            await ctx.Member.GrantRoleAsync(role);
            await ctx.RespondAsync($":white_check_mark: Теперь у тебя есть радужная роль!");
        }

        [Command("remove")]
        [Description("Удаляет радужную роль")]
        public async Task Remove(CommandContext ctx)
        {
            var role = ctx.Guild.GetRole(Bot.BotSettings.RoleId);
            await ctx.Member.RevokeRoleAsync(role);
            await ctx.RespondAsync($":white_check_mark: Радужная роль убрана.");
        }
    }
}