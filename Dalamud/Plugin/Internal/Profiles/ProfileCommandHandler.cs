﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CheapLoc;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Utility;
using Serilog;

namespace Dalamud.Plugin.Internal.Profiles;

/// <summary>
/// Service responsible for profile-related chat commands.
/// </summary>
[ServiceManager.EarlyLoadedService]
internal class ProfileCommandHandler : IServiceType, IDisposable
{
    private readonly CommandManager cmd;
    private readonly ProfileManager profileManager;
    private readonly ChatGui chat;
    private readonly Framework framework;

    private List<(string, ProfileOp)> queue = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileCommandHandler"/> class.
    /// </summary>
    /// <param name="cmd">Command handler.</param>
    /// <param name="profileManager">Profile manager.</param>
    /// <param name="chat">Chat handler.</param>
    /// <param name="framework">Framework.</param>
    [ServiceManager.ServiceConstructor]
    public ProfileCommandHandler(CommandManager cmd, ProfileManager profileManager, ChatGui chat, Framework framework)
    {
        this.cmd = cmd;
        this.profileManager = profileManager;
        this.chat = chat;
        this.framework = framework;

        this.cmd.AddHandler("/xlenableprofile", new CommandInfo(this.OnEnableProfile)
        {
            HelpMessage = Loc.Localize("ProfileCommandsEnableHint", "Enable a profile. Usage: /xlenableprofile \"Profile Name\""),
            ShowInHelp = true,
        });

        this.cmd.AddHandler("/xldisableprofile", new CommandInfo(this.OnDisableProfile)
        {
            HelpMessage = Loc.Localize("ProfileCommandsDisableHint", "Disable a profile. Usage: /xldisableprofile \"Profile Name\""),
            ShowInHelp = true,
        });

        this.cmd.AddHandler("/xltoggleprofile", new CommandInfo(this.OnToggleProfile)
        {
            HelpMessage = Loc.Localize("ProfileCommandsToggleHint", "Toggle a profile. Usage: /xltoggleprofile \"Profile Name\""),
            ShowInHelp = true,
        });

        this.framework.Update += this.FrameworkOnUpdate;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.cmd.RemoveHandler("/xlenableprofile");
        this.cmd.RemoveHandler("/xldisableprofile");
        this.cmd.RemoveHandler("/xltoggleprofile");

        this.framework.Update += this.FrameworkOnUpdate;
    }

    private void FrameworkOnUpdate(Framework framework1)
    {
        if (this.profileManager.IsBusy)
            return;

        if (this.queue.Count > 0)
        {
            var op = this.queue[0];
            this.queue.RemoveAt(0);

            var profile = this.profileManager.Profiles.FirstOrDefault(x => x.Name == op.Item1);
            if (profile == null || profile.IsDefaultProfile)
                return;

            switch (op.Item2)
            {
                case ProfileOp.Enable:
                    if (!profile.IsEnabled)
                        profile.SetState(true, false);
                    break;
                case ProfileOp.Disable:
                    if (profile.IsEnabled)
                        profile.SetState(false, false);
                    break;
                case ProfileOp.Toggle:
                    profile.SetState(!profile.IsEnabled, false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (profile.IsEnabled)
            {
                this.chat.Print(Loc.Localize("ProfileCommandsEnabling", "Enabling profile \"{0}\"...").Format(profile.Name));
            }
            else
            {
                this.chat.Print(Loc.Localize("ProfileCommandsDisabling", "Disabling profile \"{0}\"...").Format(profile.Name));
            }

            Task.Run(() => this.profileManager.ApplyAllWantStates()).ContinueWith(t =>
            {
                if (!t.IsCompletedSuccessfully && t.Exception != null)
                {
                    Log.Error(t.Exception, "Could not apply profiles through commands");
                    this.chat.PrintError(Loc.Localize("ProfileCommandsApplyFailed", "Failed to apply all of your profiles. Please check the console for errors."));
                }
                else
                {
                    this.chat.Print(Loc.Localize("ProfileCommandsApplySuccess", "Profiles applied."));
                }
            });
        }
    }

    private void OnEnableProfile(string command, string arguments)
    {
        var name = this.ValidateName(arguments);
        if (name == null)
            return;

        this.queue = this.queue.Where(x => x.Item1 != name).ToList();
        this.queue.Add((name, ProfileOp.Enable));
    }

    private void OnDisableProfile(string command, string arguments)
    {
        var name = this.ValidateName(arguments);
        if (name == null)
            return;

        this.queue = this.queue.Where(x => x.Item1 != name).ToList();
        this.queue.Add((name, ProfileOp.Disable));
    }

    private void OnToggleProfile(string command, string arguments)
    {
        var name = this.ValidateName(arguments);
        if (name == null)
            return;

        this.queue.Add((name, ProfileOp.Toggle));
    }

    private string? ValidateName(string arguments)
    {
        var name = arguments.Replace("\"", string.Empty);
        if (this.profileManager.Profiles.All(x => x.Name != name))
        {
            this.chat.PrintError($"No profile like \"{name}\".");
            return null;
        }

        return name;
    }

    private enum ProfileOp
    {
        Enable,
        Disable,
        Toggle,
    }
}
