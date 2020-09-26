using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;

namespace Bot_NetCore.Commands
{
    [Group("leaderboard")]
    [Aliases("lb")]
    [Description("Команды лидерборда.")]
    [RequirePermissions(Permissions.KickMembers)]
    public class LeaderboardCommands : BaseCommandModule
    {
        [Command("create")]
        [Description("Команда для создания/обновления лидерборда")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Create(CommandContext ctx)
        {
            await ctx.Channel.DeleteMessageAsync(await ctx.Channel.GetMessageAsync(ctx.Channel.LastMessageId));

            await UpdateLeaderboard(ctx.Guild);
        }

        [Command("showall")]
        [Description("Выводит список всех пригласивших, в том числе и спрятанных")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task ShowAll(CommandContext ctx,
            [Description("Фильтр поиска по месяцу в формате **mm**")] int month = 0,
            [Description("Год фильтра **yy**")] int year = 0,
            [Description("Количество")] int elements = 0)
        {
            await ctx.Channel.TriggerTypingAsync();

            var interactivity = ctx.Client.GetInteractivity();

            DateTime dateFilter = DateTime.Now;

            List<string> inviters = new List<string>();

            var filteredData = InviterList.Inviters.OrderByDescending(x => x.Value.ActiveCount)
                .Where(x => x.Value.ActiveCount > 0)
                .ToDictionary(x => x.Key, x => x.Value);

            if (month != 0)
            {
                if (month > 12)
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось определить формат даты!");
                    return;
                }

                if (year == 0) year = DateTime.Now.Year - 2000;
                var date = $"{month}/{year}";

                dateFilter = DateTime.ParseExact(date, "M/yy", CultureInfo.InvariantCulture);
                filteredData = filteredData.OrderByDescending(x => x.Value.Referrals.Where(x => x.Value.Active == true && x.Value.Date.Month == dateFilter.Date.Month && x.Value.Date.Year == dateFilter.Date.Year).ToList().Count)
                        .Where(x => x.Value.Referrals.Where(x => x.Value.Active == true && x.Value.Date.Month == dateFilter.Month && x.Value.Date.Year == dateFilter.Year).ToList().Count != 0)
                        .ToDictionary(x => x.Key, x => x.Value);
            }

            filteredData.Take(elements != 0 ? Math.Max(elements + 1, inviters.Count) : inviters.Count).ToList()
                .ForEach(async x =>
                {
                    try
                    {
                        var inviter = await ctx.Guild.GetMemberAsync(x.Key);
                        var referrals = 0;
                        if (month == 0)
                            referrals = x.Value.ActiveCount;
                        else
                            referrals = x.Value.Referrals.Where(x => x.Value.Active == true && x.Value.Date.Month == dateFilter.Date.Month && x.Value.Date.Year == dateFilter.Date.Year).ToList().Count;

                        var state = x.Value.Active == true ? "Активен" : "Отключен";
                        var ignored = x.Value.Ignored == true ? "Отключен" : "Активен";
                        inviters.Add($"{ inviter.DisplayName}#{inviter.Discriminator} пригласил {referrals}" +
                                        $"\nОтображение: {state} " +
                                        $"| Подсчет в конце месяца: {ignored} \n");
                    }
                    catch (NotFoundException)
                    {
                        inviters.Add("Пользователь не найден");
                    }
                });

            if (filteredData.Count > 0)
            {

                var inviters_pagination = Utility.GeneratePagesInEmbeds(inviters, "Полный список рефералов");

                if (inviters_pagination.Count() > 1)
                    await interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, inviters_pagination, timeoutoverride: TimeSpan.FromMinutes(5));
                else
                    await ctx.RespondAsync(embed: inviters_pagination.First().Embed);
            }
            else
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось найти пользователей в списке!");
            }
        }

        [Command("listinvites")]
        [Description("Выводит список приглашенных пользователем участников")]
        public async Task ListInvites(CommandContext ctx, [Description("Участник")] DiscordMember member)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }
            await ctx.Channel.TriggerTypingAsync();

            try
            {
                var interactivity = ctx.Client.GetInteractivity();

                var inviter = InviterList.Inviters[member.Id].Referrals.Values.Where(x => x.Active).ToList()
                    .OrderByDescending(x => x.Date.Month);

                List<string> referrals = new List<string>();

                foreach (var referral in inviter)
                {
                    try
                    {
                        var refMember = await ctx.Guild.GetMemberAsync(referral.Id);
                        referrals.Add($"{refMember.Id} - {refMember.Username}#{refMember.Discriminator} ({refMember.DisplayName})");
                    }
                    catch (NotFoundException)
                    {
                        referrals.Add($"Пользователь не найден");
                    }

                }

                var referrals_pagination = Utility.GeneratePagesInEmbeds(referrals, $"Список приглашенных пользователем {member.DisplayName}");

                if (referrals_pagination.Count() > 1)
                    await interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, referrals_pagination, timeoutoverride: TimeSpan.FromMinutes(5));
                else
                    await ctx.RespondAsync(embed: referrals_pagination.First().Embed);
            }
            catch (KeyNotFoundException)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не был найден указанный участник!");
            }
        }

        [Command("updateactive")]
        [Description("Обновляет статус отображения пользователя в leaderboard")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task UpdateActive(CommandContext ctx, [Description("Участник")] DiscordMember member)
        {
            await ctx.Channel.TriggerTypingAsync();

            InviterList.Inviters.Where(x => x.Key == member.Id).ToList()
                .ForEach(x => x.Value.UpdateState(!x.Value.Active));
            InviterList.SaveToXML(Bot.BotSettings.InviterXML);

            await UpdateLeaderboard(ctx.Guild);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно обновлено отображение {member.Mention}.");
        }


        [Command("updateignored")]
        [Description("Обновить статус учитывания при выдаче наград в конце месяца")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task UpdateIgnored(CommandContext ctx, [Description("Участник")] DiscordMember member)
        {
            await ctx.Channel.TriggerTypingAsync();

            InviterList.Inviters.Where(x => x.Key == member.Id).ToList()
                .ForEach(x => x.Value.UpdateIgnored(!x.Value.Ignored));
            InviterList.SaveToXML(Bot.BotSettings.InviterXML);

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно обновлено учитывание в конце месяца {member.Mention}");
        }

        [Command("lastmonth")]
        [Description("Выводит количество приглашенных за последний месяц")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task LastMonth(CommandContext ctx, [Description("Участник")] DiscordMember member)
        {
            await ctx.Channel.TriggerTypingAsync();

            if (InviterList.Inviters.ContainsKey(member.Id))
                await ctx.RespondAsync($"Количество приглашенных: {InviterList.Inviters[member.Id].LastMonthActiveCount}");
            else
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Участник не найден");
        }

        public static async Task UpdateLeaderboard(DiscordGuild guild)
        {
            var channel = guild.GetChannel(Bot.BotSettings.InvitesLeaderboardChannel);

            //Фильтруем топ 10 за последний месяц
            var currentMonthInviters = InviterList.Inviters.ToList()
                .OrderByDescending(x => x.Value.CurrentMonthActiveCount).ToList()
                .FindAll(x =>
                {
                    if (!x.Value.Active)
                        return false;
                    //guild.GetMemberAsync(x.Key);
                    return true;
                })
                .Take(10).ToDictionary(x => x.Key, x => x.Value);

            var embed = new DiscordEmbedBuilder
            {
                Color = new DiscordColor("#FF0066"),
                Title = $"Топ рефералов за {DateTime.Now.ToString("MMMM", new CultureInfo("ru-RU"))}",
            };

            int i = 1;
            foreach (var el in currentMonthInviters)
            {
                if (el.Value.CurrentMonthActiveCount > 0)
                {
                    var userString = "";
                    try
                    {
                        var user = await guild.GetMemberAsync(el.Key);
                        userString = $"{user.Username}#{user.Discriminator}";
                    }
                    catch (NotFoundException)
                    {
                        //Пользователь не найден
                        userString = $"Пользователь покинул сервер";
                    }

                    var place = i switch
                    {
                        1 => "🥇",
                        2 => "🥈",
                        3 => "🥉",
                        _ => $"{i}."
                    };

                    embed.AddField(
                        $"{place} {userString}",
                        $"пригласил {el.Value.CurrentMonthActiveCount} пользователей");
                    i++;
                }
            }

            embed.WithFooter("Чтобы попасть в топ, создайте собственную ссылку приглашения");

            //Публикуем и проверяем на уже существующую таблицу топ 10
            var messages = await channel.GetMessagesAsync();
            ulong messageId = 0;
            if (messages.Count > 0)
                messageId = messages.ToList().Where(x => (x.Author.Id == guild.CurrentMember.Id)).First().Id;

            if (messageId == 0)
                await channel.SendMessageAsync(embed: embed.Build());
            else
                await channel.GetMessageAsync(messageId).Result.ModifyAsync(embed: embed.Build());

            //Проверка только в первый день месяца
            //if(DateTime.Now.Day == 1)
            await CheckAndUpdateTopInvitersAsync(guild);
        }

        /// <summary>
        ///     Проверяет и обновляет награды топ пригласивших за предыдущий месяц.
        /// </summary>
        /// <param name="guild"></param>
        /// <returns></returns>
        public static async Task CheckAndUpdateTopInvitersAsync(DiscordGuild guild)
        {

            //Read data
            var fileName = "generated/top_inviters.xml";
            var doc = XDocument.Load(fileName);
            var root = doc.Root;

            //Check for a new month, generate new top inviters and save them, then grant a role and subscribe.
            if (DateTime.ParseExact(root.Element("lastMonth").Value, "M/yy", CultureInfo.InvariantCulture).Month != DateTime.Now.Month - 1)
            {
                //Read old inviters id's
                var oldTopInviters = root.Element("inviters").Elements().Select(x => Convert.ToUInt64(x.Attribute("id").Value));

                //Remove role from old inviters
                foreach (var inviter in oldTopInviters)
                    try
                    {
                        var member = await guild.GetMemberAsync(inviter);
                        var role = guild.GetRole(Bot.BotSettings.TopMonthRole);
                        if (member.Roles.Contains(role))
                            await member.RevokeRoleAsync(role);
                    }
                    catch (NotFoundException)
                    {
                        //Пользователь не найден.
                    }

                //New top inviters
                var topInviters = InviterList.Inviters.ToList()
                    .Where(x => !x.Value.Ignored && x.Value.Active)
                    .OrderByDescending(x => x.Value.LastMonthActiveCount)
                    .ToDictionary(x => x.Key, x => x.Value);

                Dictionary<ulong, Inviter> topThreeInviters = new Dictionary<ulong, Inviter>();
                foreach (var inviter in topInviters)
                {
                    try
                    {
                        await guild.GetMemberAsync(inviter.Key);
                        topThreeInviters.Add(inviter.Key, inviter.Value);
                        if (topThreeInviters.Count >= 3)
                            break;
                    }
                    catch (NotFoundException)
                    {
                        // Do nothing
                    }
                }

                var modLogMessage = "";

                //Grant role and sub to new top inviters
                foreach (var inviter in topThreeInviters)
                    try
                    {
                        var member = await guild.GetMemberAsync(inviter.Key);

                        modLogMessage += $"{member.DisplayName}#{member.Discriminator} \n";
                        
                        var role = guild.GetRole(Bot.BotSettings.TopMonthRole);
                        await member.GrantRoleAsync(role);

                        //Grant sub for 30 days
                        var timeSpan = Utility.TimeSpanParse("30d");

                        var start = DateTime.Now;

                        if (Subscriber.Subscribers.ContainsKey(member.Id))
                            start = Subscriber.Subscribers[member.Id].SubscriptionEnd;

                        var end = start + timeSpan;

                        var styleRole = await DonatorCommands.GetPrivateRoleAsync(guild, member);
                        await member.GrantRoleAsync(styleRole);

                        var sub = new Subscriber(member.Id, SubscriptionType.Premium, start, end, styleRole.Id, new List<ulong>());

                        Subscriber.Save(Bot.BotSettings.SubscriberXML);

                        await member.SendMessageAsync(
                            $"Спасибо за поддержку нашего сообщества! Вам выдана подписка за топ инвайты до: **{end:HH:mm:ss dd.MM.yyyy}**.\n" +
                            $"**Доступные возможности:**\n" +
                            $"• `{Bot.BotSettings.Prefix}d color hex-код цвета` — изменяет цвет вашего ника.\n" +
                            $"• `{Bot.BotSettings.Prefix}d rename` — изменяет название вашей роли донатера.\n" +
                            $"• `{Bot.BotSettings.Prefix}d roleadd` — выдаёт вам роль `💣☠️WANTED☠️💣`.\n" +
                            $"• `{Bot.BotSettings.Prefix}d rolerm` — снимает с вас роль `💣☠️WANTED☠️💣`.\n" +
                            $"• `{Bot.BotSettings.Prefix}d friend` — добавляет другу ваш цвет (до 5 друзей).\n" +
                            $"• `{Bot.BotSettings.Prefix}d unfriend` — убирает у друга ваш цвет.");
                        
                    }
                    catch (NotFoundException)
                    {
                        //Пользователь не найден.
                    }

                await guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                    "**Пользователям выдана подписка за топ инвайты** \n" +
                    $"{modLogMessage}");

                //Save data
                SaveTopInviters(topThreeInviters, fileName);
            }
        }

        /// <summary>
        ///     Сохраняет данные о топ пригласивших за предыдущий месяц.
        /// </summary>
        /// <param name="topInviters"></param>
        /// <param name="fileName"></param>
        private static void SaveTopInviters(Dictionary<ulong, Inviter> topInviters, string fileName)
        {
            var doc = new XDocument();
            var root = new XElement("topLeaderboard");

            root.Add(new XElement("lastMonth", $"{DateTime.Now.AddMonths(-1):MM}/{DateTime.Now.AddMonths(-1):yy}"));

            var invElement = new XElement("inviters");

            foreach (var inviter in topInviters.Values)
            {
                var iElement = new XElement(
                                    "inviter",
                                    new XAttribute("id", inviter.InviterId),
                                    new XAttribute("referrals", inviter.LastMonthActiveCount)
                                );
                invElement.Add(iElement);
            }

            root.Add(invElement);

            doc.Add(root);
            doc.Save(fileName);
        }
    }
}
