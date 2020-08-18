using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;

namespace Bot_NetCore.Commands
{
    public class PublicCommands : BaseCommandModule
    {
        [Command("create")]
        [Aliases("c")]
        [Description("Создаёт новый корабль.")]
        public async Task Create(CommandContext ctx, [Description("Количество членов экипажа (от 2 до 4)")]
            int slots = 4)
        {
            if (Bot.ShipCooldowns.ContainsKey(ctx.User))
                if ((Bot.ShipCooldowns[ctx.User] - DateTime.Now).Seconds > 0)
                {
                    var m = await ctx.Guild.GetMemberAsync(ctx.User.Id);
                    try
                    {
                        await m.PlaceInAsync(ctx.Guild.GetChannel(Bot.BotSettings.WaitingRoom));
                    }
                    catch (BadRequestException)
                    {
                        await ctx.RespondAsync(
                            $"{Bot.BotSettings.ErrorEmoji} Вы должны быть в голосовом канале, чтобы использовать эту команду.");
                        return;
                    }
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вам нужно подождать " +
                                           $"**{(Bot.ShipCooldowns[ctx.User] - DateTime.Now).Seconds}** секунд прежде чем " +
                                           "создавать новый корабль!");
                    return;
                }

            Bot.ShipCooldowns[ctx.User] = DateTime.Now.AddSeconds(Bot.BotSettings.FastCooldown);

            if (slots < 2 || slots > 4)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Количество членов экипажа должно быть от 2 до 4. ");
                return;
            }

            var name = "";
            switch (slots)
            {
                case 2:
                    name = "Шлюп";
                    break;
                case 3:
                    name = "Бриг";
                    break;
                case 4:
                    name = "Галеон";
                    break;
            }

            //Проверка на эмиссарство
            var channelSymbol = Bot.BotSettings.AutocreateSymbol;
            ((DiscordMember)ctx.User).Roles.ToList().ForEach(x =>
            {
                if (x.Id == Bot.BotSettings.EmissaryGoldhoadersRole)
                    channelSymbol = DiscordEmoji.FromName(ctx.Client, ":moneybag:");
                else if (x.Id == Bot.BotSettings.EmissaryTradingCompanyRole)
                    channelSymbol = DiscordEmoji.FromName(ctx.Client, ":pig:");
                else if (x.Id == Bot.BotSettings.EmissaryOrderOfSoulsRole)
                    channelSymbol = DiscordEmoji.FromName(ctx.Client, ":skull:");
                else if (x.Id == Bot.BotSettings.EmissaryAthenaRole)
                    channelSymbol = DiscordEmoji.FromName(ctx.Client, ":gem:");
                else if (x.Id == Bot.BotSettings.EmissaryReaperBonesRole)
                    channelSymbol = DiscordEmoji.FromName(ctx.Client, ":skull_crossbones:");
                else if (x.Id == Bot.BotSettings.HuntersRole)
                    channelSymbol = DiscordEmoji.FromName(ctx.Client, ":fish:");
                else if (x.Id == Bot.BotSettings.ArenaRole)
                    channelSymbol = DiscordEmoji.FromName(ctx.Client, ":crossed_swords:");
            });

            var created = await ctx.Guild.CreateChannelAsync(
                $"{channelSymbol} {name} {ctx.User.Username}",
                ChannelType.Voice, ctx.Guild.GetChannel(Bot.BotSettings.AutocreateCategory),
                bitrate: Bot.BotSettings.Bitrate, userLimit: slots);

            try
            {
                await ctx.Member.PlaceInAsync(created);
            }
            catch (BadRequestException)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Вы должны быть в голосовом канале, чтобы использовать эту команду.");
                await created.DeleteAsync();
                return;
            }

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно создан канал **{created.Name}**!");
        }

        [Command("votekick")]
        [Aliases("vk")]
        [Description("Создаёт голосование против другого игрока")]
        public async Task VoteKick(CommandContext ctx, [Description("Пользователь")] DiscordMember member)
        {
            if (ctx.Member.Id == member.Id)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не можете кикнуть самого себя!");
                return;
            }

            if (Bot.IsModerator(member))
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы не можете кикнуть данного пользователя!");
                return;
            }

            var channel = member.VoiceState?.Channel;

            if (channel == null ||                               //Пользователь не в канале
               channel.Id != ctx.Member.VoiceState?.Channel.Id)  //Оба пользователя не из одного и того же канала
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не найден или не в вашем канале!");
                return;
            }

            if (channel.Users.Count() > 2)      //Не канал автосоздания
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы можете проголосовать в канале в котором только 2 пользователя!");
                return;
            }

            //Эмоции голосования
            var emoji = DiscordEmoji.FromName(ctx.Client, ":white_check_mark:");

            var interactivity = ctx.Client.GetInteractivity();

            //Подсчёт нужных голосов
            var votesNeeded = channel.Users.Count() switch
            {
                4 => 3, //Галеон
                3 => 2, //Бриг
                2 => 2, //Шлюп - 2 Так как будут бегать по каналам и абузить. Легче пересоздать канал. Этот вариант исключён выше
                _ => Math.Round((channel.Users.Count() - 1) * 0.5 + 1, MidpointRounding.AwayFromZero) //Остальные каналы 50% + 1 голосов
            };

            //Embed голосования
            var embed = new DiscordEmbedBuilder
            {
                Title = $"Голосование за кик с канала!",
                Description = "Участники канала могут проголосовать за кик пользователя."
            };

            embed.WithAuthor($"{member.Username}#{member.Discriminator}", iconUrl: member.AvatarUrl);
            embed.WithFooter($"Голосование закончится через 60 сек. Нужно {votesNeeded} голос(а).");
            var msg = await ctx.RespondAsync(embed: embed);

            //Добавляем реакции к сообщению
            await msg.CreateReactionAsync(emoji);

            //Собираем данные
            var pollResult = await interactivity.CollectReactionsAsync(msg, new TimeSpan(0, 0, 60));

            //Обработка результатов
            var votedUsers = await msg.GetReactionsAsync(emoji);

            //Чистим реакции
            await msg.DeleteAllReactionsAsync();

            //Каст из DiscordUser в DiscordMember для проверки активного канала
            var votedMembers = new List<DiscordMember>();
            foreach (var votedUser in votedUsers)
                votedMembers.Add(await ctx.Guild.GetMemberAsync(votedUser.Id));

            var votesCount = votedMembers.Where(x => x.VoiceState?.Channel.Id == channel.Id).Count();

            //Результат
            var resultEmbed = new DiscordEmbedBuilder
            {
                Title = $"Голосование за кик с канала окончено!"
            };

            resultEmbed.WithAuthor($"{member.Username}#{member.Discriminator}", iconUrl: member.AvatarUrl);

            if (member.VoiceState?.Channel == null ||
                member.VoiceState?.Channel != null &&
                member.VoiceState?.Channel.Id != channel.Id)
            {
                resultEmbed.WithDescription($"{Bot.BotSettings.OkEmoji} Пользователь уже покинул канал.");
                resultEmbed.WithColor(new DiscordColor("00FF00"));
            }
            else if (votesCount >= votesNeeded)
            {
                resultEmbed.WithDescription($"{Bot.BotSettings.OkEmoji} Участник был перемещен в афк канал.");
                resultEmbed.WithFooter($"Голосов за кик: {votesCount}");
                resultEmbed.WithColor(new DiscordColor("00FF00"));

                await member.PlaceInAsync(ctx.Guild.AfkChannel);
            }
            else
            {
                resultEmbed.WithDescription($"{Bot.BotSettings.ErrorEmoji} Недостаточно голосов.");
                resultEmbed.WithFooter($"Голосов за кик: {votesCount}. Нужно {votesNeeded} голос(а).");
                resultEmbed.WithColor(new DiscordColor("FF0000"));
            }

            await msg.ModifyAsync(embed: resultEmbed.Build());
        }


        [Command("createfleet")]
        [Aliases("cf")]
        [Description("Создаёт голосование для создания рейда")]
        [Cooldown(1, 0, CooldownBucketType.Guild)]
        public async Task CreateFleetAsync(CommandContext ctx,
            [Description("Количество кораблей [1 - 5]")] int nShips,
            [Description("Слоты на корабле [2 - 25]")] int slots,
            [RemainingText, Description("Название рейда")] string notes)
        {
            notes = notes.Substring(0, Math.Min(notes.Length, 25));

            if (nShips < 1 || nShips > 10 ||
                slots < 2 || slots > 25)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Недопустимые параметры рейда!");
                return;
            }

            //Проверка на капитана или модератора
            var pollNeeded = !ctx.Member.Roles.Contains(ctx.Guild.GetRole(Bot.BotSettings.FleetCaptainRole)) && !Bot.IsModerator(ctx.Member);

            var pollSucceded = false;

            var moscowTime = DateTime.Now;
            var timeOfDay = moscowTime.ToString("HH:mm");

            var fleetCreationMessage = await ctx.Guild.GetChannel(Bot.BotSettings.FleetCreationChannel).
                SendMessageAsync($"**Дата рейда**: {moscowTime:dd\\/MM} \n" +
                                 $"**Время начала**: {timeOfDay} \n" +
                                 $"**Количество кораблей**: {nShips} \n" +
                                 $"**Примечание**: {notes} \n\n" +
                                 $"***Создатель рейда**: {ctx.Member.Mention}*");

            if (pollNeeded)
            {
                var pollTIme = new TimeSpan(0, 2, 0);

                //Эмоции голосования
                var emoji = DiscordEmoji.FromName(ctx.Client, ":white_check_mark:");

                var interactivity = ctx.Client.GetInteractivity();

                //Подсчёт нужных голосов - 50% + 1 голосов
                var votesNeeded = Math.Round((nShips * slots - 1) * 0.5 + 1, MidpointRounding.AwayFromZero);

                //Embed голосования
                var embed = new DiscordEmbedBuilder
                {
                    Title = $"Голосование за создание рейда!",
                    Description = "Все проголосовавшие должны находиться в Общем канале рейда."
                };

                embed.WithFooter($"Голосование закончится через {Utility.FormatTimespan(pollTIme)}. Нужно {votesNeeded} голос(а).");

                await fleetCreationMessage.ModifyAsync(embed: embed.Build());

                //Добавляем реакции к сообщению
                await fleetCreationMessage.CreateReactionAsync(emoji);

                //Собираем данные
                var pollResult = await interactivity.CollectReactionsAsync(fleetCreationMessage, pollTIme);

                //Обработка результатов
                var votedUsers = await fleetCreationMessage.GetReactionsAsync(emoji);

                //Чистим реакции
                await fleetCreationMessage.DeleteAllReactionsAsync();

                //Каст из DiscordUser в DiscordMember для проверки активного канала
                var votedMembers = new List<DiscordMember>();
                foreach (var votedUser in votedUsers)
                    votedMembers.Add(await ctx.Guild.GetMemberAsync(votedUser.Id));

                var votesCount = votedMembers.Where(x => x.VoiceState?.Channel.Id == Bot.BotSettings.FleetLobby).Count();

                //Результат
                var resultEmbed = new DiscordEmbedBuilder
                {
                    Title = $"Голосование окончено! Голосов за: {votesCount}"
                };

                if (votesCount >= votesNeeded)
                {
                    resultEmbed.WithDescription($"{Bot.BotSettings.OkEmoji} Каналы рейда создаются.");
                    resultEmbed.WithFooter("Результаты голосования будут удалены через 30 секунд.");
                    resultEmbed.WithColor(new DiscordColor("00FF00"));
                    pollSucceded = true;
                }
                else
                {
                    resultEmbed.WithDescription($"{Bot.BotSettings.ErrorEmoji} Недостаточно голосов.");
                    resultEmbed.WithFooter("Сообщение будет удалено через 30 секунд.");
                    resultEmbed.WithColor(new DiscordColor("FF0000"));
                }

                await fleetCreationMessage.DeleteAllReactionsAsync();
                await fleetCreationMessage.ModifyAsync(embed: resultEmbed.Build());
            }

            //Если капитан или голосование успешное
            if (pollNeeded == false || (pollNeeded && pollSucceded))
            {
                var rootFleetCategory = ctx.Guild.GetChannel(Bot.BotSettings.FleetCategory);

                var fleetCategory = await rootFleetCategory.CloneAsync(); //await ctx.Guild.CreateChannelCategoryAsync($"Рейд {notes}");
                                                                          //var positions = $"Created pos: **{newFleet.Position}** ";
                await fleetCategory.ModifyAsync(x =>
                {
                    x.Name = $"Рейд {notes}";
                    x.Position = rootFleetCategory.Position + 1;
                });
                //positions += $"New pos: **{newFleet.Position}** Root pos: **{rootFleetCategory.Position}**";
                //await ctx.RespondAsync(positions);


                //TODO: Check permissions - UPD: Seems to be ok
                await ctx.Guild.CreateChannelAsync($"рейд-{notes}", ChannelType.Text, fleetCategory);
                await ctx.Guild.CreateChannelAsync($"бронь-инвайты-{notes}", ChannelType.Text, fleetCategory);
                await ctx.Guild.CreateChannelAsync($"Общий - {notes}", ChannelType.Voice, fleetCategory, bitrate: Bot.BotSettings.Bitrate, userLimit: nShips * slots);
                for (int i = 0; i < nShips; i++)
                    await ctx.Guild.CreateChannelAsync($"Рейд {i + 1} - {notes}", ChannelType.Voice, fleetCategory, bitrate: Bot.BotSettings.Bitrate, userLimit: slots + 1);
            }

            //Чистим голосование после создания рейда
            if (pollNeeded || pollSucceded)
            {
                Thread.Sleep(30000);
                if (pollSucceded)
                    await fleetCreationMessage.ModifyEmbedSuppressionAsync(true);
                else
                    await fleetCreationMessage.DeleteAsync();
            }
        }
    }
}
