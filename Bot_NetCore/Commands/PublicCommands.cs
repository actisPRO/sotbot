using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;

namespace SeaOfThieves.Commands
{
    public class PublicCommands
    {
        [Command("create")]
        [Aliases("c")]
        [Description("Создаёт новый корабль. Вы должны быть в голосовом канале, чтобы использовать это.")]
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
                Bot.BotSettings.Bitrate, slots);

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

            //В DSharpPlus 3.2.3 нету параметра Users в классе DiscordChannel.
            //Так что проверяем по пользователям на их присутствие в канале
            var channel = member.VoiceState?.Channel;

            if (channel == null ||                               //Пользователь не в канале
               channel.Id != ctx.Member.VoiceState?.Channel.Id) //Оба пользователя не из одного и того же канала
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Пользователь не найден или не в вашем канале!");
                return;
            }

            if (channel.ParentId != Bot.BotSettings.AutocreateCategory)      //Не канал автосоздания
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Вы можете проголосовать только в канале автосоздания!");
                return;
            }

            //Эмоции голосования
            var emoji = DiscordEmoji.FromName(ctx.Client, ":white_check_mark:");

            var interactivity = ctx.Client.GetInteractivityModule();

            //Упираюсь в лимит DSharpPlus, в данной версии не известно сколько пользователей в канале
            //Так что считаем что канал полный
            var votesNeeded = channel.UserLimit switch
            {
                4 => 3, //Галеон
                3 => 2, //Бриг
                2 => 2, //Шлюп - 2 Так как будут бегать по каналам и абузить. Легче пересоздать канал
                _ => Math.Round((channel.UserLimit - 1) * 0.5 + 1, MidpointRounding.AwayFromZero) //Остальные каналы 50% + 1 голосов
            };

            //Embed голосования
            var embed = new DiscordEmbedBuilder
            {
                Title = $"Голосование за кик с канала!",
                Description = "Участники канала могут проголосовать за кик пользователя."
            };

            embed.WithAuthor($"{member.Username}#{member.Discriminator}", icon_url: member.AvatarUrl);
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

            var votedCount = votedMembers.Where(x => x.VoiceState?.Channel.Id == channel.Id).Count();

            //Результат
            var resultEmbed = new DiscordEmbedBuilder
            {
                Title = $"Голосование за кик с канала окончено!"
            };

            resultEmbed.WithAuthor($"{member.Username}#{member.Discriminator}", icon_url: member.AvatarUrl);

            if (member.VoiceState?.Channel == null ||
                member.VoiceState?.Channel != null &&
                member.VoiceState?.Channel.Id != channel.Id)
            {
                resultEmbed.WithDescription($"{Bot.BotSettings.OkEmoji} Пользователь уже покинул канал.");
                resultEmbed.WithColor(new DiscordColor("00FF00"));
            }
            else if (votedCount >= votesNeeded)
            {
                resultEmbed.WithDescription($"{Bot.BotSettings.OkEmoji} Участник был перемещен в афк канал.");
                resultEmbed.WithFooter($"Голосов за кик: { votedCount}");
                resultEmbed.WithColor(new DiscordColor("00FF00"));

                await member.PlaceInAsync(ctx.Guild.AfkChannel);
            }
            else
            {
                resultEmbed.WithDescription($"{Bot.BotSettings.ErrorEmoji} Недостаточно голосов.");
                resultEmbed.WithFooter($"Голосов за кик: { votedCount}. Нужно {votesNeeded} голос(а).");
                resultEmbed.WithColor(new DiscordColor("FF0000"));
            }

            await msg.ModifyAsync(embed: resultEmbed);
        }
    }
}
