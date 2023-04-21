using System.Collections.Generic;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;

namespace Bot_NetCore.Misc;

public static class Invites
{
    /// <summary>
    ///     Словарь, содержащий в качестве ключа Id канала, а в качестве значения - приглашение.
    /// </summary>
    private static Dictionary<ulong, string> LfgInvites = new();
    
    public static async Task<string> GetChannelInviteAsync(DiscordChannel channel)
    {
        if (LfgInvites.TryGetValue(channel.Id, out var channelInvite))
        {
            return channelInvite;
        }

        var invite = await channel.CreateInviteAsync(max_age: 0);
        LfgInvites.Add(channel.Id, invite.ToString());
        return invite.ToString();
    }
    
    public static void RemoveChannelInvite(DiscordClient client, DiscordChannel channel)
    {
        if (!LfgInvites.Remove(channel.Id))
        {
            client.Logger.LogWarning($"Invitation for channel {channel.Id} was not found and has not been deleted");
        }
    }
}