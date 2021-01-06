using Microsoft.Extensions.Logging;

namespace Bot_NetCore.Misc
{
    public static class BotLoggerEvents
    {
        /// <summary>
        ///     Все что касается бота.
        /// </summary>
        public static EventId Bot { get; } = new EventId(1001, "Bot");

        /// <summary>
        ///     Все что касается команд.
        /// </summary>
        public static EventId Commands { get; } = new EventId(1002, nameof(Commands));

        /// <summary>
        ///     Все что касается ивентов.
        /// </summary>
        public static EventId Event { get; } = new EventId(1003, nameof(Event));

        /// <summary>
        ///     Все что касается логики ивентов.
        /// </summary>
        public static EventId AsyncListener { get; } = new EventId(1004, nameof(AsyncListener));

        /// <summary>
        ///     Все что касается таймеров.
        /// </summary>
        public static EventId Timers { get; } = new EventId(1005, nameof(Timers));
    }
}
