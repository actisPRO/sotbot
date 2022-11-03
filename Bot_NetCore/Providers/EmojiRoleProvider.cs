using System.Collections.Generic;
using System.Linq;
using DSharpPlus;
using DSharpPlus.Entities;
using Renci.SshNet.Messages;

namespace Bot_NetCore.Providers
{
    public class EmojiRoleProvider
    {
        public DiscordChannel Channel { get; init; }
        public ulong MessageId { get; init; }
        public IDictionary<DiscordEmoji, DiscordRole> Roles { get; init; }
        public bool Removable { get; init; }

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

        public async void ValidateEmojis(DiscordClient client)
        {
            var message = await Channel.GetMessageAsync(MessageId);
            
            var requiredEmojiList = Roles.Keys;
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

        public async void GrantRole(DiscordClient client, DiscordMember member, DiscordEmoji emoji)
        {
            if (Removable && emoji == DiscordEmoji.FromName(client, ":x:"))
            {
                var providerRoles = Roles.Values;
                foreach (var role in member.Roles)
                    if (providerRoles.Contains(role))
                        await member.RevokeRoleAsync(role);
                
                return;
            }

            if (Roles.ContainsKey(emoji))
            {
                await member.GrantRoleAsync(Roles[emoji]);
            }
        }
    }
}

public class ReactionEmojiComparer : IEqualityComparer<DiscordReaction>
{
    public bool Equals(DiscordReaction x, DiscordReaction y) => x?.Emoji.Name == y?.Emoji.Name;

    public int GetHashCode(DiscordReaction obj) => obj.Emoji.GetHashCode();
}