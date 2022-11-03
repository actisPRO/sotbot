using System.Collections.Generic;
using Bot_NetCore.Providers;

namespace Bot_NetCore
{
    public static class GlobalState
    {
        public static ICollection<EmojiRoleProvider> EmojiRoleProviders = new List<EmojiRoleProvider>();
    }
}