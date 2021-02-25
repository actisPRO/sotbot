using System;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Listeners;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;

namespace Bot_NetCore.Commands
{
    [Group("ss")]
    [Description("Команды Секретного Санты")]
    public class SecretSantaCommands : BaseCommandModule
    {
        [Command("join")]
        [Attributes.RequireDirectMessage]
        [Description("Добавляет вас в список участников Секретного Санты")]
        public async Task Join(CommandContext ctx, [RemainingText] string args = "NONE_")
        {
            if (!Bot.BotSettings.SecretSantaEnabled)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Команды Секретного Санты отключены!");
                return;
            }

            if (args != "NONE_")
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Адрес нужно указать __отдельным сообщением__.");
            }

            if (DateTime.Now > Bot.BotSettings.SecretSantaLastJoinDate)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} К сожалению, регистрация на Секретного Санту уже закрыта =(");
                return;
            }

            if (SecretSantaParticipant.Get(ctx.User.Id) != null)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Ты уже зарегистрирован на Секретного Санту, используй `!ss edit` для " +
                    $"изменения адреса или `!ss cancel` для отмены участия.");
                return;
            }

            var member = await ctx.Client.Guilds[Bot.BotSettings.Guild].GetMemberAsync(ctx.User.Id);
            if (member.JoinedAt.DateTime > Bot.BotSettings.LastPossibleJoinDate)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} К сожалению, ты не можешь участвовать в Секретном Сайте, потому что" +
                    $"присоединился к нашему сообществу недавно =(.");
                return;
            }

            await ctx.RespondAsync("**Пожалуйста, укажи свой __почтовый__ адрес для отправки подарка:**\n" +
                                   "Лучше всего, если он будет в формате *Имя Фамилия, индекс, страна, регион, город, улица, дом, квартира*");
            var interactivity = ctx.Client.GetInteractivity();
            DmMessageListener.DmHandled.Add(ctx.User);

            var address =
                await interactivity.WaitForMessageAsync(m => m.Author.Id == ctx.User.Id, TimeSpan.FromMinutes(3));

            SecretSantaParticipant.Create(ctx.User.Id, address.Result.Content);
            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Мы добавили тебя в базу данных! Ты получишь сообщение с адресом получателя" +
                $" твоего подарка через некоторое время. Используй `!ss edit новый адрес` для изменения адреса или `!ss cancel` для отмены участия.");
            DmMessageListener.DmHandled.Remove(ctx.User);

            await member.GrantRoleAsync(ctx.Client.Guilds[Bot.BotSettings.Guild]
                .GetRole(Bot.BotSettings.SecretSantaRole));
        }

        [Command("edit")]
        [Attributes.RequireDirectMessage]
        [Description("Изменяет адрес")]
        public async Task Edit(CommandContext ctx, [RemainingText] string address = "none")
        {
            if (!Bot.BotSettings.SecretSantaEnabled)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Команды Секретного Санты отключены!");
                return;
            }

            var ss = SecretSantaParticipant.Get(ctx.User.Id);
            if (ss == null)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Ты не являешься участником Секретного Санты! Используй команду `!ss join` для участия.");
                return;
            }

            ss.Address = address;
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Адрес успешно изменён!");
        }

        [Command("cancel")]
        [Attributes.RequireDirectMessage]
        [Description("Удаляет вас из списка участников")]
        public async Task Delete(CommandContext ctx)
        {
            if (!Bot.BotSettings.SecretSantaEnabled)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Команды Секретного Санты отключены!");
                return;
            }

            var ss = SecretSantaParticipant.Get(ctx.User.Id);
            if (ss == null)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Ты не являешься участником Секретного Санты!");
                return;
            }

            SecretSantaParticipant.Delete(ctx.User.Id);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Мы удалили тебя из списка участников.");

            var member = await ctx.Client.Guilds[Bot.BotSettings.Guild].GetMemberAsync(ctx.User.Id);
            await member.RevokeRoleAsync(ctx.Client.Guilds[Bot.BotSettings.Guild]
                .GetRole(Bot.BotSettings.SecretSantaRole));
        }

        [Command("fdelete")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task FDelete(CommandContext ctx, DiscordMember member, [RemainingText] string reason)
        {
            var ss = SecretSantaParticipant.Get(ctx.User.Id);
            if (ss == null)
            {
                await ctx.RespondAsync(
                    $"{Bot.BotSettings.ErrorEmoji} Пользователь не является участником Секретного Санты!");
                return;
            }

            SecretSantaParticipant.Delete(member.Id);
            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Участник удален!");

            await member.RevokeRoleAsync(ctx.Guild.GetRole(Bot.BotSettings.SecretSantaRole));

            await member.SendMessageAsync(
                "Администратор удалил тебя из списка участников Секретного Санты. **Причина:** " + reason);
        }

        [Command("sort")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Sort(CommandContext ctx)
        {
            var senders = SecretSantaParticipant.GetAll();
            var receivers = SecretSantaParticipant.GetAll();

            var random = new Random();
            for (int i = 0; i < senders.Count; ++i)
            {
                var avaliable = receivers;
                avaliable.Remove(senders[i]);

                var receiver = random.Next(0, avaliable.Count);
                senders[i].SendingTo = avaliable[receiver].Id;
                receivers.Remove(avaliable[receiver]);
            }

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Список сгененирован!");
        }

        [Command("clean")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Clean(CommandContext ctx)
        {
            var participants = SecretSantaParticipant.GetAll();
            int count = 0;
            for (int i = 0; i < participants.Count; ++i)
            {
                try
                {
                    var member = ctx.Guild.GetMemberAsync(participants[i].Id);
                }
                catch
                {
                    ++count;
                    participants.RemoveAt(i);
                }
            }

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Список очищен, удалено {count} участников!");
        }

        [Command("send")]
        [RequirePermissions(Permissions.Administrator)]
        public async Task Send(CommandContext ctx)
        {
            var participants = SecretSantaParticipant.GetAll();
            foreach (var participant in participants)
            {
                try
                {
                    var member = await ctx.Guild.GetMemberAsync(participant.Id);
                    var sendingTo = SecretSantaParticipant.Get(participant.SendingTo);
                    await member.SendMessageAsync("**🎅 Секретный Санта 🎅**\n" +
                                                  $"Отправь подарок на следующий адрес:\n{sendingTo.Address}\n" +
                                                  $"*Если получатель указал вместо адреса 'цифровой подарок' или ты сам хочешь отправить " +
                                                  $"цифровой подарок - напиши в ЛС пользователю `Санта#2145` (на сервере закреплён под модераторами)," +
                                                  $" он объяснит, что делать дальше.\n\n" +
                                                  $"**Пожалуйста, не забудь отправить свой подарок и сохранить трек-номер.**\n" +
                                                  $"Если ты не получишь подарок до Нового Года - свяжись с `Санта#2145`.");
                }
                catch
                {

                }
            }

            await ctx.RespondAsync($"{Bot.BotSettings.OkEmoji} Рассылка завершена!");
        }
    }
}