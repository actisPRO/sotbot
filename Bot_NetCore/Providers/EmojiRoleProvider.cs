using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Renci.SshNet.Messages;

namespace Bot_NetCore.Providers
{
    public class EmojiRoleProvider
    {
        public DiscordChannel Channel { get; init; }
        public ulong MessageId { get; init; }
        public IDictionary<DiscordEmoji, DiscordRole> Roles { get; init; }
        public bool Removable { get; init; }

        public EmojiRoleProvider()
        {
        }

        public EmojiRoleProvider(DiscordChannel channel,
            IDictionary<DiscordEmoji, DiscordRole> roles,
            ulong messageId,
            bool removable = true)
        {
            Channel = channel;
            Roles = roles;
            MessageId = messageId;
            Removable = removable;
        }

        public async Task ValidateEmojisAsync(DiscordClient client)
        {
            client.Logger.LogInformation($"Validating emojis for ERP: Channel {Channel.Id}, Message {MessageId}");
            var message = await Channel.GetMessageAsync(MessageId);

            var requiredEmojiList = new List<DiscordEmoji>(Roles.Keys);
            if (Removable) requiredEmojiList.Add(DiscordEmoji.FromName(client, ":x:"));

            var existingEmojis = (from reaction in message.Reactions
                select reaction.Emoji).ToList();

            foreach (var emoji in requiredEmojiList)
            {
                if (!existingEmojis.Contains(emoji))
                {
                    await message.CreateReactionAsync(emoji);
                }
            }
        }

        public async Task GrantRoleAsync(DiscordClient client, DiscordMember member, DiscordEmoji emoji)
        {
            var providerRoles = Roles.Values;
            foreach (var role in new List<DiscordRole>(member.Roles))
                if (providerRoles.Contains(role))
                    await member.RevokeRoleAsync(role);
            
            if (Removable && emoji == DiscordEmoji.FromName(client, ":x:")) return;

            if (Roles.ContainsKey(emoji))
            {
                await member.GrantRoleAsync(Roles[emoji]);
            }
        }
    }
}
