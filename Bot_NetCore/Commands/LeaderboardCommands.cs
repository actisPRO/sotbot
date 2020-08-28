using System;
using System.Collections.Generic;
using System.Globalization;
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
            [Description("Год фильтра **yy**")] int year = 0)
        {
            await ctx.Channel.TriggerTypingAsync();

            var interactivity = ctx.Client.GetInteractivity();

            DateTime dateFilter = DateTime.Now;

            List<string> inviters = new List<string>();

            var filteredData = InviterList.Inviters.OrderByDescending(x => x.Value.Referrals.Count)
                .Where(x => x.Value.Referrals.Count > 0)
                .ToDictionary(x => x.Key, x => x.Value);

            if (month != 0)
            {
                if (month > 12)
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось определить формат даты!");
                    return;
                }

                var date = "";

                if (year != 0)
                    date = $"{month}/{year}";
                else
                    date = $"{month}/{DateTime.Now:yy}";

                dateFilter = DateTime.ParseExact(date, "M/yy", CultureInfo.InvariantCulture);
                filteredData = filteredData.OrderByDescending(x => x.Value.Referrals.Where(x => x.Value.Date.Month == dateFilter.Date.Month && x.Value.Date.Year == dateFilter.Date.Year).ToList().Count)
                        .Where(x => x.Value.Referrals.Where(x => x.Value.Date.Month == dateFilter.Date.Month && x.Value.Date.Year == dateFilter.Date.Year).ToList().Count != 0)
                        .ToDictionary(x => x.Key, x => x.Value);
            }

            filteredData.ToList().ForEach(async x =>
            {
                try
                {
                    var inviter = await ctx.Guild.GetMemberAsync(x.Key);
                    var referrals = 0;
                    if (month == 0)
                        referrals = x.Value.Referrals.Count;
                    else
                        referrals = x.Value.Referrals.Where(x => x.Value.Date.Month == dateFilter.Date.Month && x.Value.Date.Year == dateFilter.Date.Year).ToList().Count;

                    var state = x.Value.Active == true ? "Активен" : "Отключен";
                    var ignored = x.Value.Ignored == true ? "Активен" : "Отключен";
                    inviters.Add($"{ inviter.DisplayName}#{inviter.Discriminator} пригласил {referrals}" +
                                 $"\nОтображение: {state} " +
                                 $"| Подсчет в конце месяца: {ignored}");
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
        public async Task ListInvites(CommandContext ctx, DiscordMember member)
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
        }


        [Command("updatehidden")]
        [Description("Обновить статус учитывания при выдаче наград в конце месяца")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task UpdateIgnored(CommandContext ctx, [Description("Участник")] DiscordMember member)
        {
            await ctx.Channel.TriggerTypingAsync();

            InviterList.Inviters.Where(x => x.Key == member.Id).ToList()
                .ForEach(x => x.Value.UpdateIgnored(!x.Value.Ignored));
            InviterList.SaveToXML(Bot.BotSettings.InviterXML);
        }

        public static async Task<Task> UpdateLeaderboard(DiscordGuild guild)
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

            return Task.CompletedTask;
        }

        private void CheckAndUpdateTopInviters(Dictionary<ulong, Inviter> topInviters)
        {
            //Read data

            //Month: DateTime.Now.ToString("MMMM", new CultureInfo("ru-RU"))

            //Save data if needed
            if (true) //TODO: Condition
            {
                var doc = new XDocument();
                var root = new XElement("topLeaderboard");

                root.Add(new XElement("updatedRolesMonth", DateTime.Now.ToString("MMMM", new CultureInfo("ru-RU"))));
            }
        }
    }
}
