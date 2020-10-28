using System;
using System.Threading.Tasks;
using Bot_NetCore.Attributes;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;

namespace Bot_NetCore.Commands
{
    [Group("blacklist")]
    [Aliases("bl")] 
    [Hidden]
    public class BlacklistCommands : BaseCommandModule
    {
        [Command("add")]
        public async Task Add(CommandContext ctx)
        {
            var embed = new DiscordEmbedBuilder();
            embed.Description = "**Добавление записи в чёрный список**\n";
            embed.WithAuthor(ctx.Member.Username + "#" + ctx.Member.Discriminator, iconUrl: ctx.Member.AvatarUrl);
            embed.Color = DiscordColor.Orange;

            embed.WithFooter(
                "Отправьте ID пользователя. Если неизвестно, отправьте `нет`, для отмены отправьте `отмена`");
            
            var status = await ctx.RespondAsync(embed: embed.Build());

            var interactivity = ctx.Client.GetInteractivity();
            
            // запрашиваем ID пользователя
            var userId =
                await interactivity.WaitForMessageAsync(m => m.Author.Id == ctx.Member.Id, TimeSpan.FromMinutes(1));

            if (userId.Result == null || userId.Result.Content.Contains("отмена"))
            {
                await ctx.Message.DeleteAsync();
                await status.DeleteAsync();
                if (userId.Result != null) await userId.Result.DeleteAsync();
                return;
            }

            DiscordUser user = null;
            // ищем пользователя
            try
            {
                await userId.Result.DeleteAsync();
                user = await ctx.Client.GetUserAsync(Convert.ToUInt64(userId.Result.Content));
            }
            catch (Exception e)
            {
                await status.DeleteAsync();
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось найти данного пользователя!");
                return;
            }

            if (user == null) embed.AddField("Пользователь", "Неизвестен");
            else
            {
                embed.AddField("Пользователь", user.Username + "#" + user.Discriminator, true);
                embed.AddField("ID", user.Id.ToString(), true);
                embed.WithImageUrl(user.AvatarUrl);
            }

            embed.WithFooter("Отправьте Xbox пользователя. Если неизвестно, отправьте `нет`, для отмены отправьте `отмена`");
            
            status = await status.ModifyAsync(embed: embed.Build());
            
            // запрашиваем xbox пользователя
            
            var xbox =
                await interactivity.WaitForMessageAsync(m => m.Author.Id == ctx.Member.Id, TimeSpan.FromMinutes(1));

            if (xbox.Result == null || xbox.Result.Content.Contains("отмена"))
            {
                await ctx.Message.DeleteAsync();
                await status.DeleteAsync();
                if (xbox.Result != null) await xbox.Result.DeleteAsync();
                return;
            }

            if (xbox.Result.Content.Contains("нет"))
            {
                embed.AddField("Xbox", "Неизвестен", true);
            }
            else
            {
                embed.AddField("Xbox", xbox.Result.Content, true);
            }

            // запрашиваем причину
            
            // запрашиваем допольнительную информацию
        }
    }
}