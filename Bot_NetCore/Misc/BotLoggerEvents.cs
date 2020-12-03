using System;
using System.Collections.Generic;
using System.Text;
using DSharpPlus;
using Microsoft.Extensions.Logging;

namespace Bot_NetCore.Misc
{
    public static class BotLoggerEvents
    {
        /// <summary>
        ///     Все что касается бота.
        /// </summary>
        public static EventId Bot { get; } = new EventId(400, "Bot");

        /// <summary>
        ///     Все что касается команд.
        /// </summary>
        public static EventId Commands { get; } = new EventId(401, nameof(Commands));

        /// <summary>
        ///     Все что касается ивентов.
        /// </summary>
        public static EventId Event { get; } = new EventId(401, nameof(Event));

        /// <summary>
        ///     Все что касается логики ивентов.
        /// </summary>
        public static EventId AsyncListener { get; } = new EventId(401, nameof(AsyncListener));

        /// <summary>
        ///     Все что касается таймеров.
        /// </summary>
        public static EventId Timers { get; } = new EventId(401, nameof(Timers));
    }
}
