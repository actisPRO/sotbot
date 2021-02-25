using System;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;

namespace Bot_NetCore.Attributes
{
    /// <summary>
    /// Defines that usage of this command is restricted to members with specified permissions. This check also verifies that the bot has the same permissions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class RequireDirectMessageAttribute : CheckBaseAttribute
    {
        /// <summary>
        /// Defines that this command is only usable within a direct message channel.
        /// </summary>
        public RequireDirectMessageAttribute()
        { }

        public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
            => Task.FromResult(ctx.Guild == null );
    }
}