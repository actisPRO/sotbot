using System.Collections.Generic;
using System.Threading.Tasks;
using DSharpPlus.Entities;

namespace Bot_NetCore.Misc;

public static class Invites
{
    /// <summary>
    ///     Словарь, содержащий в качестве ключа Id канала, а в качестве значения - приглашение.
    /// </summary>
    public static Dictionary<ulong, string> LfgInvites = new();
    
    public static async Task<string> GetChannelInviteAsync(DiscordChannel channel)
    {
        if (LfgInvites.TryGetValue(channel.Id, out var channelInvite))
        {
            return channelInvite;
        }

        var invite = await channel.CreateInviteAsync(max_age: 0);
        return invite.ToString();
    }
}