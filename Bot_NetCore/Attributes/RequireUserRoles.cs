using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace Bot_NetCore.Attributes
{
    /// <summary>
    /// Defines that usage of this command is restricted to members with specified role or admin permission. Note that it's much preferred to restrict access using <see cref="RequirePermissionsAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RequireUserRolesAttribute : CheckBaseAttribute
    {
        /// <summary>
        /// Gets the id of the role required to execute this command.
        /// </summary>
        public IReadOnlyList<ulong> RoleIDs { get; }

        /// <summary>
        /// Gets the role checking mode. Refer to <see cref="RoleCheckMode"/> for more information.
        /// </summary>
        public RoleCheckMode CheckMode { get; }

        /// <summary>
        /// Defines that usage of this command is restricted to members with specified role or admin permission. Note that it's much preferred to restrict access using <see cref="RequirePermissionsAttribute"/>.
        /// </summary>
        /// <param name="checkMode">Role checking mode.</param>
        /// <param name="roleIDs">IDs of the role to be verified by this check.</param>
        public RequireUserRolesAttribute(RoleCheckMode checkMode, params ulong[] roleIDs)
        {
            this.CheckMode = checkMode;
            this.RoleIDs = new ReadOnlyCollection<ulong>(roleIDs);
        }

        public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            if (ctx.Guild == null || ctx.Member == null)
                return Task.FromResult(false);

            if (ctx.Member.Roles.Any(x => x.CheckPermission(DSharpPlus.Permissions.Administrator) == DSharpPlus.PermissionLevel.Allowed))
                return Task.FromResult(true);

            var rns = ctx.Member.Roles.Select(xr => xr.Id);
            var rnc = rns.Count();
            var ins = rns.Intersect(this.RoleIDs);
            var inc = ins.Count();

            switch (this.CheckMode)
            {
                case RoleCheckMode.All:
                    return Task.FromResult(this.RoleIDs.Count == inc);

                case RoleCheckMode.SpecifiedOnly:
                    return Task.FromResult(rnc == inc);

                case RoleCheckMode.None:
                    return Task.FromResult(inc == 0);

                case RoleCheckMode.Any:
                default:
                    return Task.FromResult(inc > 0);
            }
        }
    }

}
