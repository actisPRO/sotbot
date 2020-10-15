using System;
using System.Threading.Tasks;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;

namespace Bot_NetCore.Listeners
{
    public class ErrorListener
    {
        [AsyncListener(EventTypes.ClientErrored)]
        private static Task OnErrored(ClientErrorEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Warning, "Bot",
                $"Возникла ошибка при выполнении ивента {e.EventName}.",
                DateTime.Now);
            return Task.CompletedTask;
        }


        /// <summary>
        ///     Отправляем в консоль сообщения об ошибках при выполнении команды.
        /// </summary>
        [AsyncListener(EventTypes.CommandErrored)]
        public static async Task OnCommandErrored(CommandErrorEventArgs e)
        {
            if (e.Exception is CommandNotFoundException) return;

            if (e.Command.Name == "dgenlist" && e.Exception is NotFoundException) return; //костыль

            if (e.Exception is ArgumentException &&
                e.Exception.Message.Contains("Could not convert specified value to given type.") ||
                e.Exception.Message == "Could not find a suitable overload for the command.")
            {
                await e.Context.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Не удалось выполнить команду. Проверьте правильность введенных параметров.");
                return;
            }

            if (e.Exception is ArgumentException &&
                e.Exception.Message == "Not enough arguments supplied to the command.")
            {
                await e.Context.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Не удалось выполнить команду: вы ввели не все параметры.");
                return;
            }

            if (e.Exception is InvalidOperationException &&
                e.Exception.Message == "No matching subcommands were found, and this group is not executable.")
            {
                await e.Context.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Не удалось найти подкоманду.");
                return;
            }

            if (e.Exception is InvalidOperationException &&
                e.Exception.Message == "Unable to convert time!")
            {
                await e.Context.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось определить время.");
                return;
            }

                if (e.Exception is ArgumentNullException &&
                    e.Exception.Message.Contains("Value cannot be null."))
            {
                await e.Context.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Не удалось выполнить команду: вы ввели недопустимые параметры.");
                return;
            }

            if (e.Exception is NotFoundException)
            {
                await e.Context.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не был найден указанный пользователь.");
                return;
            }

            if (e.Exception is ChecksFailedException)
            {
                var msg = $"{Bot.BotSettings.ErrorEmoji} Не удалось выполнить команду: ";

                var ex = e.Exception as ChecksFailedException;
                foreach (var check in ex.FailedChecks)
                    if (check is CooldownAttribute)
                        msg += $"\n Подождите {Utility.FormatTimespan((check as CooldownAttribute).Reset)} после последнего запуска команды.";
                    else if (check is RequireBotPermissionsAttribute)
                        msg += "\n У бота недостаточно прав.";
                    else if (check is RequireOwnerAttribute)
                        msg += "\n Команда для приватных сообщений.";
                    else if (check is RequireGuildAttribute)
                        msg += "\n Доступна только на определённом  сервере.";
                    else if (check is RequireNsfwAttribute)
                        msg += "\n Команда для использования только в NSFW канале.";
                    else if (check is RequireOwnerAttribute)
                        msg += "\n Команда только для владельца бота.";
                    else if (check is RequirePermissionsAttribute)
                        msg += "\n У вас нет доступа к этой команде!";
                    else if (check is RequirePrefixesAttribute)
                        msg += "\n Команда работает только с определённым префиксом.";
                    else if (check is RequireRolesAttribute)
                        msg += "\n У вас нет доступа к этой команде!";
                    else if (check is RequireUserPermissionsAttribute)
                        msg += "\n У вас нет доступа к этой команде!";

                await e.Context.RespondAsync(msg);
                return;
            }

            var command = (e.Command.Parent != null ? e.Command.Parent.Name + " " : "") + e.Command.Name;

            e.Context.Client.DebugLogger.LogMessage(LogLevel.Warning, "SoT",
                $"Участник {e.Context.Member.Username}#{e.Context.Member.Discriminator} " +
                $"({e.Context.Member.Id}) пытался запустить команду {command}, но произошла ошибка.",
                DateTime.Now);

            await e.Context.RespondAsync(
                $"{Bot.BotSettings.ErrorEmoji} Возникла ошибка при выполнении команды **{command}**! Попробуйте ещё раз, если " +
                "ошибка повторяется - проверьте канал `#📚-гайд-по-боту📚`. " +
                $"**Информация об ошибке:** {e.Exception.Message}");

            var errChannel = e.Context.Guild.GetChannel(Bot.BotSettings.ErrorLog);

            var message = $"**Команда:** {command}\n" +
                          $"**Канал:** {e.Context.Channel}\n" +
                          $"**Пользователь:** {e.Context.Member}\n" +
                          $"**Исключение:** {e.Exception.GetType()}:{e.Exception.Message}\n" +
                          $"**Трассировка стека:** \n```{e.Exception.StackTrace}```";

            await errChannel.SendMessageAsync(message);
        }
    }
}
