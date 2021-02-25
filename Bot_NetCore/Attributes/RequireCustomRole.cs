using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace Bot_NetCore.Attributes
{
    /// <summary>
    /// Defines that usage of this command is restricted to members with specified permissions. This check also verifies that the bot has the same permissions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class RequireCustomRoleAttribute : CheckBaseAttribute
    {
        /// <summary>
        /// Gets the role required by this attribute
        /// </summary>
        public RoleType SpecifiedRole { get; }


        /// <summary>
        /// Defines that usage of this command is restricted to members with specified permissions. This check also verifies that the bot has the same permissions.
        /// </summary>
        /// <param name="roleType">Role required to execute this command.</param>
        public RequireCustomRoleAttribute(RoleType roleType)
        {
            this.SpecifiedRole = roleType;
        }

        public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            if (ctx.Guild == null)
                return await Task.FromResult(false);

            var usr = ctx.Member;
            if (usr == null)
                return await Task.FromResult(false);

            //Adming and mods goes here, and always be checked before other ones
            if ((ctx.Channel.PermissionsFor(usr) & Permissions.Administrator) != 0)
                return await Task.FromResult(true);

            if (SpecifiedRole != RoleType.Admin && Bot.IsModerator(ctx.Member))
                return await Task.FromResult(true);

            switch (SpecifiedRole)
            {
                case RoleType.Helper:
                    if (ctx.Member.Roles.Any(x => x.Id == Bot.BotSettings.HelperRole))
                        return await Task.FromResult(true);
                    return await Task.FromResult(false);

                case RoleType.FleetCaptain:
                    if (ctx.Member.Roles.Any(x => x.Id == Bot.BotSettings.FleetCaptainRole))
                        return await Task.FromResult(true);
                    return await Task.FromResult(false);

                default:
                    return await Task.FromResult(false);
            }
        }
    }

    public enum RoleType
    {
        Admin,
        Moderator,
        Helper,
        FleetCaptain
    }
}