using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Attributes;
using Bot_NetCore.Entities;
using Bot_NetCore.Listeners;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;

namespace Bot_NetCore.Commands
{
    public class DmSupportCommands : BaseCommandModule
    {
        private static readonly IReadOnlyDictionary<SupportType, string> SupportEmoji = new Dictionary<SupportType, string>()
        {
            { SupportType.Admin, ":red_circle:"},
            { SupportType.Moderator, ":green_circle:"},
            { SupportType.Developer, ":white_circle:"},
            { SupportType.Donate, ":blue_circle:"},
            { SupportType.FleetCaptain, ":purple_circle:"},
            { SupportType.Events, ":orange_circle:"}
        };

        private static readonly IReadOnlyDictionary<SupportType, string> SupportNames = new Dictionary<SupportType, string>()
        {
            { SupportType.Admin, "Администрация"},
            { SupportType.Moderator, "Модерация"},
            { SupportType.Developer, "Поддержка бота"},
            { SupportType.Donate, "Донат/Реклама"},
            { SupportType.FleetCaptain, "Рейды"},
            { SupportType.Events, "Ивенты"}
        };

        [Command("support")]
        [Description("Создает запрос тикета")]
        [RequireDirectMessage]
        [Cooldown(1, 30, CooldownBucketType.User)]
        public async Task Support(CommandContext ctx)
        {
            if (SupportBlacklistEntry.IsBlacklisted(ctx.User.Id))
            {
                await ctx.RespondAsync("Создание тикетов заблокировано. Свяжитесь с администрацией для выяснения");
                return;
            }

            DmMessageListener.DmHandled.Add(ctx.User);

            try
            {
                var guild = await ctx.Client.GetGuildAsync(Bot.BotSettings.Guild);


                //Создание эмбеда тикета и заполнение по шаблону
                var ticketEmbed = new DiscordEmbedBuilder
                {
                    Title = "Sea of Thieves RU | Создание тикета",
                    Description = "Выберите категорию вашего запроса через реакцию‎\n‎",
                    Color = new DiscordColor("#e67e22")
                };

                ticketEmbed.AddField($"{SupportEmoji[SupportType.Admin]} {SupportNames[SupportType.Admin]}", "Связь с администрацией, используйте только для **ВАЖНЫХ** вопросов", true);
                ticketEmbed.AddField($"{SupportEmoji[SupportType.Moderator]} {SupportNames[SupportType.Moderator]}", "Вопросы по поводу модерации, нарушений и так далее. (Отвечают модераторы и администраторы)", true);
                ticketEmbed.AddField($"{SupportEmoji[SupportType.Developer]} {SupportNames[SupportType.Developer]}", "По вопросам бота, сайта, техническим вопросам, помощь с командами и их ошибками.", true);
                ticketEmbed.AddField($"{SupportEmoji[SupportType.Donate]} {SupportNames[SupportType.Donate]}", "По вопросам рекламы и при проблемах с донатами.", true);
                ticketEmbed.AddField($"{SupportEmoji[SupportType.FleetCaptain]} {SupportNames[SupportType.FleetCaptain]}", "По вопросам рейдов и нарушений в рейдах. (Ошибки при выдаче роли -> к `Разработчикам`)", true);
                ticketEmbed.AddField($"{SupportEmoji[SupportType.Events]} {SupportNames[SupportType.Events]}", "По вопросам ивентов на сервере. Как игровых так и внутри сервера.", true);

                ticketEmbed.WithFooter("‎\n" +
                    "При злоупотреблении системой тикетов вы будете заблокированы.\n" +
                    "Дождитесь загрузки всех вариантов ответа. У вас есть минута на выбор варианта");

                var ticketMessage = await ctx.RespondAsync(embed: ticketEmbed.Build());

                var interactivity = ctx.Client.GetInteractivity();

                //Создаем предложенные реакции.
                foreach (var emoji in SupportEmoji.Values)
                {
                    await ticketMessage.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, emoji));
                    await Task.Delay(400);
                }

                //Шаблон закрытого тикета
                var errorEmbed = new DiscordEmbedBuilder()
                {
                    Title = "Sea of Thieves RU | Создание тикета закрыто",
                    Description = "Произошла ошибка при создании тикета.",
                    Color = new DiscordColor("#e74c3c"),
                };
                errorEmbed.WithThumbnail(guild.IconUrl);
                errorEmbed.WithTimestamp(DateTime.Now);

                //Ждем одну из предложенных реакций. (Минута времени)
                var em = await interactivity.WaitForReactionAsync(x => SupportEmoji.Values.Contains(x.Emoji.GetDiscordName()), ctx.User, TimeSpan.FromSeconds(60));

                if (!em.TimedOut)
                {
                    new Task(async () =>
                    {
                        foreach (var emoji in SupportEmoji.Values)
                        {
                            await ticketMessage.DeleteOwnReactionAsync(DiscordEmoji.FromName(ctx.Client, emoji));
                            await Task.Delay(400);
                        }
                    }).Start();

                    var selectedCategory = SupportEmoji.FirstOrDefault(x => x.Value == em.Result.Emoji.GetDiscordName()).Key;

                    //Успешное продолжение создания тикета
                    //Запрос описания проблемы
                    ticketEmbed = new DiscordEmbedBuilder
                    {
                        Title = "Sea of Thieves RU | Ожидание вопроса",
                        Description = "Опишите ваш вопрос коротким сообщением.\n‎",
                        Color = new DiscordColor("#e67e22")
                    };
                    ticketEmbed.WithThumbnail(guild.IconUrl);

                    ticketEmbed.AddField("Выбранная категория", $"{SupportEmoji[selectedCategory]} {SupportNames[selectedCategory]}", true);

                    ticketEmbed.WithFooter("‎\nУ вас есть 5 минут для ввода вопроса.");

                    await ticketMessage.ModifyAsync(embed: ticketEmbed.Build());

                    var emsg = await interactivity.WaitForMessageAsync(x => x.Author.Id == ctx.User.Id && !x.Content.StartsWith(Bot.BotSettings.Prefix), TimeSpan.FromMinutes(5));

                    if (!emsg.TimedOut)
                    {
                        //Запрос подтверждения тикета.
                        ticketEmbed = new DiscordEmbedBuilder(ticketEmbed)
                        {
                            Title = "Sea of Thieves RU | Подтверждение тикета",
                            Description = "Подтвердите ваш тикет для отправки. \n" +
                            "`✅ отправить` `❌ отменить` \n‎",
                            Color = new DiscordColor("#e67e22")
                        };

                        ticketEmbed.AddField("Вопрос", emsg.Result.Content, true);

                        ticketEmbed.WithFooter("‎\nДождитесь загрузки всех вариантов ответа. У вас есть минута на выбор варианта");

                        await ticketMessage.ModifyAsync(embed: ticketEmbed.Build());

                        //Реакции подтверждения
                        var yesEmoji = DiscordEmoji.FromName(ctx.Client, ":white_check_mark:");
                        var noEmoji = DiscordEmoji.FromName(ctx.Client, ":x:");

                        await ticketMessage.CreateReactionAsync(yesEmoji);
                        await Task.Delay(400);
                        await ticketMessage.CreateReactionAsync(noEmoji);

                        //Ждем ответа на подтверждение
                        em = await interactivity.WaitForReactionAsync(x => x.Emoji == yesEmoji || x.Emoji == noEmoji, ctx.User, TimeSpan.FromSeconds(60));

                        if (!em.TimedOut)
                        {
                            await ticketMessage.DeleteOwnReactionAsync(yesEmoji);
                            await Task.Delay(400);
                            await ticketMessage.DeleteOwnReactionAsync(noEmoji);

                            if (em.Result.Emoji == yesEmoji)
                            {
                                //Создание канала для ответа
                                var supportChannel = await guild.CreateChannelAsync($"{DiscordEmoji.FromName(ctx.Client, SupportEmoji[selectedCategory])} {ctx.User.Username}", ChannelType.Text,
                                    guild.GetChannel(Bot.BotSettings.SupportCategory));

                                //Выдача прав на канал
                                await supportChannel.AddOverwriteAsync(await guild.GetMemberAsync(ctx.User.Id), Permissions.AccessChannels);

                                switch (selectedCategory)
                                {
                                    case SupportType.Admin:
                                    case SupportType.Developer: 
                                    case SupportType.Donate:
                                        //Do nothing
                                        break;
                                    case SupportType.Moderator:
                                    case SupportType.Events:
                                        await supportChannel.AddOverwriteAsync(guild.GetRole(Bot.BotSettings.ModeratorsRole), Permissions.AccessChannels);
                                        break;
                                    case SupportType.FleetCaptain:
                                        await supportChannel.AddOverwriteAsync(guild.GetRole(Bot.BotSettings.ModeratorsRole), Permissions.AccessChannels);
                                        await supportChannel.AddOverwriteAsync(guild.GetRole(Bot.BotSettings.FleetCaptainRole), Permissions.AccessChannels);
                                        break;
                                }

                                //Сохраняем в базу данных
                                var ticketData = TicketSQL.Create(supportChannel.Id, ctx.User.Id, ctx.Channel.Id, ticketMessage.Id,
                                    emsg.Result.Content, DateTime.Now, SupportNames[selectedCategory]);

                                //Обновляем тикет в лс
                                ticketEmbed = new DiscordEmbedBuilder(ticketEmbed)
                                {
                                    Description = "Тикет был создан.\n‎",
                                    Color = new DiscordColor("#f1c40f")
                                };
                                ticketEmbed.WithFooter($"‎\nID: {ticketData.ChannelId}");
                                ticketEmbed.WithTimestamp(DateTime.Now);

                                await ticketMessage.ModifyAsync(embed: ticketEmbed.Build());

                                await ctx.RespondAsync($"Ваш запрос был отравлен. Ждите ответ в {supportChannel.Mention}.");

                                //Обновляем тикет для отправки в канал
                                ticketEmbed = new DiscordEmbedBuilder(ticketEmbed)
                                {
                                    Title = "Sea of Thieves RU | Тикет",
                                    Description = "Ожидайте ответ на ваш запрос.\n‎"
                                }
                                .WithAuthor(ctx.User.ToString(), iconUrl: ctx.User.AvatarUrl)
                                .WithFooter($"‎\nID: {ticketData.ChannelId}");

                                var message = await supportChannel.SendMessageAsync($"Тикет от пользователя: {ctx.User.Mention}", embed: ticketEmbed.Build());

                                ticketData.MessageId = message.Id;
                            }
                            else
                            {
                                ticketEmbed = new DiscordEmbedBuilder(errorEmbed)
                                    .WithDescription("Тикет был отменён.");

                                await ticketMessage.ModifyAsync(embed: ticketEmbed.Build());
                            }
                        }
                        else
                        {
                            await ticketMessage.DeleteOwnReactionAsync(yesEmoji);
                            await Task.Delay(400);
                            await ticketMessage.DeleteOwnReactionAsync(noEmoji);

                            ticketEmbed = new DiscordEmbedBuilder(errorEmbed)
                                .WithDescription("Время выбора истекло.");

                            await ticketMessage.ModifyAsync(embed: ticketEmbed.Build());
                        }
                    }
                    else
                    {
                        ticketEmbed = new DiscordEmbedBuilder(errorEmbed)
                            .WithDescription("Время ввода вопроса истекло.");

                        await ticketMessage.ModifyAsync(embed: ticketEmbed.Build());
                    }
                }
                else
                {
                    new Task(async () =>
                    {
                        foreach (var emoji in SupportEmoji.Values)
                        {
                            await ticketMessage.DeleteOwnReactionAsync(DiscordEmoji.FromName(ctx.Client, emoji));
                            await Task.Delay(400);
                        }
                    }).Start();
                    ticketEmbed = new DiscordEmbedBuilder(errorEmbed)
                        .WithDescription("Время выбора категории тикета истекло.");

                    await ticketMessage.ModifyAsync(embed: ticketEmbed.Build());
                }
                DmMessageListener.DmHandled.Remove(ctx.User);
            }
            catch(Exception)
            {
                DmMessageListener.DmHandled.Remove(ctx.User);
                throw;
            }
        }

        [Group("ticket")]
        [Description("Команды управления тикетами.")]
        [RequireGuild]
        [RequireCustomRole(RoleType.FleetCaptain)]
        public class SupportCommands : BaseCommandModule
        {
            [Command("change")]
            [Description("Меняет категорию тикета. Лимит испоьзования: 2 раза в 10 минут")]
            [Cooldown(2, 600, CooldownBucketType.Channel)] //Discord API ограничивает обновление данных канала 2 раза в 10 минут. (Тупость такой большой рейтлимит ставить)
            public async Task Change(CommandContext ctx)
            {
                if(ctx.Channel.Parent.Id != Bot.BotSettings.SupportCategory)
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Команда используется только в каналах поддержки.");
                    return;
                }
                await ctx.Message.DeleteAsync();
                await ctx.TriggerTypingAsync();

                //Создание эмбеда тикета и заполнение по шаблону
                var changeEmbed = new DiscordEmbedBuilder
                {
                    Title = "Sea of Thieves RU | Изменение тикета",
                    Description = "Выберите категорию для переноса.\n",
                    Color = new DiscordColor("#e67e22")
                };

                changeEmbed.WithThumbnail(ctx.Guild.IconUrl);

                foreach (var suit in (SupportType[])Enum.GetValues(typeof(SupportType)))
                {
                    changeEmbed.Description += $"\n{SupportEmoji[suit]} {SupportNames[suit]}";
                }


                changeEmbed.WithFooter("Дождитесь загрузки всех вариантов ответа. У вас есть минута на выбор варианта");

                var changeMessage = await ctx.RespondAsync(embed: changeEmbed.Build());

                var interactivity = ctx.Client.GetInteractivity();

                //Создаем предложенные реакции.
                foreach (var emoji in SupportEmoji.Values)
                {
                    await changeMessage.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, emoji));
                    await Task.Delay(400);
                }

                //Ждем одну из предложенных реакций. (Минута времени)
                var em = await interactivity.WaitForReactionAsync(x => SupportEmoji.Values.Contains(x.Emoji.GetDiscordName()), ctx.User, TimeSpan.FromSeconds(60));

                if (!em.TimedOut)
                {
                    var selectedCategory = SupportEmoji.FirstOrDefault(x => x.Value == em.Result.Emoji.GetDiscordName()).Key;

                    var circleEmoji = DiscordEmoji.FromName(ctx.Client, SupportEmoji[selectedCategory]);
                    var newChannelName = circleEmoji + ctx.Channel.Name[1..];
                    await ctx.Channel.ModifyAsync(x => x.Name = newChannelName);

                    switch (selectedCategory)
                    {
                        case SupportType.Admin:
                        case SupportType.Developer:
                        case SupportType.Donate:
                            await ctx.Channel.AddOverwriteAsync(ctx.Guild.GetRole(Bot.BotSettings.ModeratorsRole), deny: Permissions.AccessChannels);
                            await ctx.Channel.AddOverwriteAsync(ctx.Guild.GetRole(Bot.BotSettings.FleetCaptainRole), deny: Permissions.AccessChannels);
                            break;
                        case SupportType.Moderator:
                        case SupportType.Events:
                            await ctx.Channel.AddOverwriteAsync(ctx.Guild.GetRole(Bot.BotSettings.ModeratorsRole), Permissions.AccessChannels);
                            await ctx.Channel.AddOverwriteAsync(ctx.Guild.GetRole(Bot.BotSettings.FleetCaptainRole), deny: Permissions.AccessChannels);
                            break;
                        case SupportType.FleetCaptain:
                            await ctx.Channel.AddOverwriteAsync(ctx.Guild.GetRole(Bot.BotSettings.ModeratorsRole), Permissions.AccessChannels);
                            await ctx.Channel.AddOverwriteAsync(ctx.Guild.GetRole(Bot.BotSettings.FleetCaptainRole), Permissions.AccessChannels);
                            break;
                    }

                    await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Категория тикета была изменена на `{circleEmoji} {SupportNames[selectedCategory]}`. Модератор: {ctx.User}");

                    //Get ticket data from db
                    var ticketData = TicketSQL.Get(ctx.Channel.Id);

                    ticketData.Category = SupportNames[selectedCategory];

                    //Update dm embed
                    try
                    {
                        var dmChannel = await ctx.Client.GetChannelAsync(ticketData.DmChannelId);
                        var dmEmbedMessage = await dmChannel.GetMessageAsync(ticketData.DmMessageId);

                        var dmEmbed = dmEmbedMessage.Embeds.FirstOrDefault();
                        var newEmbed = new DiscordEmbedBuilder(dmEmbed);
                        newEmbed.Fields[0].Value = $"{circleEmoji} {SupportNames[selectedCategory]}";

                        await dmEmbedMessage.ModifyAsync(embed: newEmbed.Build());
                    }
                    catch { }

                    //Update channel embed
                    try
                    {
                        var embedMessage = await ctx.Channel.GetMessageAsync(ticketData.MessageId);

                        var embed = embedMessage.Embeds.FirstOrDefault();
                        var newEmbed = new DiscordEmbedBuilder(embed);
                        newEmbed.Fields[0].Value = $"{circleEmoji} {SupportNames[selectedCategory]}";

                        await embedMessage.ModifyAsync(embed: newEmbed.Build());
                    }
                    catch { }
                }
                else
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Время ответа вышло.");
                }
                await changeMessage.DeleteAsync();
            }

            [Command("close")]
            [Aliases("c")]
            [Description("Закрывает тикет вместе с каналом")]
            public async Task Close(CommandContext ctx)
            {
                if (ctx.Channel.Parent.Id != Bot.BotSettings.SupportCategory)
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Команда используется только в каналах поддержки.");
                    return;
                }

                //Убираем у пользователя возможность писать в данном канале.
                var overwritesToBeBlocked = ctx.Channel.PermissionOverwrites.Where(x => x.Type == OverwriteType.Member).ToList();

                foreach (var overwrite in overwritesToBeBlocked)
                    await overwrite.UpdateAsync(deny: Permissions.SendMessages, reason: "закрытие тикета");

                //Get ticket data from db
                var ticketData = TicketSQL.Get(ctx.Channel.Id);

                ticketData.Status = TicketSQL.TicketStatus.Closed;

                try
                {
                    var member = await ctx.Guild.GetMemberAsync(ticketData.UserId);

                    await member.SendMessageAsync($"Тикет `{ticketData.MessageId}` закрыт модератором {ctx.Member.Username}.");
                }
                catch { }

                //Update dm embed
                try
                {
                    var dmChannel = await ctx.Client.GetChannelAsync(ticketData.DmChannelId);
                    var dmEmbedMessage = await dmChannel.GetMessageAsync(ticketData.DmMessageId);

                    var dmEmbed = dmEmbedMessage.Embeds.FirstOrDefault();
                    var newEmbed = new DiscordEmbedBuilder(dmEmbed)
                    {
                        Description = "Тикет закрыт",
                        Color = new DiscordColor("#2ecc71")
                    };

                    await dmEmbedMessage.ModifyAsync(embed: newEmbed.Build());
                }
                catch { }

                //Update channel embed
                try
                {
                    var embedMessage = await ctx.Channel.GetMessageAsync(ticketData.MessageId);

                    var embed = embedMessage.Embeds.FirstOrDefault();
                    var newEmbed = new DiscordEmbedBuilder(embed)
                    {
                        Description = "Тикет закрыт",
                        Color = new DiscordColor("#2ecc71")
                    };

                    await embedMessage.ModifyAsync(embed: newEmbed.Build());
                }
                catch { }

                await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Тикет был успешно закрыт. Модератор: {ctx.User}\n Канал будет удалён автоматически через 2 дня.");
            }

            [Command("delete")]
            [Aliases("d")]
            [Description("Удаляет тикет вместе с каналом")]
            public async Task Delete(CommandContext ctx)
            {
                if (ctx.Channel.Parent.Id != Bot.BotSettings.SupportCategory)
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Команда используется только в каналах поддержки.");
                    return;
                }

                //Get ticket data from db
                var ticketData = TicketSQL.Get(ctx.Channel.Id);

                ticketData.Status = TicketSQL.TicketStatus.Deleted;

                await ctx.Channel.DeleteAsync();
            }

            [Command("block")]
            [Description("Блокирует доступ к команде `!support`")]
            [RequireCustomRole(RoleType.Moderator)]
            public async Task Block(CommandContext ctx, [Description("Пользователь")] DiscordUser user, [Description("Причина"), RemainingText] string reason = "Не указана")
            {
                await ctx.TriggerTypingAsync();

                if (SupportBlacklistEntry.IsBlacklisted(user.Id))
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь уже заблокирован");
                    return;
                }

                SupportBlacklistEntry.Create(user.Id, DateTime.Now, ctx.User.Id, reason);

                await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Пользователь успешно добавлен в ЧС поддержки.");

                await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                    "**Добавление пользователя в ЧС поддержки**\n\n" +
                    $"**Модератор:** {ctx.Member}\n" +
                    $"**Дата:** {DateTime.Now}\n" +
                    $"**Пользователь:** {user}\n" +
                    $"**Причина:** {reason}");
            }

            [Command("unblock")]
            [Description("Разрешает доступ к команде `!support`")]
            [RequireCustomRole(RoleType.Moderator)]
            public async Task Unblock(CommandContext ctx, [Description("Пользователь")] DiscordUser user, [Description("Причина"), RemainingText] string reason = "снятие")
            {
                await ctx.TriggerTypingAsync();

                if (!SupportBlacklistEntry.IsBlacklisted(user.Id))
                {
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не заблокирован");
                    return;
                }

                SupportBlacklistEntry.Remove(user.Id);

                await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Пользователь успешно убран из ЧС поддержки.");

                await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                    "**Удаление пользователя из ЧС поддержки**\n\n" +
                    $"**Модератор:** {ctx.Member}\n" +
                    $"**Дата:** {DateTime.Now}\n" +
                    $"**Пользователь:** {user}\n" +
                    $"**Причина:** {reason}");
            }

            [Command("blocked")]
            [Description("Список заблокированных в `!support`")]
            public async Task Blocked(CommandContext ctx, [Description("Пользователь")]DiscordUser user = null)
            {
                await ctx.TriggerTypingAsync();
                var blacklistedUsers = user != null ? SupportBlacklistEntry.GetBlacklisted(user.Id) : SupportBlacklistEntry.GetBlacklisted();

                if(blacklistedUsers.Count == 0)
                {
                    await ctx.RespondAsync("Пользователи не найдены");
                    return;
                }

                var formattedList = new List<string>();
                foreach(var entry in blacklistedUsers)
                {
                    try
                    {
                        var blockedUser = await ctx.Client.GetUserAsync(entry.UserId);
                        var moderator = await ctx.Guild.GetMemberAsync(entry.ModeratorId);

                        formattedList.Add($"**Пользователь:** {blockedUser.Username}#{blockedUser.Discriminator} ({entry.UserId})" +
                            $"\n**Дата блокировки:** {entry.BanDate.ToShortDateString()}" +
                            $"\n**Модератор:** {moderator.Username}#{moderator.Discriminator} ({entry.ModeratorId})" +
                            $"\n**Причина:** {entry.Reason}");
                    }
                    catch
                    {
                        formattedList.Add($"**Пользователь:** {entry.UserId}" +
                            $"\n**Дата блокировки:** {entry.BanDate.ToShortDateString()}" +
                            $"\n**Модератор:** {entry.ModeratorId}" +
                            $"\n**Причина:** {entry.Reason}");
                    }
                }

                var interactivity = ctx.Client.GetInteractivity();

                var inviters_pagination = Utility.GeneratePagesInEmbeds(formattedList, "Список ЧС поддержки.");

                if (inviters_pagination.Count() > 1)
                    //await interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, inviters_pagination, timeoutoverride: TimeSpan.FromMinutes(5));
                    await interactivity.SendPaginatedMessageAsync(
                        channel: await ctx.Member.CreateDmChannelAsync(),
                        user: ctx.User,
                        pages: inviters_pagination,
                        behaviour: PaginationBehaviour.Ignore,
                        deletion: ButtonPaginationBehavior.DeleteButtons,
                        token: default);
                else
                    await ctx.RespondAsync(embed: inviters_pagination.First().Embed);
            }
        }
    }
    public enum SupportType
    {
        Admin,
        Moderator,
        Developer,
        Donate,
        FleetCaptain,
        Events
    }
}
