using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Exceptions;

namespace SeaOfThieves.Commands
{
    public class CreationCommands
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

            var created = await ctx.Guild.CreateChannelAsync(
                $"{Bot.BotSettings.AutocreateSymbol} {name} {ctx.User.Username}",
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
    }
}