using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using Dalamud.Configuration.Internal;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace Dalamud.Interface.Internal.Windows.Settings.Widgets;

[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:Elements should be documented", Justification = "Internals")]
public class ProxySettingsEntry : SettingsEntry
{
    private bool useManualProxy;
    private string proxyProtocol = string.Empty;
    private string proxyHost = string.Empty;
    private int proxyPort;

    private int proxyProtocolIndex;
    private string proxyStatus = "Unknown";
    private readonly string[] proxyProtocols = new string[] { "http", "https", "socks5" };

    public override void Load()
    {

        this.useManualProxy = Service<DalamudConfiguration>.Get().UseManualProxy;
        this.proxyProtocol = Service<DalamudConfiguration>.Get().ProxyProtocol;
        this.proxyHost = Service<DalamudConfiguration>.Get().ProxyHost;
        this.proxyPort = Service<DalamudConfiguration>.Get().ProxyPort;
        this.proxyProtocolIndex = Array.IndexOf(this.proxyProtocols, this.proxyProtocol);
        if (this.proxyProtocolIndex == -1)
            this.proxyProtocolIndex = 0;
    }

    public override void Save()
    {
        Service<DalamudConfiguration>.Get().UseManualProxy = this.useManualProxy;
        Service<DalamudConfiguration>.Get().ProxyProtocol = this.proxyProtocol;
        Service<DalamudConfiguration>.Get().ProxyHost = this.proxyHost;
        Service<DalamudConfiguration>.Get().ProxyPort = this.proxyPort;
    }

    public override void Draw()
    {
        ImGui.Text("Agent Settings");
        ImGuiHelpers.SafeTextColoredWrapped(ImGuiColors.DalamudRed, "Setting up the network proxy used by Dalamud will affect the connection of the plugin library, save it and restart the game to take effect");
        ImGui.Checkbox("Manual Configuration of Agents", ref this.useManualProxy);
        if (this.useManualProxy)
        {
            ImGuiHelpers.SafeTextColoredWrapped(ImGuiColors.DalamudGrey, "When changing the options below, make sure you know what you're doing, otherwise don't make any changes.");
            ImGui.Text("Agreements");
            ImGui.SameLine();
            ImGui.Combo("##proxyProtocol", ref this.proxyProtocolIndex, this.proxyProtocols, this.proxyProtocols.Length);
            ImGui.Text("Address");
            ImGui.SameLine();
            ImGui.InputText("##proxyHost", ref this.proxyHost, 100);
            ImGui.Text("End");
            ImGui.SameLine();
            ImGui.InputInt("##proxyPort", ref this.proxyPort);
            this.proxyProtocol = this.proxyProtocols[this.proxyProtocolIndex];
        }

        if (ImGui.Button("Test the GitHub connection"))
        {
            Task.Run(async () =>
            {
                try
                {
                    this.proxyStatus = "Testing";
                    var handler = new HttpClientHandler();
                    if (this.useManualProxy)
                    {
                        handler.UseProxy = true;
                        handler.Proxy = new WebProxy($"{this.proxyProtocol}://{this.proxyHost}:{this.proxyPort}", true);
                    }
                    else
                    {
                        handler.UseProxy = false;
                    }
                    var httpClient = new HttpClient(handler);
                    httpClient.Timeout = TimeSpan.FromSeconds(3);
                    _ = await httpClient.GetStringAsync("https://raw.githubusercontent.com/goatcorp/dalamud-distrib/main/version");
                    this.proxyStatus = "Valid";
                }
                catch (Exception)
                {
                    this.proxyStatus = "Invalid";
                }
            });
        }

        var proxyStatusColor = ImGuiColors.DalamudWhite;
        switch (this.proxyStatus)
        {
            case "Testing":
                proxyStatusColor = ImGuiColors.DalamudYellow;
                break;
            case "Valid":
                proxyStatusColor = ImGuiColors.ParsedGreen;
                break;
            case "Invalid":
                proxyStatusColor = ImGuiColors.DalamudRed;
                break;
            default: break;
        }

        ImGui.TextColored(proxyStatusColor, $"Agent Test Results: {this.proxyStatus}");
    }
}
