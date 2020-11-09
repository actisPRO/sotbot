using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Commands
{
    [Group("support")]
    [Description("Команда создания тикета.")]
    public class SupportCommands : BaseCommandModule
    {
        private static IReadOnlyDictionary<SupportType, string> SupportEmoji = new Dictionary<SupportType, string>()
        {
            { SupportType.Admin, ":red_circle:"},
            { SupportType.Moderator, ":orange_circle:"},
            { SupportType.Developer, ":white_circle:"},
            { SupportType.FleetCaptain, ":purple_circle:"},
            { SupportType.Events, ":green_circle:"},
            { SupportType.Tech, ":blue_circle:"}
        };

        private static IReadOnlyDictionary<SupportType, string> SupportNames = new Dictionary<SupportType, string>()
        {
            { SupportType.Admin, "Администрация"},
            { SupportType.Moderator, "Модерация"},
            { SupportType.Developer, "Разработчики"},
            { SupportType.FleetCaptain, "Капитаны рейда"},
            { SupportType.Events, "Ивенты"},
            { SupportType.Tech, "Технические Вопросы"}
        };

        [GroupCommand]
        [Description("Создает запрос тикета")]
        [RequireDirectMessage]
        public async Task Support(CommandContext ctx)
        {
            //TODO: Check for blacklisted users.
            if(ctx.User.IsBot) //Always true
            {
                await ctx.RespondAsync("Вы были заблокированы для создания тикетов. Свяжитесь с администрацией для выяснения");
                return;
            }

            var guild = await ctx.Client.GetGuildAsync(Bot.BotSettings.Guild);


            //Создание эмбеда тикета и заполнение по шаблону
            var ticketEmbed = new DiscordEmbedBuilder
            {
                Title = "Sea of Thieves RU | Создание тикета",
                Description = "Выберите категорию вашего запроса через реакцию",
                Color = new DiscordColor("#e67e22")
            };

            ticketEmbed.WithThumbnail(guild.IconUrl);

            ticketEmbed.AddField($"{SupportEmoji[SupportType.Admin]} {SupportNames[SupportType.Admin]}", "text", true);
            ticketEmbed.AddField($"{SupportEmoji[SupportType.Moderator]} {SupportNames[SupportType.Moderator]}", "text", true);
            ticketEmbed.AddField($"{SupportEmoji[SupportType.Developer]} {SupportNames[SupportType.Developer]}", "text", true);
            ticketEmbed.AddField($"{SupportEmoji[SupportType.FleetCaptain]} {SupportNames[SupportType.FleetCaptain]}", "text", true);
            ticketEmbed.AddField($"{SupportEmoji[SupportType.Events]} {SupportNames[SupportType.Events]}", "text", true);
            ticketEmbed.AddField($"{SupportEmoji[SupportType.Tech]} {SupportNames[SupportType.Tech]}", "text", true);

            ticketEmbed.WithFooter("Дождитесь загрузки всех вариантов ответа. У вас есть минута на выбор варианта");

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
                        "`✅ отравить` `❌ отменить` \n‎",
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
                            
                            var supportChannel = await guild.CreateChannelAsync($"{DiscordEmoji.FromName(ctx.Client, SupportEmoji[selectedCategory])} {ctx.User.Username}", ChannelType.Text,
                                guild.GetChannel(Bot.BotSettings.SupportCategory));

                            //Выдача прав на канал
                            await supportChannel.AddOverwriteAsync(await guild.GetMemberAsync(ctx.User.Id), Permissions.AccessChannels);

                            if (selectedCategory == SupportType.FleetCaptain)
                                await supportChannel.AddOverwriteAsync(guild.GetRole(Bot.BotSettings.FleetCaptainRole), Permissions.AccessChannels);


                            ticketEmbed = new DiscordEmbedBuilder(ticketEmbed)
                            {
                                Description = "Тикет был создан.\n‎",
                                Color = new DiscordColor("#2ecc71")
                            };
                            ticketEmbed.WithFooter("‎\nТикет успешно создан");
                            ticketEmbed.WithTimestamp(DateTime.Now);

                            await ticketMessage.ModifyAsync(embed: ticketEmbed.Build());

                            await ctx.RespondAsync($"Ваш запрос был отравлен. Ждите ответа в {supportChannel.Mention}.");

                            ticketEmbed = new DiscordEmbedBuilder(ticketEmbed)
                            {
                                Title = "Sea of Thieves RU | Тикет",
                                Description = "Ожидайте ответ на ваш запрос.\n‎",
                                Color = new DiscordColor("#e74c3c")
                            }
                            .WithAuthor(ctx.User.ToString(), iconUrl: ctx.User.AvatarUrl)
                            .WithFooter("‎\nВ ожидании ответа.");

                            await supportChannel.SendMessageAsync($"Тикет от пользователя: {ctx.User.Mention}", embed: ticketEmbed.Build());
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
        }

        private enum SupportType
        {
            Admin,
            Moderator,
            Developer,
            FleetCaptain,
            Events,
            Tech
        }
    }
}
