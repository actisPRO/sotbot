using System;
using System.IO;
using System.Threading.Tasks;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using Microsoft.Extensions.Logging;
using DSharpPlus.EventArgs;

namespace Bot_NetCore.Listeners
{
    public static class LoggerListener
    {
        [AsyncListener(EventTypes.CommandExecuted)]
        public static async Task LogOnCommandExecuted(CommandsNextExtension ctx, CommandExecutionEventArgs e)
        {
            var command = (e.Command.Parent != null ? e.Command.Parent.Name + " " : "") + e.Command.Name;

            e.Context.Client.Logger.LogInformation(BotLoggerEvents.Event, $"Пользователь {e.Context.User.Username}#{e.Context.User.Discriminator} ({e.Context.User.Id}) выполнил команду {command}");

            await Task.CompletedTask; //Пришлось добавить, выдавало ошибку при компиляции
        }
    }
}
