using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Attributes;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
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
            public async Task WhoIs(CommandContext ctx, [Description("Пользователь"), RemainingText] DiscordUser user)
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

                    //Сбор информации в переменные
                    var ban = GetBansInfo(user.Id);
                    var warnings = WarnSQL.GetForUser(user.Id).Count;
                    var reports = ReportSQL.GetForUser(user.Id);
                    var webUser = WebUser.GetByDiscordId(user.Id);

                    //Создание эмбеда
                    var embed = new DiscordEmbedBuilder();
                    embed.WithAuthor($"{user.Username}#{user.Discriminator}", iconUrl: user.AvatarUrl);
                    embed.WithThumbnail(user.AvatarUrl);

                    //Статус на сервере
                    if (ban != null)
                    {
                        embed.WithColor(new DiscordColor("#c0392b"));
                        embed.WithDescription($"Забанен до {ban}.");
                    }
                    else if (member == null)
                    {
                        embed.WithColor(new DiscordColor("#e67e22"));
                        embed.WithDescription("Не является участником.");
                    }
                    else
                    {
                        embed.WithColor(new DiscordColor("#27ae60"));
                        embed.WithDescription("Участник сервера.");
                    }


                    //1 Row - ID, Username
                    embed.AddFieldOrDefault("ID", user.Id.ToString(), true);
                    if (member != null)
                    {

                        embed.AddFieldOrDefault("Имя на сервере", member.Mention, true);
                    }
                    embed.NewInlineRow();

                    //2 Row - Creation and join dates
                    embed.AddFieldOrDefault("Создан", user.CreationTimestamp.ToString("HH:mm:ss \n dd.MM.yyyy"), true);
                    if (member != null)
                    {
                        embed.AddFieldOrDefault("Присоединился", member.JoinedAt.ToString("HH:mm:ss \n dd.MM.yyyy"), true);
                    }
                    embed.NewInlineRow();

                    //3 Row - WebUser info
                    if (webUser != null)
                    {
                        embed.AddFieldOrDefault("Привязка", "Да", true);
                        embed.AddFieldOrEmpty("Страна", GetCountryFlag(webUser.LastIp), true);
                        if (!string.IsNullOrEmpty(webUser.LastXbox))
                            embed.AddFieldOrDefault("Xbox", webUser.LastXbox.ToString(), true);
                        else
                            embed.AddFieldOrDefault("Xbox", "Нет", true);
                    }
                    embed.NewInlineRow();

                    //4 Row - Donate info
                    embed.AddFieldOrReplace("Донат", GetDonationInfo(user.Id), "Нет", true);
                    embed.AddFieldOrReplace("Подписка", GetSubscriptionInfo(user.Id), "Нет", true);
                    embed.AddFieldOrReplace("Приватный корабль", GetPrivateShip(user.Id), "Нет", true);
                    embed.NewInlineRow();

                    //5 Row - Reports info
                    embed.AddFieldOrDefault("Предупреждения", $":pencil: {warnings}", true);
                    embed.AddFieldOrDefault("Правила", GetCodexInfo(reports, member), true);
                    embed.AddFieldOrDefault("Правила рейда", GetFleetCodexInfo(reports, member), true);
                    embed.AddFieldOrDefault("Мут", $"{GetMutesInfo(reports)}", true);
                    embed.AddFieldOrDefault("Голосовой мут", $"{GetVoiceMutesInfo(reports)}", true);
                    embed.NewInlineRow();

                    //6 Row - Note
                    if (Note.Notes.ContainsKey(user.Id))
                        embed.AddFieldOrDefault("Заметка", Note.Notes[user.Id].Content);

                    embed.WithFooter("(*) Не принял после разблокировки");

                    var message = await ctx.RespondAsync(embed: embed.Build());

                    //Реакция на вывод сообщения с предупреждениями
                    if (warnings > 0)
                    {
                        var interactivity = ctx.Client.GetInteractivity();

                        var emoji = DiscordEmoji.FromName(ctx.Client, ":pencil:");

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

            private static string GetBansInfo(ulong userId)
            {
                var userBans = BanSQL.GetForUser(userId).Where(x => x.UnbanDateTime > DateTime.Now);
                if (userBans.Any())
                {
                    return $"{userBans.OrderBy(x => x.UnbanDateTime).FirstOrDefault().UnbanDateTime}";
                }
                return null;
            }

            private static string GetDonationInfo(ulong userId)
            {
                //Донат
                if (Donator.Donators.ContainsKey(userId)) 
                    return Donator.Donators[userId].Balance.ToString();
                return null;
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0071:Simplify interpolation", Justification = "<Pending>")]
            private static string GetSubscriptionInfo(ulong userId)
            {
                //Подписка
                if (Subscriber.Subscribers.ContainsKey(userId))
                {
                    var subscriber = Subscriber.Subscribers[userId];
                    var length = subscriber.SubscriptionEnd - DateTime.Now;

                    return $"{subscriber.SubscriptionEnd:HH:mm:ss \n dd.MM.yyyy} ({Utility.ToCorrectCase(length, Utility.TimeUnit.Days)})";
                }
                return null;
            }

            private static string GetPrivateShip(ulong userId)
            {
                //Приватный корабль
                foreach (var ship in ShipList.Ships.Values)
                    foreach (var shipMember in ship.Members.Values)
                        if (shipMember.Type == MemberType.Owner && shipMember.Id == userId)
                        {
                            return ship.Name;
                        }
                return null;
            }

            private static string GetCodexInfo(List<ReportSQL> reports, DiscordMember member)
            {
                if (member is { } && member.Roles.Any(x => x.Id == Bot.BotSettings.CodexRole))
                    return "Принял";
                foreach (var report in reports)
                {
                    if (report.ReportType == ReportType.CodexPurge)
                    {
                        if (report.ReportEnd <= DateTime.Now)
                            return "Не принял*";
                        else if (report.ReportEnd > DateTime.Now)
                            return Utility.FormatTimespan(DateTime.Now - report.ReportEnd);
                    }
                }
                return "Не принял";
            }

            private static string GetFleetCodexInfo(List<ReportSQL> reports, DiscordMember member)
            {
                if (member is { } && member.Roles.Any(x => x.Id == Bot.BotSettings.FleetCodexRole))
                    return "Принял";
                foreach (var report in reports)
                {
                    if (report.ReportType == ReportType.FleetPurge)
                    {
                        if (report.ReportEnd <= DateTime.Now)
                            return "Не принял*";
                        else if (report.ReportEnd > DateTime.Now)
                            return Utility.FormatTimespan(DateTime.Now - report.ReportEnd);
                    }
                }
                return "Не принял";
            }

            private static string GetMutesInfo(List<ReportSQL> reports)
            {
                foreach (var report in reports)
                    if (report.ReportType == ReportType.Mute && report.ReportEnd > DateTime.Now)
                        return Utility.FormatTimespan(DateTime.Now - report.ReportEnd);
                return null;
            }

            private static string GetVoiceMutesInfo(List<ReportSQL> reports)
            {
                foreach (var report in reports)
                    if (report.ReportType == ReportType.VoiceMute && report.ReportEnd > DateTime.Now)
                        return Utility.FormatTimespan(DateTime.Now - report.ReportEnd);
                return null;
            }

            private static string GetCountryFlag(string ip)
            {
                using (var reader = new DatabaseReader("data/GeoLite2-City.mmdb"))
                {
                    try
                    {
                        var city = reader.City(ip);
                        return $":flag_{city.Country.IsoCode.ToLower()}:";
                    }
                    catch (AddressNotFoundException) { return null; }
                    catch (NullReferenceException) { return null; }
                }
            }
        }
    }
}
