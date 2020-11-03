using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Listeners
{
    public static class ReactionListener
    {
        /// <summary>
        ///     Словарь, содержащий в качестве ключа пользователя Discord, а в качестве значения - время истечения кулдауна.
        /// </summary>
        public static Dictionary<DiscordUser, DateTime> EmojiCooldowns = new Dictionary<DiscordUser, DateTime>();

        [AsyncListener(EventTypes.MessageReactionRemoved)]
        public static async Task ClientOnMessageReactionRemoved(MessageReactionRemoveEventArgs e)
        {
            if (e.User.IsBot) return;

            //Проверка если сообщение с принятием правил
            if (e.Message.Id == Bot.BotSettings.CodexMessageId)
            {
                //При надобности добавить кулдаун
                //if (EmojiCooldowns.ContainsKey(e.User)) // проверка на кулдаун
                //    if ((EmojiCooldowns[e.User] - DateTime.Now).Seconds > 0) return;

                //// если проверка успешно пройдена, добавим пользователя
                //// в словарь кулдаунов
                //EmojiCooldowns[e.User] = DateTime.Now.AddSeconds(Bot.BotSettings.FastCooldown);

                //Забираем роль
                var user = (DiscordMember)e.User;
                if (user.Roles.Any(x => x.Id == Bot.BotSettings.CodexRole))
                    await user.RevokeRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.CodexRole));

                return;
            }

            //Emissary Message
            if (e.Message.Id == Bot.BotSettings.EmissaryMessageId) return;
        }

        [AsyncListener(EventTypes.MessageReactionAdded)]
        public static async Task ClientOnMessageReactionAdded(MessageReactionAddEventArgs e)
        {
            if (e.User.IsBot) return;

            //Проверка если сообщение с принятием правил сообщества
            if (e.Message.Id == Bot.BotSettings.CodexMessageId && e.Emoji.GetDiscordName() == ":white_check_mark:")
            {
                //При надобности добавить кулдаун
                /*if (EmojiCooldowns.ContainsKey(e.User)) // проверка на кулдаун
                    if ((EmojiCooldowns[e.User] - DateTime.Now).Seconds > 0) return;

                // если проверка успешно пройдена, добавим пользователя
                // в словарь кулдаунов
                EmojiCooldowns[e.User] = DateTime.Now.AddSeconds(Bot.BotSettings.FastCooldown);*/

                //Проверка на purge
                var hasPurge = false;
                ReportSQL validPurge = null;
                foreach (var purge in ReportSQL.GetForUser(e.User.Id, ReportType.CodexPurge))
                {
                    if (purge.ReportEnd > DateTime.Now)
                    {
                        validPurge = purge;
                        hasPurge = true;
                        break;
                    }
                }

                if (hasPurge)
                {
                    var moderator = await e.Channel.Guild.GetMemberAsync(validPurge.Moderator);
                    try
                    {
                        await ((DiscordMember) e.User).SendMessageAsync(
                            "**Возможность принять правила заблокирована**\n" +
                            $"**Снятие через:** {Utility.FormatTimespan(DateTime.Now - validPurge.ReportEnd)}\n" +
                            $"**Модератор:** {moderator.Username}#{moderator.Discriminator}\n" +
                            $"**Причина:** {validPurge.Reason}\n");
                    }

                    catch (UnauthorizedException)
                    {
                        //user can block the bot
                    }
                    return;
                }

                //Выдаем роль правил
                var user = (DiscordMember)e.User;
                if (!user.Roles.Contains(e.Channel.Guild.GetRole(Bot.BotSettings.CodexRole)))
                {
                    //Выдаем роль правил
                    await user.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.CodexRole));

                    //Убираем роль блокировки правил
                    await user.RevokeRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.PurgeCodexRole));

                    e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                        $"Пользователь {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) подтвердил прочтение правил через реакцию.",
                        DateTime.Now);
                }

                return;
            }

            //Проверка если сообщение с принятием правил рейда
            if (e.Message.Id == Bot.BotSettings.FleetCodexMessageId && e.Emoji.GetDiscordName() == ":white_check_mark:")
            {
                //Проверка на purge
                var hasPurge = false;
                ReportSQL validPurge = null;
                foreach (var purge in ReportSQL.GetForUser(e.User.Id, ReportType.FleetPurge))
                {
                    if (purge.ReportEnd > DateTime.Now)
                    {
                        validPurge = purge;
                        hasPurge = true;
                        break;
                    }
                }

                if (hasPurge)
                {
                    var moderator = await e.Channel.Guild.GetMemberAsync(validPurge.Moderator);
                    try
                    {
                        await ((DiscordMember) e.User).SendMessageAsync(
                            "**Возможность принять правила заблокирована**\n" +
                            $"**Снятие через:** {Utility.FormatTimespan(DateTime.Now - validPurge.ReportEnd)}\n" +
                            $"**Модератор:** {moderator.Username}#{moderator.Discriminator}\n" +
                            $"**Причина:** {validPurge.Reason}\n");
                    }

                    catch (UnauthorizedException)
                    {
                        //user can block the bot
                    }
                    return;
                } //Удаляем блокировку если истекла
        
                
                var user = (DiscordMember) e.User;
                
                //Проверка на регистрацию и привязку Xbox

                var cs =
                    $"Server={Bot.BotSettings.DatabaseHost}; Port=3306; Database={Bot.BotSettings.DatabaseName}; Uid={Bot.BotSettings.DatabaseUser}; Pwd={Bot.BotSettings.DatabasePassword};";
                var connection = new MySqlConnection(cs);
                await connection.OpenAsync();
                
                var statement = $"SELECT xbox FROM users WHERE userid='{user.Id}'";
                var cmd = new MySqlCommand(statement, connection);
                var res = cmd.ExecuteScalar();
                
                await connection.CloseAsync();
                
                if (res == null || res == DBNull.Value || res.ToString() == "")
                {
                    await user.SendMessageAsync(
                        $"{Bot.BotSettings.ErrorEmoji} К вашему аккаунту не привязан Xbox, пожалуйста, войдите с помощью Discord на сайт {Bot.BotSettings.WebURL}login и повторите попытку.");
                    return;
                }
                
                // Проверка ЧС

                if (BlacklistEntry.IsBlacklisted(user.Id))
                {
                    await user.SendMessageAsync(
                        $"{Bot.BotSettings.ErrorEmoji} Вы находитесь в чёрном списке рейдов и вам навсегда ограничен доступ к ним.");
                    return;
                }

                //Выдаем роль правил рейда
                if (!user.Roles.Any(x => x.Id == Bot.BotSettings.FleetCodexRole))
                {
                    await user.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.FleetCodexRole));
                    e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                        $"Пользователь {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) подтвердил прочтение правил рейда.",
                        DateTime.Now);
                }

                return;
            }

            //Проверка на сообщение эмиссарства
            if (e.Message.Id == Bot.BotSettings.EmissaryMessageId)
            {
                await e.Message.DeleteReactionAsync(e.Emoji, e.User);

                if (EmojiCooldowns.ContainsKey(e.User)) // проверка на кулдаун
                    if ((EmojiCooldowns[e.User] - DateTime.Now).Seconds > 0) return;

                // если проверка успешно пройдена, добавим пользователя
                // в словарь кулдаунов
                EmojiCooldowns[e.User] = DateTime.Now.AddSeconds(Bot.BotSettings.FastCooldown);

                //Проверка у пользователя уже существующих ролей эмисарства и их удаление
                var user = (DiscordMember)e.User;
                user.Roles.Where(x => x.Id == Bot.BotSettings.EmissaryGoldhoadersRole ||
                                x.Id == Bot.BotSettings.EmissaryTradingCompanyRole ||
                                x.Id == Bot.BotSettings.EmissaryOrderOfSoulsRole ||
                                x.Id == Bot.BotSettings.EmissaryAthenaRole ||
                                x.Id == Bot.BotSettings.EmissaryReaperBonesRole ||
                                x.Id == Bot.BotSettings.HuntersRole ||
                                x.Id == Bot.BotSettings.ArenaRole).ToList()
                         .ForEach(async x => await user.RevokeRoleAsync(x));

                //Выдаем роль в зависимости от реакции
                switch (e.Emoji.GetDiscordName())
                {
                    case ":moneybag:":
                        await user.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.EmissaryGoldhoadersRole));
                        break;
                    case ":pig:":
                        await user.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.EmissaryTradingCompanyRole));
                        break;
                    case ":skull:":
                        await user.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.EmissaryOrderOfSoulsRole));
                        break;
                    case ":gem:":
                        await user.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.EmissaryAthenaRole));
                        break;
                    case ":bone:":
                        await user.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.EmissaryReaperBonesRole));
                        break;
                    case ":fish:":
                        await user.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.HuntersRole));
                        break;
                    case ":axe:":
                        await user.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.ArenaRole));
                        break;
                    default:
                        break;
                }
                //Отправка в лог
                e.Client.DebugLogger.LogMessage(LogLevel.Info, "SoT",
                    $"{e.User.Username}#{e.User.Discriminator} получил новую роль эмиссарства.",
                    DateTime.Now);

                return;
            }

            //Проверка на голосование
            if (e.Message.Channel.Id == Bot.BotSettings.VotesChannel)
            {
                var vote = Vote.GetByMessageId(e.Message.Id);

                await e.Message.DeleteReactionAsync(e.Emoji, e.User);

                // Проверка на окончание голосования
                if (DateTime.Now > vote.End)
                {
                    return;
                }

                // Проверка на предыдущий голос
                if (vote.Voters.ContainsKey(e.User.Id))
                {
                    return;
                }

                if (e.Emoji.GetDiscordName() == ":white_check_mark:")
                {
                    vote.Voters.Add(e.User.Id, true);
                    ++vote.Yes;
                }
                else
                {
                    vote.Voters.Add(e.User.Id, false);
                    ++vote.No;
                }

                var total = vote.Voters.Count;

                var author = await e.Guild.GetMemberAsync(vote.Author);
                var embed = Utility.GenerateVoteEmbed(
                    author,
                    DiscordColor.Yellow,
                    vote.Topic,
                    vote.End,
                    vote.Voters.Count,
                    vote.Yes,
                    vote.No,
                    vote.Id);

                Vote.Save(Bot.BotSettings.VotesXML);

                await e.Message.ModifyAsync(embed: embed);
                await (await e.Guild.GetMemberAsync(e.User.Id)).SendMessageAsync($"{Bot.BotSettings.OkEmoji} Спасибо, ваш голос учтён!");
            }

            //then check if it is a private ship confirmation message
            foreach (var ship in ShipList.Ships.Values)
            {
                if (ship.Status) continue;

                if (e.Message.Id == ship.CreationMessage)
                {
                    if (e.Emoji == DiscordEmoji.FromName((DiscordClient)e.Client, ":white_check_mark:"))
                    {
                        var name = ship.Name;
                        var channel = await e.Channel.Guild.CreateChannelAsync($"☠{name}☠", ChannelType.Voice,
                            e.Channel.Guild.GetChannel(Bot.BotSettings.PrivateCategory), bitrate: Bot.BotSettings.Bitrate);

                        var member = await e.Channel.Guild.GetMemberAsync(ShipList.Ships[name].Members.ToArray()[0].Value.Id);

                        await channel.AddOverwriteAsync(member, Permissions.UseVoice);
                        await channel.AddOverwriteAsync(e.Channel.Guild.GetRole(Bot.BotSettings.CodexRole), Permissions.AccessChannels);
                        await channel.AddOverwriteAsync(e.Channel.Guild.EveryoneRole, Permissions.None, Permissions.UseVoice);

                        ShipList.Ships[name].SetChannel(channel.Id);
                        ShipList.Ships[name].SetStatus(true);
                        ShipList.Ships[name].SetMemberStatus(member.Id, true);

                        ShipList.SaveToXML(Bot.BotSettings.ShipXML);

                        await member.SendMessageAsync(
                            $"{Bot.BotSettings.OkEmoji} Запрос на создание корабля **{name}** был подтвержден администратором **{e.User.Username}#{e.User.Discriminator}**");
                        await e.Channel.SendMessageAsync(
                            $"{Bot.BotSettings.OkEmoji} Администратор **{e.User.Username}#{e.User.Discriminator}** успешно подтвердил " +
                            $"запрос на создание корабля **{name}**!");

                        e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                            $"Администратор {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) подтвердил создание приватного корабля {name}.",
                            DateTime.Now);
                    }
                    else if (e.Emoji == DiscordEmoji.FromName((DiscordClient)e.Client, ":no_entry:"))
                    {
                        var name = ship.Name;
                        var member =
                            await e.Channel.Guild.GetMemberAsync(ShipList.Ships[name].Members.ToArray()[0].Value.Id);

                        ShipList.Ships[name].Delete();
                        ShipList.SaveToXML(Bot.BotSettings.ShipXML);

                        var doc = XDocument.Load("actions.xml");
                        foreach (var action in doc.Element("actions").Elements("action"))
                            if (Convert.ToUInt64(action.Value) == member.Id)
                                action.Remove();
                        doc.Save("actions.xml");

                        await member.SendMessageAsync(
                            $"{Bot.BotSettings.OkEmoji} Запрос на создание корабля **{name}** был отклонен администратором **{e.User.Username}#{e.User.Discriminator}**");
                        await e.Channel.SendMessageAsync(
                            $"{Bot.BotSettings.OkEmoji} Администратор **{e.User.Username}#{e.User.Discriminator}** успешно отклонил запрос на " +
                            $"создание корабля **{name}**!");

                        e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                            $"Администратор {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) отклонил создание приватного корабля {name}.",
                            DateTime.Now);
                    }
                    else
                    {
                        return;
                    }
                }
            }

        }
    }
}
