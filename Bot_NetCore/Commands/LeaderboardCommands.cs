using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;
using SeaOfThieves.Entities;

namespace SeaOfThieves.Commands
{
    [Group("leaderboard")]
    [Description("Команды лидерборда\n" +
                 "!help [Команда] для описания команды")]
    public class LeaderboardCommands
    {
        [Command("create")]
        [Description("Команда для создания/обновления лидерборда")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task Create(CommandContext ctx)
        {
            await ctx.Channel.DeleteMessageAsync(await ctx.Channel.GetMessageAsync(ctx.Channel.LastMessageId));

            await UpdateLeaderboard(ctx.Guild);
        }

        [Command("showall")]
        [Description("Выводит список всех пригласивших, в том числе и спрятанных")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task ShowAll(CommandContext ctx)
        {
            await ctx.Channel.DeleteMessageAsync(await ctx.Channel.GetMessageAsync(ctx.Channel.LastMessageId));

            var interactivity = ctx.Client.GetInteractivityModule();

            List<string> inviters = new List<string>();

            InviterList.Inviters.ToList()
                .OrderByDescending(x => x.Value.Referrals.Count).ToList()
                .ForEach(async x =>
                {
                    try
                    {
                        var inviter = await ctx.Guild.GetMemberAsync(x.Key);
                        string state = x.Value.Active == true ? "Активен" : "Отключен";
                        inviters.Add($"{inviter.DisplayName}#{inviter.Discriminator} пригласил {x.Value.Referrals.Count} Отображение: {state}");
                    }
                    catch (NotFoundException)
                    {
                        inviters.Add("Пользователь не найден");
                    }
                });

            var inviters_pagination = Utility.GeneratePagesInEmbeds(inviters, "Полный список рефералов");

            await interactivity.SendPaginatedMessage(ctx.Channel, ctx.User, inviters_pagination, timeoutoverride: TimeSpan.FromMinutes(5));
        }

        [Command("listinvites")]
        [Description("Выводит список приглашенных пользователем участников")]
        [Hidden]
        public async Task ListInvites(CommandContext ctx, DiscordMember member)
        {
            if (!Bot.IsModerator(ctx.Member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }
            var responceMsg = await ctx.RespondAsync("Загрузка пользователей...");
            try
            {
                var interactivity = ctx.Client.GetInteractivityModule();

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

                await responceMsg.DeleteAsync();
                await interactivity.SendPaginatedMessage(ctx.Channel, ctx.User, referrals_pagination, timeoutoverride: TimeSpan.FromMinutes(5));
            }
            catch (KeyNotFoundException)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не был найден указанный участник!");
            }
        }

        [Command("updatemember")]
        [Description("Обновляет статус отображения пользователя в leaderboard")]
        [RequirePermissions(Permissions.Administrator)]
        [Hidden]
        public async Task UpdateMember(CommandContext ctx, [Description("Участник")] DiscordMember member)
        {
            await ctx.Channel.DeleteMessageAsync(await ctx.Channel.GetMessageAsync(ctx.Channel.LastMessageId));

            InviterList.Inviters.Where(x => x.Key == member.Id).ToList()
                .ForEach(x => x.Value.UpdateState(!x.Value.Active));
            InviterList.SaveToXML(Bot.BotSettings.InviterXML);

            await UpdateLeaderboard(ctx.Guild);
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
                .Take(10).ToList();

            var embed = new DiscordEmbedBuilder
            {
                Color = new DiscordColor("#FF0066"),
                Title = $"Топ рефералов за {DateTime.UtcNow.ToString("MMMM", new CultureInfo("ru-RU"))}",
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
    }
}
