using System;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Listeners;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Interactivity;

namespace Bot_NetCore.Commands
{
    [Group("ss")]
    [Description("Команды Секретного Санты")]
    public class SecretSantaCommands : BaseCommandModule
    {
        [Command("join")]
        [RequireDirectMessage]
        [Description("Добавляет вас в список участников Секретного Санты")]
        public async Task Join(CommandContext ctx)
        {
            if (!Bot.BotSettings.SecretSantaEnabled)
            {
                await ctx.RespondAsync($"{Bot.BotSettings.ErrorEmoji} Команды Секретного Санты отключены!");
                return;
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

            await ctx.RespondAsync("**Пожалуйста, укажи свой адрес для отправки подарка:**");
            var interactivity = ctx.Client.GetInteractivity();
            DmMessageListener.DmHandled.Add(ctx.User);
            var address =
                await interactivity.WaitForMessageAsync(m => m.Author.Id == ctx.User.Id, TimeSpan.FromMinutes(3));

            SecretSantaParticipant.Create(ctx.User.Id, address.Result.Content);
            await ctx.RespondAsync(
                $"{Bot.BotSettings.OkEmoji} Мы добавили тебя в базу данных! Ты получишь сообщение с адресом получателя" +
                $" твоего подарка через некоторое время. Используй `!ss edit новый адрес` для изменения адреса или `!ss cancel` для отмены участия.");
            DmMessageListener.DmHandled.Remove(ctx.User);
        }

        [Command("edit")]
        [RequireDirectMessage]
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
        [RequireDirectMessage]
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
        }
    }
}