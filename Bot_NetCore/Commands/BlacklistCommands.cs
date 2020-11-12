using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Exceptions;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;

namespace Bot_NetCore.Commands
{
    [Group("blacklist")]
    [Aliases("bl")]
    [Hidden]
    [RequireGuild]
    public class BlacklistCommands : BaseCommandModule
    {
        [Command("add")]
        public async Task Add(CommandContext ctx)
        {
            var isFleetCaptain = ctx.Member.Roles.Contains(ctx.Guild.GetRole(Bot.BotSettings.FleetCaptainRole)) && !Bot.IsModerator(ctx.Member); //Только капитаны рейда, модераторы не учитываются

            //Проверка на модератора или капитана рейда
            if (!Bot.IsModerator(ctx.Member) && !isFleetCaptain)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }
            
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

            if (userId.Result == null || userId.Result.Content.ToLower().Contains("отмена"))
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

            if (xbox.Result == null || xbox.Result.Content.ToLower().Contains("отмена"))
            {
                await ctx.Message.DeleteAsync();
                await status.DeleteAsync();
                if (xbox.Result != null) await xbox.Result.DeleteAsync();
                return;
            }

            var xboxSql = "";
            if (xbox.Result.Content.ToLower().Contains("нет"))
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

            if (reason.Result == null || reason.Result.Content.ToLower().Contains("отмена"))
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

            if (additional.Result == null || additional.Result.Content.ToLower().Contains("отмена"))
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
                $"**Пользователь:** {usernameSql} ({userIdSql})\n" +
                $"**Xbox:** {xboxSql}\n" +
                $"**Причина:** {reasonSql}\n" +
                $"**Дополнительно:** {additionalSql}\n");
        }

        [Command("remove")]
        public async Task Remove(CommandContext ctx, string id)
        {
            var isFleetCaptain = ctx.Member.Roles.Contains(ctx.Guild.GetRole(Bot.BotSettings.FleetCaptainRole)) && !Bot.IsModerator(ctx.Member); //Только капитаны рейда, модераторы не учитываются

            //Проверка на модератора или капитана рейда
            if (!Bot.IsModerator(ctx.Member) && !isFleetCaptain)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} У вас нет доступа к этой команде!");
                return;
            }
            
            BlacklistEntry.Remove(id);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Успешно удалена запись!");
        }

        [Command("check")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Check(CommandContext ctx)
        {
            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Запущена проверка всех зарегистрированных аккаунтов. Она может занять некоторое время.");
            await ctx.TriggerTypingAsync();

            var blacklist = BlacklistEntry.GetAll();

            var markedForBan = new List<WebUser>();
            var markedIds = new List<ulong>();
            var message = "**Следующие учётные записи были добавлены в чёрный список:**\n";
            foreach (var entry in blacklist)
            {
                // at first, check users with the same xbox
                if (entry.Xbox != "")
                {
                    var bannedXbox = WebUser.GetUsersByXbox(entry.Xbox);
                    foreach (var user in bannedXbox)
                    {
                        if (!markedIds.Contains(user.UserId))
                        {
                            markedIds.Add(user.UserId);   
                            markedForBan.Add(user);
                            message +=
                                $"• {user.Username} ({user.UserId}) {user.LastXbox} - совпадение по Xbox | Бан {entry.Id}\n";
                        }
                    }
                }
                
                var currentUser = WebUser.GetByDiscordId(entry.DiscordId);
                if (currentUser == null) continue;

                var ips = currentUser.Ips;
                foreach (var ip in ips)
                {
                    var sameIp = WebUser.GetUsersByIp(ip);
                    foreach (var user in sameIp)
                    {
                        if (!(user.UserId == currentUser.UserId && user.LastXbox == currentUser.LastXbox) && !markedIds.Contains(user.UserId))
                        {
                            // another account -> ban
                            markedIds.Add(user.UserId);
                            markedForBan.Add(user);
                            message +=
                                $"• {user.Username} ({user.UserId}) {user.LastXbox} - совпадение по IP с {currentUser.Username} ({currentUser.UserId}) | Бан {entry.Id}\n";
                        }
                    }
                }

                var xboxes = currentUser.Xboxes;
                foreach (var xbox in xboxes)
                {
                    var sameXbox = WebUser.GetUsersByXbox(xbox);
                    foreach (var user in sameXbox)
                    {
                        if (!(user.UserId == currentUser.UserId && user.LastXbox == currentUser.LastXbox) && !markedIds.Contains(user.UserId))
                        {
                            // another account -> ban
                            markedIds.Add(user.UserId);
                            markedForBan.Add(user);
                            message +=
                                $"• {user.Username} ({user.UserId}) {user.LastXbox} - совпадение по Xbox с {currentUser.Username} ({currentUser.UserId})| Бан {entry.Id}\n";
                        }
                    }
                }
            }

            foreach (var user in markedForBan)
            {
                var id = RandomString.NextString(6);
                var ban = BlacklistEntry.Create(id, user.UserId, user.Username, user.LastXbox, DateTime.Now,
                    ctx.Client.CurrentUser.Id,
                    "Автоматическая блокировка системой защиты", "");

                try
                {
                    var member = await ctx.Guild.GetMemberAsync(user.UserId);
                    var role = ctx.Guild.GetRole(Bot.BotSettings.FleetCodexRole);
                    if (member.Roles.Contains(role))
                        await member.RevokeRoleAsync(role);
                }
                catch (NotFoundException)
                {
                    // not a guild member
                }
            }

            await ctx.RespondAsync(message);
        }
    }
}