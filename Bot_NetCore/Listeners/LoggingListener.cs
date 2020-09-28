using System;
using System.IO;
using System.Threading.Tasks;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.EventArgs;

namespace Bot_NetCore.Listeners
{
    public static class LoggerListener
    {
        [AsyncListener(EventTypes.CommandExecuted)]
        public static async Task LogOnCommandExecuted(CommandExecutionEventArgs e)
        {
            var command = (e.Command.Parent != null ? e.Command.Parent.Name + " " : "") + e.Command.Name;

            e.Context.Client.DebugLogger.LogMessage(LogLevel.Info,
                    "Bot",
                    $"Пользователь {e.Context.Member.Username}#{e.Context.Member.Discriminator} ({e.Context.Member.Id}) выполнил команду {command}",
                    DateTime.Now);
            await Task.CompletedTask; //Пришлось добавить, выдавало ошибку при компиляции
        }


#pragma warning disable
        public static async void LogOnLogMessageReceived(object? sender, DebugLogMessageEventArgs e)
#pragma warning restore
        {
            if (!Directory.Exists("logs")) Directory.CreateDirectory("logs");

            var fileName = "logs/" + DateTime.Today.ToString("yyyy-MM-dd");

            var loglevel = "";
            switch (e.Level)
            {
                case LogLevel.Critical:
                    loglevel = "Critical";
                    break;
                case LogLevel.Error:
                    loglevel = "Error";
                    break;
                case LogLevel.Warning:
                    loglevel = "Warning";
                    break;
                case LogLevel.Info:
                    loglevel = "Info";
                    break;
                case LogLevel.Debug:
                    loglevel = "Debug";
                    break;
            }

            try
            {
                //файл для удобного парсинга
                using (var fs = new FileStream(fileName + ".csv", FileMode.Append))
                {
                    using (var sw = new StreamWriter(fs))
                    {
                        var message = e.Message.Replace("\"", "'");
                        await sw.WriteLineAsync($"{e.Timestamp:s},{loglevel},{e.Application},\"{message}\"");
                        sw.Close();
                    }
                }

                //файл для удобного просмотра
                using (var fs = new FileStream(fileName + ".log", FileMode.Append))
                {
                    using (var sw = new StreamWriter(fs))
                    {
                        await sw.WriteLineAsync($"[{e.Timestamp:G}] [{loglevel}] [{e.Application}] {e.Message}");
                        sw.Close();
                    }
                }
            }
            catch (IOException)
            {
                //Файл открыт другим процессом.
            }
        }
    }
}
