using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Commands;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;

namespace Bot_NetCore.Listeners
{
    public static class JoinListener
    {
        ///     Invites список приглашений
        /// </summary>
        public static List<DiscordInvite> Invites;

        [AsyncListener(EventTypes.Ready)]
        public static async Task InvitesOnClientOnReady(ReadyEventArgs e)
        {
            var guild = e.Client.Guilds[Bot.BotSettings.Guild];
            var guildInvites = await guild.GetInvitesAsync();
            Invites = guildInvites.ToList();
        }

        /// <summary>
        ///     Лог посещений
        /// </summary>
        [AsyncListener(EventTypes.GuildMemberRemoved)]
        public static async Task ClientOnGuildMemberRemoved(GuildMemberRemoveEventArgs e)
        {
            // Сохранение ролей участника
            var roles = e.Member.Roles;
            var rolesToSave = new List<ulong>();
            var ignoredRoles = new List<ulong> //роли, которые не нужно сохранять при выходе
            {
                Bot.BotSettings.CodexRole,
                Bot.BotSettings.FleetCodexRole,
                Bot.BotSettings.MuteRole,
                Bot.BotSettings.VoiceMuteRole,
                Bot.BotSettings.EmissaryAthenaRole,
                Bot.BotSettings.EmissaryGoldhoadersRole,
                Bot.BotSettings.EmissaryReaperBonesRole,
                Bot.BotSettings.EmissaryTradingCompanyRole,
                Bot.BotSettings.EmissaryOrderOfSoulsRole,
                e.Guild.EveryoneRole.Id,
            };

            foreach (var role in roles)
            {
                if (!ignoredRoles.Contains(role.Id))
                {
                    rolesToSave.Add(role.Id);
                }
            }

            if (rolesToSave.Count != 0)
            {
                UsersLeftList.Users[e.Member.Id] = new UserLeft(e.Member.Id, rolesToSave);
                UsersLeftList.SaveToXML(Bot.BotSettings.UsersLeftXML);
            }

            await e.Guild.GetChannel(Bot.BotSettings.UserlogChannel)
                .SendMessageAsync(
                    $"**Участник покинул сервер:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}).");

            //Обновляем статус бота
            await Bot.UpdateBotStatusAsync(e.Client, e.Guild);

            //Если пользователь не был никем приглашен, то при выходе он будет сохранен.
            if (!InviterList.Inviters.ToList().Any(i => i.Value.Referrals.ContainsKey(e.Member.Id)))
            {
                if (!InviterList.Inviters.ContainsKey(0))
                    InviterList.Inviters[0] = new Inviter(0, false);

                InviterList.Inviters[0].AddReferral(e.Member.Id, false);
            }

            //При выходе обновляем реферала на неактив.
            InviterList.Inviters.ToList().Where(i => i.Value.Referrals.ContainsKey(e.Member.Id)).ToList()
                                         .ForEach(i => i.Value.UpdateReferral(e.Member.Id, false));

            InviterList.SaveToXML(Bot.BotSettings.InviterXML);

            await LeaderboardCommands.UpdateLeaderboard(e.Guild);

            e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                $"Участник {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}) покинул сервер.",
                DateTime.Now);
        }

        /// <summary>
        ///     Приветственное сообщение + лог посещений + проверка на бан
        /// </summary>
        [AsyncListener(EventTypes.GuildMemberAdded)]
        public static async Task ClientOnGuildMemberAdded(GuildMemberAddEventArgs e)
        {
            //Проверка на бан
            var userBans = BanSQL.GetForUser(e.Member.Id);
            var unexpiredBans = new List<BanSQL>();
            foreach (var ban in userBans)
                if (ban.UnbanDateTime > DateTime.Now) unexpiredBans.Add(ban);
            if (unexpiredBans.Any())
            {
                var message = "**У вас есть неистёкшие блокировки на этом сервере:** \n";
                foreach (var ban in unexpiredBans)
                {
                    message +=
                        $"• **Причина:** {ban.Reason}. **Истекает через** {Utility.FormatTimespan(ban.UnbanDateTime - DateTime.Now)} | {ban.UnbanDateTime:dd.MM.yyyy HH:mm:ss}\n";
                }

                try
                {
                    await e.Member.SendMessageAsync(message);
                }
                catch (UnauthorizedException)
                {
                    // if bot is in user's blacklist
                }

                await e.Member.BanAsync();
                return;
            }

            //Проверка на mute
            if (ReportList.Mutes.ContainsKey(e.Member.Id) && !ReportList.Mutes[e.Member.Id].Expired())
            {
                //Отправка сообщения в лс
                try
                {
                    await e.Member.SendMessageAsync(
                        $"**Вам выдан мут на данном сервере**\n\n" +
                        $"**Снятие через:** {Utility.FormatTimespan(ReportList.Mutes[e.Member.Id].getRemainingTime())}\n" +
                        $"**Причина:** {ReportList.Mutes[e.Member.Id].Reason}");
                }
                catch (UnauthorizedException)
                {
                    //user can block the bot
                }

                //Выдаем роль мута
                await e.Member.GrantRoleAsync(e.Guild.GetRole(Bot.BotSettings.MuteRole));
            }

            //Проверка на voice mute
            if (ReportList.VoiceMutes.ContainsKey(e.Member.Id) && !ReportList.VoiceMutes[e.Member.Id].Expired())
            {
                //Отправка сообщения в лс
                try
                {
                    await e.Member.SendMessageAsync(
                        $"**Вам выдан голосовой мут на данном сервере**\n\n" +
                        $"**Снятие через:** {Utility.FormatTimespan(ReportList.VoiceMutes[e.Member.Id].getRemainingTime())}\n" +
                        $"**Причина:** {ReportList.VoiceMutes[e.Member.Id].Reason}");
                }
                catch (UnauthorizedException)
                {
                    //user can block the bot
                }

                //Выдаем роль мута
                await e.Member.GrantRoleAsync(e.Guild.GetRole(Bot.BotSettings.VoiceMuteRole));
            }

            //Проверка на purge
            if (ReportList.CodexPurges.ContainsKey(e.Member.Id) && !ReportList.CodexPurges[e.Member.Id].Expired())
                await e.Member.GrantRoleAsync(e.Guild.GetRole(Bot.BotSettings.PurgeCodexRole));

            //Выдача доступа к приватным кораблям
            try
            {
                var ships = ShipList.Ships.Values.Where(x => x.Members.ContainsKey(e.Member.Id));
                foreach (var ship in ships)
                    await e.Guild.GetChannel(ship.Channel).AddOverwriteAsync(e.Member, Permissions.UseVoice);
            }
            catch (Exception ex)
            {
                e.Client.DebugLogger.LogMessage(LogLevel.Warning, "Bot",
                   $"Ошибка при выдаче доступа к приватному кораблю. \n{ex.Message}\n{ex.StackTrace}",
                   DateTime.Now); ;
            }

            var invites = Invites.AsReadOnly().ToList(); //Сохраняем список старых инвайтов в локальную переменную
            var guildInvites = await e.Guild.GetInvitesAsync(); //Запрашиваем новый список инвайтов
            Invites = guildInvites.ToList(); //Обновляю список инвайтов

            try
            {
                await e.Member.SendMessageAsync($"**Привет, {e.Member.Mention}!\n**" +
                                                "Мы рады что ты присоединился к нашему сообществу :wink:!\n\n" +
                                                "Прежде чем приступать к игре, прочитай и прими правила в канале " +
                                                "`#👮-пиратский-кодекс-👮`. После принятия можешь ознакомиться с гайдом по боту" +
                                                "в канале `#📚-гайд-по-боту-📚`.\n" +
                                                "Если у тебя есть какие-то вопросы, не стесняйся писать администрации.\n\n" +
                                                "**Удачной игры!**");
            }
            catch (UnauthorizedException)
            {
                //Пользователь заблокировал бота
            }

            // Выдача ролей, которые были у участника перед выходом.
            if (UsersLeftList.Users.ContainsKey(e.Member.Id))
            {
                foreach (var role in UsersLeftList.Users[e.Member.Id].Roles)
                {
                    try
                    {
                        await e.Member.GrantRoleAsync(e.Guild.GetRole(role));
                    }
                    catch (NotFoundException)
                    {

                    }
                }

                UsersLeftList.Users.Remove(e.Member.Id);
                UsersLeftList.SaveToXML(Bot.BotSettings.UsersLeftXML);
            }

            try
            {
                //Находит обновившийся инвайт по количеству приглашений
                //Вызывает NullReferenceException в случае если ссылка только для одного использования
                var updatedInvite = guildInvites.ToList().Find(g => invites.Find(i => i.Code == g.Code).Uses < g.Uses);

                //Если не удалось определить инвайт, значит его нет в новых так как к.во использований ограничено и он был удален
                if (updatedInvite == null)
                {
                    updatedInvite = invites.Where(p => guildInvites.All(p2 => p2.Code != p.Code))                       //Ищем удаленный инвайт
                                           .Where(x => (x.CreatedAt.AddSeconds(x.MaxAge) < DateTimeOffset.Now))      //Проверяем если он не истёк
                                           .FirstOrDefault();                                                           //С такими условиями будет только один такой инвайт
                }

                if (updatedInvite != null)
                {

                    await e.Guild.GetChannel(Bot.BotSettings.UserlogChannel)
                    .SendMessageAsync(
                        $"**Участник присоединился:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}) используя " +
                        $"приглашение {updatedInvite.Code} от участника {updatedInvite.Inviter.Username}#{updatedInvite.Inviter.Discriminator}. ");

                    e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                        $"Участник {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}) присоединился к серверу. Приглашение: {updatedInvite.Code} от участника {updatedInvite.Inviter.Username}#{updatedInvite.Inviter.Discriminator}.",
                        DateTime.Now);

                    //Проверяем если пригласивший уже существует, если нет то создаем
                    if (!InviterList.Inviters.ContainsKey(updatedInvite.Inviter.Id))
                        Inviter.Create(updatedInvite.Inviter.Id, e.Member.IsBot);

                    //Проверяем если пользователь был ранее приглашен другими и обновляем активность, если нет то вносим в список
                    if (InviterList.Inviters.ToList().Exists(x => x.Value.Referrals.ContainsKey(e.Member.Id)))
                        InviterList.Inviters.ToList().Where(x => x.Value.Referrals.ContainsKey(e.Member.Id)).ToList()
                            .ForEach(x => x.Value.UpdateReferral(e.Member.Id, true));
                    else
                        InviterList.Inviters[updatedInvite.Inviter.Id].AddReferral(e.Member.Id);

                    InviterList.SaveToXML(Bot.BotSettings.InviterXML);
                    //Обновление статистики приглашений
                    await LeaderboardCommands.UpdateLeaderboard(e.Guild);
                }
                else
                {
                    await e.Guild.GetChannel(Bot.BotSettings.UserlogChannel)
                        .SendMessageAsync(
                            $"**Участник присоединился:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}). " +
                            $"Приглашение не удалось определить.");

                    e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                        $"Участник {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}) присоединился к серверу. Приглашение не удалось определить.",
                        DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                await e.Guild.GetChannel(Bot.BotSettings.UserlogChannel)
                    .SendMessageAsync(
                        $"**Участник присоединился:** {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}). " +
                        $"При попытке отследить инвайт произошла ошибка.");

                e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                    $"Участник {e.Member.Username}#{e.Member.Discriminator} ({e.Member.Id}) присоединился к серверу. Приглашение не удалось определить.",
                    DateTime.Now);

                e.Client.DebugLogger.LogMessage(LogLevel.Warning, "Bot",
                    "Не удалось определить приглашение.",
                    DateTime.Now);

                var errChannel = e.Guild.GetChannel(Bot.BotSettings.ErrorLog);

                var message = "**Ошибка при логгинге инвайта**\n" +
                              $"**Пользователь:** {e.Member}\n" +
                              $"**Исключение:** {ex.GetType()}:{ex.Message}\n" +
                              $"**Трассировка стека:** \n```{ex.StackTrace}```\n" +
                              $"{ex}";

                await errChannel.SendMessageAsync(message);
            }

            //Обновляем статус бота
            await Bot.UpdateBotStatusAsync(e.Client, e.Guild);
        }

        /// <summary>
        ///     Проверка на создание инвайтов
        /// </summary>
        [AsyncListener(EventTypes.InviteCreated)]
        public static async Task ClientOnInviteCreated(InviteCreateEventArgs e)
        {
            var guildInvites = await e.Client.Guilds[Bot.BotSettings.Guild].GetInvitesAsync();
            Invites = guildInvites.ToList();
        }

        /// <summary>
        ///     Проверка на удаление инвайтов
        /// </summary>
        [AsyncListener(EventTypes.InviteDeleted)]
        public static async Task ClientOnInviteDeleted(InviteDeleteEventArgs e)
        {
            var guildInvites = await e.Client.Guilds[Bot.BotSettings.Guild].GetInvitesAsync();
            Invites = guildInvites.ToList();
        }
    }
}
