using System;
using System.Threading.Tasks;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.EventArgs;

namespace Bot_NetCore.Listeners
{
    public static class DMListener
    {
        [AsyncListener(EventTypes.DmChannelCreated)]
        public static async Task DmChannelCreated(DmChannelCreateEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT", "DM Created",
                DateTime.Now); // и еще немного ЧСВ
        }

        [AsyncListener(EventTypes.DmChannelDeleted)]
        public static async Task DmChannelDeleted(DmChannelDeleteEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT", "DM Deleted",
                DateTime.Now); // и еще немного ЧСВ
        }

        [AsyncListener(EventTypes.MessageCreated)]
        public static async Task MessageCreated(MessageCreateEventArgs e)
        {
            if(e.Guild == null)
                e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT", "DM Message received",
                    DateTime.Now); // и еще немного ЧСВ
        }

        [AsyncListener(EventTypes.MessageDeleted)]
        public static async Task MessageDeleted(MessageDeleteEventArgs e)
        {
            if (e.Guild == null)
                e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT", "DM Message removed",
                    DateTime.Now); // и еще немного ЧСВ
        }
    }
}
