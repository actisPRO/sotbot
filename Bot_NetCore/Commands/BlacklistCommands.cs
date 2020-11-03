using System;
using System.Threading.Tasks;
using Bot_NetCore.Attributes;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
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
            if (userId.Result.Content != "нет")
            {
                try
                {
                    user = await ctx.Client.GetUserAsync(Convert.ToUInt64(userId.Result.Content));
                }
                catch (Exception e)
                {
                    await status.DeleteAsync();
                    await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось найти данного пользователя!");
                    return;
                }
            }
            
            await userId.Result.DeleteAsync();
            
            if (user == null) embed.AddField("Пользователь", "Неизвестен");
            else
            {
                embed.AddField("Пользователь", user.Username + "#" + user.Discriminator, true);
                embed.AddField("ID", user.Id.ToString(), true);
                embed.WithThumbnail(user.AvatarUrl);
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

            var xboxSql = "";
            if (xbox.Result.Content.Contains("нет"))
            {
                embed.AddField("Xbox", "Неизвестен", false);
            }
            else
            {
                embed.AddField("Xbox", xbox.Result.Content, false);
                xboxSql = xbox.Result.Content;
            }

            embed.WithFooter("Отправьте причину добавления в чёрный список, для отмены отправьте `отмена`");

            await xbox.Result.DeleteAsync();
            status = await status.ModifyAsync(embed: embed.Build());

            // запрашиваем причину
            
            var reason =
                await interactivity.WaitForMessageAsync(m => m.Author.Id == ctx.Member.Id, TimeSpan.FromMinutes(1));

            if (reason.Result == null || reason.Result.Content.Contains("отмена"))
            {
                await ctx.Message.DeleteAsync();
                await status.DeleteAsync();
                if (reason.Result != null) await reason.Result.DeleteAsync();
                return;
            }

            var reasonSql = reason.Result.Content;
            embed.AddField("Причина", reason.Result.Content, false);

            embed.WithFooter(
                "Отправьте дополнительную информацию (например, ссылки на доказательства), иначе отправьте `нет` или для отмены отправьте `отмена`");

            await reason.Result.DeleteAsync();
            status = await status.ModifyAsync(embed: embed.Build());
            
            // запрашиваем допольнительную информацию
            
            var additional =
                await interactivity.WaitForMessageAsync(m => m.Author.Id == ctx.Member.Id, TimeSpan.FromMinutes(1));

            if (additional.Result == null || additional.Result.Content.Contains("отмена"))
            {
                await ctx.Message.DeleteAsync();
                await status.DeleteAsync();
                if (additional.Result != null) await additional.Result.DeleteAsync();
                return;
            }

            var additionalSql = "";
            if (additional.Result.Content == "нет")
            {
                embed.AddField("Дополнительно", "Не указано", false);
            }
            else
            {
                embed.AddField("Дополнительно", additional.Result.Content, false);
                additionalSql = additional.Result.Content;
            }
            
            var id = RandomString.NextString(6);

            embed.WithFooter("Готово! ID записи: " + id);
            embed.Color = DiscordColor.SpringGreen;

            await additional.Result.DeleteAsync();
            status = await status.ModifyAsync(embed: embed.Build());

            var userIdSql = user == null ? 0 : user.Id;
            var usernameSql = user == null ? "" : user.Username + "#" + user.Discriminator;
            
            var entry = 
                BlacklistEntry.Create(id, userIdSql, usernameSql, xboxSql, DateTime.Now, ctx.Member.Id, reasonSql, additionalSql);

            await ctx.Guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                "**Добавление записи в ЧС**\n\n" +
                $"**Модератор:** {ctx.Member}\n" +
                $"**Дата:** {DateTime.Now}\n" +
                $"**ID:** {id}\n" +
                $"**Пользователь:** {userIdSql}\n" +
                $"**Xbox:** {xboxSql}\n" +
                $"**Причина:** {reasonSql}\n" +
                $"**Дополнительно:** {additionalSql}\n");
        }
    }
}