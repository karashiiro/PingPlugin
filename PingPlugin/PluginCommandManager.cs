﻿using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using PingPlugin.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static Dalamud.Game.Command.CommandInfo;
// ReSharper disable ForCanBeConvertedToForeach

namespace PingPlugin
{
    public class PluginCommandManager<THost> : IDisposable
    {
        private readonly ICommandManager commandManager;
        private readonly (string, CommandInfo)[] pluginCommands;
        private readonly THost host;

        public PluginCommandManager(THost host, ICommandManager commandManager)
        {
            this.commandManager = commandManager;
            this.host = host;

            this.pluginCommands = host.GetType().GetMethods()
                .Where(method => method.GetCustomAttribute<CommandAttribute>() != null)
                .SelectMany(GetCommandInfoTuple)
                .ToArray();

            AddComandHandlers();
        }

        // http://codebetter.com/patricksmacchia/2008/11/19/an-easy-and-efficient-way-to-improve-net-code-performances/
        // Benchmarking this myself gave similar results, so I'm doing this to somewhat counteract using reflection to access command attributes.
        // I like the convenience of attributes, but in principle it's a bit slower to use them as opposed to just initializing CommandInfos directly.
        // It's usually sub-1 millisecond anyways, though. It probably doesn't matter at all.
        private void AddComandHandlers()
        {
            for (var i = 0; i < this.pluginCommands.Length; i++)
            {
                var (command, commandInfo) = this.pluginCommands[i];
                this.commandManager.AddHandler(command, commandInfo);
            }
        }

        private void RemoveCommandHandlers()
        {
            for (var i = 0; i < this.pluginCommands.Length; i++)
            {
                var (command, _) = this.pluginCommands[i];
                this.commandManager.RemoveHandler(command);
            }
        }

        private IEnumerable<(string, CommandInfo)> GetCommandInfoTuple(MethodInfo method)
        {
            var handlerDelegate = (IReadOnlyCommandInfo.HandlerDelegate)Delegate.CreateDelegate(typeof(IReadOnlyCommandInfo.HandlerDelegate), this.host, method);

            var command = handlerDelegate.Method.GetCustomAttribute<CommandAttribute>();
            var aliases = handlerDelegate.Method.GetCustomAttribute<AliasesAttribute>();
            var helpMessage = handlerDelegate.Method.GetCustomAttribute<HelpMessageAttribute>();
            var showInHelp = handlerDelegate.Method.GetCustomAttribute<ShowInHelpAttribute>();

            var commandInfo = new CommandInfo(handlerDelegate)
            {
                HelpMessage = helpMessage?.HelpMessage ?? string.Empty,
                ShowInHelp = showInHelp != null,
            };

            // Create list of tuples that will be filled with one tuple per alias, in addition to the base command tuple.
            var commandInfoTuples = new List<(string, CommandInfo)> { (command.Command, commandInfo) };
            if (aliases != null)
            {
                // ReSharper disable once LoopCanBeConvertedToQuery
                for (var i = 0; i < aliases.Aliases.Length; i++)
                {
                    commandInfoTuples.Add((aliases.Aliases[i], commandInfo));
                }
            }

            return commandInfoTuples;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            RemoveCommandHandlers();
        }
    }
}
