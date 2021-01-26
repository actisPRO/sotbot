using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace Bot_NetCore.Commands
{
    [Group("event")]
    [RequireGuild]
    [RequirePermissions(Permissions.Administrator)]
    [Description("Команды для ивента с скриншотами")]
    public class EventCommands : BaseCommandModule
    {
        [Command("cleanup")]
        [Description("Команда очистки скриншотов по реакциям. (Вводить в канале конкурса)")]
        public async Task EventCleanUp(CommandContext ctx)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();

            //Getting all messages from channel
            List<DiscordMessage> allMessages = new List<DiscordMessage>();
            var messages = await ctx.Channel.GetMessagesAsync();
            while (messages.Count() != 0)
            {
                allMessages.AddRange(messages);
                messages = await ctx.Channel.GetMessagesBeforeAsync(messages.Last().Id);
            }

            var deletedCount = 0;
            var approvedCount = 0;
            var i = 0;
            foreach (var message in allMessages)
            {
                if (message.Reactions.Count != 0)
                {
                    if (message.Reactions.FirstOrDefault().Emoji.GetDiscordName() == ":ok:")
                    {
                        approvedCount++;
                    }
                    else if (message.Reactions.FirstOrDefault().Emoji.GetDiscordName() == ":no_entry_sign:")
                    {
                        deletedCount++;
                        try
                        {
                            var member = await ctx.Guild.GetMemberAsync(message.Author.Id);
                            await member.SendMessageAsync($"**Ваш скриншот будет автоматически удалён из канала** {ctx.Channel.Mention}.\n" +
                                "**Причина:** несоответствие с требованиями конкурса. \n" +
                                "Внимательнее читайте условия в канале <#718099718369968199>.");
                        }
                        catch { }
                        await message.DeleteAsync();
                        if (i % 5 == 0)
                            await Task.Delay(3000);
                        else
                            await Task.Delay(400);
                        i++;
                    }
                }
            }

            await ctx.RespondAsync($"Total messages in channel: {allMessages.Count} \n" +
                $"Total deleted messages:  {deletedCount}\n" +
                $"Total approved messages: {approvedCount}");
        }

        [Command("createreactions")]
        [Description("Создает реакции под сообщением (Вводить в канале конкурса)")]
        public async Task EventCreateReactions(CommandContext ctx, [Description("Реакция которая будет добавлена под каждым скриншотом")] DiscordEmoji emoji)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();

            List<DiscordMessage> allMessages = new List<DiscordMessage>();
            var messages = await ctx.Channel.GetMessagesAsync();
            while (messages.Count() != 0)
            {
                allMessages.AddRange(messages);
                messages = await ctx.Channel.GetMessagesBeforeAsync(messages.Last().Id);
            }

            var i = 0;
            foreach (var message in allMessages)
            {
                if (message.Attachments.Count != 0)
                {
                    await message.CreateReactionAsync(emoji);
                    if (i % 10 == 0)
                        await Task.Delay(2000);
                    else
                        await Task.Delay(400);
                    i++;
                }
            }
        }

        [Command("gettopvoted")]
        [Description("Отправляет в лс список топ 10 по голосам (Вводить в канале конкурса)")]
        public async Task EventGetTopVoted(CommandContext ctx)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();

            List<DiscordMessage> allMessages = new List<DiscordMessage>();
            var messages = await ctx.Channel.GetMessagesAsync();
            while (messages.Count() != 0)
            {
                allMessages.AddRange(messages);
                messages = await ctx.Channel.GetMessagesBeforeAsync(messages.Last().Id);
            }

            var topMessages = allMessages.Where(x => x.Reactions.Count != 0).OrderByDescending(x => x.Reactions.FirstOrDefault().Count).Take(10);

            var responce = "**Топ 10 скриншотов** \n";
            foreach (var topMessage in topMessages)
            {
                var votes = topMessage.Reactions.FirstOrDefault().Count;
                responce += $"Голосов: {votes} | https://discord.com/channels/{ctx.Guild.Id}/{ctx.Channel.Id}/{topMessage.Id} \n";
            }

            await ctx.Member.SendMessageAsync(responce);
        }

        //Запихну сюда, так как это по любому временный код
        [AsyncListener(EventTypes.MessageReactionAdded)]
        public static async Task ClientOnEventReationAdded(DiscordClient client, MessageReactionAddEventArgs e)
        {
            //Check channel id in dev and main server
            if (e.Channel.Id == 803193543426441217 || e.Channel.Id == 801834857504178206)
            {
                var member = await e.Guild.GetMemberAsync(e.User.Id);

                if (member.JoinedAt > new DateTime(2021, 01, 30, 18, 0, 0))
                    await e.Message.DeleteReactionAsync(e.Emoji, e.User);
            }
        }
    }
}
