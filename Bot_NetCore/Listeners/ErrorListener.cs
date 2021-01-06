using System;
using System.Threading.Tasks;
using Bot_NetCore.Attributes;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bot_NetCore.Listeners
{
    public class ErrorListener
    {
        [AsyncListener(EventTypes.ClientErrored)]
        private static Task OnErrored(DiscordClient client, ClientErrorEventArgs e)
        {
            client.Logger.LogWarning(BotLoggerEvents.Event, $"Возникла ошибка при выполнении ивента {e.EventName}.");
            return Task.CompletedTask;
        }


        /// <summary>
        ///     Отправляем в консоль сообщения об ошибках при выполнении команды.
        /// </summary>
        [AsyncListener(EventTypes.CommandErrored)]
        public static async Task OnCommandErrored(CommandsNextExtension ctx, CommandErrorEventArgs e)
        {
            //Команда не найдена - Ничего не отправляем
            if (e.Exception is CommandNotFoundException) return;

            var command = (e.Command.Parent != null ? e.Command.Parent.Name + " " : "") + e.Command.Name;
            var commandHint = $":information_source: Используйте `!help {command}` для подробной информации о команде.";

            //Костыль для команды "genlist" - вообще нужно?
            if (e.Command.Name == "genlist" && e.Exception is NotFoundException) return; //костыль

            //Проблемы с параметрами команды
            if (e.Exception is ArgumentException)
            {
                //Введены не правильные параметры команды
                if (e.Exception.Message.Contains("Could not convert specified value to given type.") ||
                    e.Exception.Message == "Could not find a suitable overload for the command.")
                {
                    await e.Context.RespondAsync(
                        $"{Bot.BotSettings.ErrorEmoji} Не удалось выполнить команду. Проверьте правильность введенных параметров.\n{commandHint}");
                }

                if (e.Exception.Message == "Not enough arguments supplied to the command.")
                {
                    await e.Context.RespondAsync(
                        $"{Bot.BotSettings.ErrorEmoji} Не удалось выполнить команду: вы ввели не все параметры.\n{commandHint}");
                }
                return;
            }

            //Введены пустые параметры команды
            if (e.Exception is ArgumentNullException &&
                e.Exception.Message.Contains("Value cannot be null."))
            {
                await e.Context.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Не удалось выполнить команду: вы ввели недопустимые параметры.\n{commandHint}");
                return;
            }

            //Введена несуществующая подкоманда
            if (e.Exception is InvalidOperationException &&
                e.Exception.Message == "No matching subcommands were found, and this group is not executable.")
            {
                await e.Context.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Не удалось найти команду.\n{commandHint}");
                return;
            }

            //Параметр с временем не удалось определить
            if (e.Exception is InvalidOperationException &&
                e.Exception.Message == "Unable to convert time!")
            {
                await e.Context.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось определить время.");
                return;
            }

            //Не удалось найти пользователя
            if (e.Exception is NotFoundException || e.Exception is NullReferenceException)
            {
                await e.Context.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не был найден указанный пользователь.");
                return;
            }

            //Ошибки при проверке выполнения команды
            if (e.Exception is ChecksFailedException)
            {
                var msg = $"{Bot.BotSettings.ErrorEmoji} Не удалось выполнить команду: ";

                var ex = e.Exception as ChecksFailedException;
                foreach (var check in ex.FailedChecks)
                    if (check is CooldownAttribute)
                        msg += $"\n Подождите {Utility.FormatTimespan((check as CooldownAttribute).Reset)} до нового ввода команды.";
                    else if (check is RequireBotPermissionsAttribute)
                        msg += "\n У бота недостаточно прав.";
                    else if (check is RequireOwnerAttribute)
                        msg += "\n Команда для приватных сообщений.";
                    else if (check is RequireGuildAttribute)
                        msg += "\n Использование команды доступно только на сервере.";
                    else if (check is RequireDirectMessageAttribute)
                        msg += "\n Использование команды доступно только в личных сообщениях.";
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
                    else if (check is RequireCustomRole)
                        msg += "\n У вас нет доступа к этой команде!";
                    else if (check is RequireUserPermissionsAttribute)
                        msg += "\n У вас нет доступа к этой команде!";

                await e.Context.RespondAsync(msg);
                return;
            }

            //Ошибка сервера
            if (e.Exception is ServerErrorException)
            {
                await e.Context.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Возникла ошибка во время запроса к серверу! Попробуйте ещё раз, если " +
                    "ошибка повторяется - обратитесь к разработчикам через `!support`");
                return;
            }

            //Другие ошибки
            ctx.Client.Logger.LogWarning(BotLoggerEvents.Event, $"Участник {e.Context.User.Username}#{e.Context.User.Discriminator} " +
                $"({e.Context.User.Id}) пытался запустить команду {command}, но произошла ошибка.");

            await e.Context.RespondAsync(
                $"{Bot.BotSettings.ErrorEmoji} Возникла ошибка при выполнении команды **{command}**! Попробуйте ещё раз, если " +
                "ошибка повторяется - проверьте канал `#📚-гайд-по-боту📚`. " +
                $"**Информация об ошибке:** {e.Exception.Message}");

            //Отправляем данные об ошибке в канал лога ошибок.
            var guild = await e.Context.Client.GetGuildAsync(Bot.BotSettings.Guild);
            var errChannel = guild.GetChannel(Bot.BotSettings.ErrorLog);

            var message = $"**Команда:** {command}\n" +
                          $"**Канал:** {e.Context.Channel}\n" +
                          $"**Пользователь:** {e.Context.User}\n" +
                          $"**Исключение:** {e.Exception.GetType()}:{e.Exception.Message}\n" +
                          $"**Трассировка стека:** \n```{e.Exception.StackTrace}```";

            await errChannel.SendMessageAsync(message);
        }
    }
}
