using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot_NetCore.Attributes;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;

namespace Bot_NetCore.Commands
{
    [RequireGuild]
    public class WhoisCommands : BaseCommandModule
    {
        [Group("whois")]
        [Description("Информация о пользователе.")]
        [RequireCustomRole(RoleType.FleetCaptain)]
        [RequireGuild]
        public class WhoisCommand : BaseCommandModule
        {
            [GroupCommand]
            public async Task WhoIs(CommandContext ctx, [Description("Пользователь")] DiscordUser user)
            {
                await ctx.TriggerTypingAsync();

                try
                {
                    DiscordMember member = null;
                    try
                    {
                        member = await ctx.Guild.GetMemberAsync(user.Id);
                    }
                    catch (NotFoundException)
                    {
                        // is not a member of the guild
                    }

                    var embed = new DiscordEmbedBuilder();
                    embed.WithColor(DiscordColor.Orange);
                    embed.WithAuthor($"{user.Username}#{user.Discriminator}", iconUrl: user.AvatarUrl);
                    embed.AddField("ID", user.Id.ToString(), true);

                    //Баны
                    var userBans = BanSQL.GetForUser(user.Id).Where(x => x.UnbanDateTime > DateTime.Now);
                    if (userBans.Any())
                    {
                        embed.AddField("Бан", $"{userBans.OrderBy(x => x.UnbanDateTime).FirstOrDefault().UnbanDateTime}");
                        embed.WithColor(DiscordColor.Red);
                    }

                    if (member == null)
                    {
                        embed.WithDescription("Покинул сервер");
                    }
                    else
                    {
                        embed.AddField("Имя на сервере", member.DisplayName, true);
                        embed.AddField("Присоединился", member.JoinedAt.ToString("HH:mm:ss dd.MM.yyyy"));
                        embed.WithColor(DiscordColor.Green);
                    }

                    //Предупреждения
                    var warnings = WarnSQL.GetForUser(user.Id).Count;

                    // Web
                    var webUser = WebUser.GetByDiscordId(user.Id);
                    if (webUser == null)
                    {
                        embed.AddField("Привязка", "Нет", true);
                    }
                    else
                    {
                        embed.AddField("Привязка", "Да", true);
                        // Country
                        using (var reader = new DatabaseReader("data/GeoLite2-City.mmdb"))
                        {
                            try
                            {
                                var city = reader.City(webUser.LastIp);
                                embed.AddField("Страна", $":flag_{city.Country.IsoCode.ToLower()}:", true);
                            }
                            catch (AddressNotFoundException) { }
                            catch (NullReferenceException) { }
                        }

                        if (!string.IsNullOrEmpty(webUser.LastXbox))
                            embed.AddField("Xbox", webUser.LastXbox, true);
                    }

                    //Emoji используется при выводе списка предупреждений.
                    var emoji = DiscordEmoji.FromName(ctx.Client, ":pencil:");

                    embed.AddField("Предупреждения", $"{emoji} {warnings}", true);

                    var mute = "Нет";

                    var codex = "Не принял";
                    var hasPurge = false;

                    var fleetCodex = "Не принял";
                    var hasFleetPurge = false;

                    foreach (var purge in ReportSQL.GetForUser(user.Id))
                    {
                        if (purge.ReportType == ReportType.Mute)
                            if (purge.ReportEnd > DateTime.Now)
                                mute = Utility.FormatTimespan(DateTime.Now - purge.ReportEnd);

                        if (purge.ReportType == ReportType.CodexPurge)
                        {
                            if (purge.ReportEnd > DateTime.Now)
                            {
                                hasPurge = true;
                                codex = Utility.FormatTimespan(DateTime.Now - purge.ReportEnd);
                            }
                            else if (!hasPurge && purge.ReportEnd <= DateTime.Now)
                                codex = "Не принял*";
                        }

                        if (purge.ReportType == ReportType.FleetPurge)
                        {
                            if (purge.ReportEnd > DateTime.Now)
                            {
                                hasFleetPurge = true;
                                fleetCodex = Utility.FormatTimespan(DateTime.Now - purge.ReportEnd);
                            }
                            else if (!hasFleetPurge && purge.ReportEnd <= DateTime.Now)
                                fleetCodex = "Не принял*";
                        }
                    }

                    if (BlacklistEntry.IsBlacklisted(user.Id)) fleetCodex = "В ЧС";

                    if (member is { } && member.Roles.Any(x => x.Id == Bot.BotSettings.CodexRole))
                        codex = "Принял";
                    if (member is { } && member.Roles.Any(x => x.Id == Bot.BotSettings.FleetCodexRole))
                        fleetCodex = "Принял";

                    if (member != null)
                    {
                        embed.AddField("Правила", codex, true);
                        embed.AddField("Правила рейда", fleetCodex, true);
                    }

                    //Мут
                    embed.AddField("Мут", mute, true);

                    //Донат
                    var donate = 0;
                    if (Donator.Donators.ContainsKey(user.Id)) donate = Donator.Donators[user.Id].Balance;
                    embed.AddField("Донат", donate.ToString(), true);

                    //Подписка
                    var subscription = "Нет";
                    if (Subscriber.Subscribers.ContainsKey(user.Id))
                    {
                        var subscriber = Subscriber.Subscribers[user.Id];
                        var length = subscriber.SubscriptionEnd - DateTime.Now;

                        subscription =
                            $"{subscriber.SubscriptionEnd:HH:mm:ss dd.MM.yyyy} ({Utility.ToCorrectCase(length, Utility.TimeUnit.Days)})";
                    }
                    embed.AddField("Подписка", subscription, true);

                    //Приватный корабль
                    var privateShip = "Нет";
                    foreach (var ship in ShipList.Ships.Values)
                        foreach (var shipMember in ship.Members.Values)
                            if (shipMember.Type == MemberType.Owner && shipMember.Id == user.Id)
                            {
                                privateShip = ship.Name;
                                break;
                            }
                    embed.AddField("Владелец приватного корабля", privateShip);

                    //Заметка
                    var note = "Отсутствует";
                    if (Note.Notes.ContainsKey(user.Id))
                        note = Note.Notes[user.Id].Content;
                    embed.AddField("Заметка", note, false);

                    embed.WithFooter("(*) Не принял после разблокировки");

                    var message = await ctx.RespondAsync(embed: embed.Build());

                    //Реакция на вывод сообщения с предупреждениями
                    if (warnings > 0)
                    {
                        var interactivity = ctx.Client.GetInteractivity();

                        await message.CreateReactionAsync(emoji);

                        var em = await interactivity.WaitForReactionAsync(xe => xe.Emoji == emoji, message, ctx.User, TimeSpan.FromSeconds(60));

                        if (!em.TimedOut)
                        {
                            await ctx.TriggerTypingAsync();

                            var command = $"whois wl {user.Id}";

                            var cmds = ctx.CommandsNext;

                            // Ищем команду и извлекаем параметры.
                            var cmd = cmds.FindCommand(command, out var customArgs);

                            // Создаем фейковый контекст команды.
                            var fakeContext = cmds.CreateFakeContext(ctx.Member, ctx.Channel, command, ctx.Prefix, cmd, customArgs);

                            // Выполняем команду за пользователя.
                            await cmds.ExecuteCommandAsync(fakeContext);
                        }
                        else
                        {
                            await message.DeleteAllReactionsAsync();
                        }

                    }
                }
                catch (NotFoundException)
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не найден.");
                }
            }

            [Command("wlist")]
            [Aliases("wl")]
            [Description("Выводит список предупреждений.")]
            public async Task WList(CommandContext ctx, [Description("Пользователь")] DiscordUser user)
            {
                var warnings = WarnSQL.GetForUser(user.Id);
                int count = warnings.Count;

                if (count == 0)
                {
                    await ctx.RespondAsync("*Предупреждения отсутствуют.*");
                    return;
                }

                var interactivity = ctx.Client.GetInteractivity();

                List<Utility.CustomEmbedField> fields = new List<Utility.CustomEmbedField>();

                for (var i = count; i > 0; i--)
                {
                    var warn = warnings[i - 1];
                    fields.Add(new Utility.CustomEmbedField($"*{i}*.",
                        $"**ID:** {warn.Id}\n**Причина:** {warn.Reason} \n **Выдан:** {await ctx.Client.GetUserAsync(warn.Moderator)} {warn.Date.ToShortDateString()}"));
                }


                var fields_pagination = Utility.GeneratePagesInEmbeds(fields, "Список варнов.");

                if (fields_pagination.Count() > 1)
                    await interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, fields_pagination, timeoutoverride: TimeSpan.FromMinutes(5));
                else
                    await ctx.RespondAsync(embed: fields_pagination.First().Embed);
            }

            [Command("note")]
            [Description("Показать заметку.")]
            public async Task GetNote(CommandContext ctx, [Description("Пользователь")] DiscordUser user)
            {
                if (!Note.Notes.ContainsKey(user.Id))
                {
                    await ctx.RespondAsync("У пользователя нет заметки!");
                    return;
                }

                var embed = new DiscordEmbedBuilder();

                embed.WithAuthor($"{user.Username}#{user.Discriminator}", iconUrl: user.AvatarUrl);
                embed.WithColor(DiscordColor.Blurple);

                var note = "Отсутствует";
                note = Note.Notes[user.Id].Content;
                embed.AddField("Заметка", note, false);

                await ctx.RespondAsync(embed: embed.Build());
            }

            [Command("addnote")]
            [Description("Добавить заметку.")]
            public async Task AddNote(CommandContext ctx, [Description("Пользователь")] DiscordUser user, [Description("Заметка")][RemainingText] string content)
            {
                string oldContent = null;
                if (Note.Notes.ContainsKey(user.Id))
                    oldContent = Note.Notes[user.Id].Content;

                var note = new Note(user.Id, content);
                Note.Save(Bot.BotSettings.NotesXML);

                var message = $"{Bot.BotSettings.OkEmoji} Успешно добавлена заметка о пользователе!";
                if (oldContent != null) message += " **Предыдущая заметка:** " + oldContent;

                await ctx.RespondAsync(message);
            }

            [Command("deletenote")]
            [Aliases("dnote")]
            [Description("Удалить заметку.")]
            public async Task DeleteNote(CommandContext ctx, [Description("Пользователь")] DiscordUser user)
            {
                if (!Note.Notes.ContainsKey(user.Id))
                {
                    await ctx.RespondAsync("У пользователя нет заметки!");
                    return;
                }

                Note.Notes.Remove(user.Id);

                await ctx.RespondAsync($"Успешно удалена заметка пользователя <@{user.Id}>!");
            }

            [Command("xbox")]
            [Description("Поиск пользователя по нику Xbox.")]
            public async Task Xbox(CommandContext ctx, [Description("Пользователь")][RemainingText] string xbox)
            {
                // Web
                var users = WebUser.GetUsersByXbox(xbox);
                if (users.Count == 0)
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователи не найдены.");
                }
                else
                {
                    var msg = $"**Найденные пользователи по нику {xbox}:**";
                    foreach (var user in users)
                        msg += $"\n{user.Username} ({user.UserId})";
                    await ctx.RespondAsync(msg);
                }
            }
        }
    }
}
