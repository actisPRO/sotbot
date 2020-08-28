using System.Collections.Generic;
using System.Linq;
using System.Text;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Converters;
using DSharpPlus.CommandsNext.Entities;
using DSharpPlus.Entities;

namespace Bot_NetCore.Commands
{
    public class HelpFormatter : BaseHelpFormatter
    {
        public DiscordEmbedBuilder EmbedBuilder { get; }
        private Command Command { get; set; }

        /// <summary>
        /// Creates a new default help formatter.
        /// </summary>
        /// <param name="ctx">Context in which this formatter is being invoked.</param>
        public HelpFormatter(CommandContext ctx)
            : base(ctx)
        {
            this.EmbedBuilder = new DiscordEmbedBuilder()
                .WithTitle("Команды бота")
                .WithColor(0x007FFF);
        }

        /// <summary>
        /// Sets the command this help message will be for.
        /// </summary>
        /// <param name="command">Command for which the help message is being produced.</param>
        /// <returns>This help formatter.</returns>
        public override BaseHelpFormatter WithCommand(Command command)
        {
            this.Command = command;

            this.EmbedBuilder.WithDescription($"{Formatter.InlineCode(command.Name)}: {command.Description ?? "Описание отсутствует."}");

            if (command is CommandGroup cgroup && cgroup.IsExecutableWithoutSubcommands)
                this.EmbedBuilder.WithDescription($"{this.EmbedBuilder.Description}\n\nГруппа может быть выполнена как команда.");

            if (command.Aliases?.Any() == true)
                this.EmbedBuilder.AddField("Псевдонимы (Алиасы)", string.Join(", ", command.Aliases.Select(Formatter.InlineCode)), false);

            if (command.Overloads?.Any() == true)
            {
                var sb = new StringBuilder();

                foreach (var ovl in command.Overloads.OrderByDescending(x => x.Priority))
                {
                    sb.Append('`').Append(command.QualifiedName);

                    foreach (var arg in ovl.Arguments)
                        sb.Append(arg.IsOptional || arg.IsCatchAll ? " [" : " <").Append(arg.Name).Append(arg.IsCatchAll ? "..." : "").Append(arg.IsOptional || arg.IsCatchAll ? ']' : '>');

                    sb.Append("`\n\n");

                    foreach (var arg in ovl.Arguments)
                        sb.Append('`').Append(arg.Name).Append(" (").Append(this.CommandsNext.GetUserFriendlyTypeName(arg.Type)).Append(")`: ").Append(arg.Description ?? "Описание отсутствует.").Append('\n');

                    sb.Append("\n");
                }

                this.EmbedBuilder.AddField("Параметры", sb.ToString().Trim(), false);
            }

            return this;
        }

        /// <summary>
        /// Sets the subcommands for this command, if applicable. This method will be called with filtered data.
        /// </summary>
        /// <param name="subcommands">Subcommands for this command group.</param>
        /// <returns>This help formatter.</returns>
        public override BaseHelpFormatter WithSubcommands(IEnumerable<Command> subcommands)
        {
            var withDescription = string.Join("\n",
                subcommands.Where(x => x.Name != "help" && x.GetType() == typeof(Command) && x.Description != null)
                           .OrderBy(x => x.Name)
                           .Select(x => $"{Formatter.InlineCode(x.Name)} - {x.Description}"));

            var withoutDescription = string.Join(", ",
                subcommands.Where(x => x.Name != "help" && x.GetType() == typeof(Command) && x.Description == null)
                           .OrderBy(x => x.Name)
                           .Select(x => $"{Formatter.InlineCode(x.Name)}"));

            var fieldValue = string.Join("\n\n", new string[] { withoutDescription, withDescription });

            this.EmbedBuilder.AddField(this.Command != null ? "Подкоманды" : "Команды", fieldValue, false);

            //Default page
            if (this.Command == null)
            {
                var groupCommands = string.Join("\n",
                    subcommands.Where(x => x.GetType() == typeof(CommandGroup))
                               .OrderBy(x => x.Name.Length)
                               .Select(x => $"{Formatter.InlineCode(x.Name)} - {x.Description}"));

                this.EmbedBuilder.AddField("Группы команд", groupCommands, false);
            }
            return this;
        }

        /// <summary>
        /// Construct the help message.
        /// </summary>
        /// <returns>Data for the help message.</returns>
        public override CommandHelpMessage Build()
        {
            if (this.Command == null)
                this.EmbedBuilder.WithDescription($"Введите `{Bot.BotSettings.Prefix}help [группа / команда]` для подробной информации.");

            return new CommandHelpMessage(embed: this.EmbedBuilder.Build());
        }
    }
}
