using Discord;
using Discord.Commands;
using Discord.WebSocket;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

/// <summary>
/// A trade cooldown shared by every bot in the fleet, held by the trade-bridge.
///
/// Discord's per-channel slowmode can't do this job: it only rate-limits MESSAGES (so /trade walks
/// straight past it) and it's per channel (so a member could hop Hoopa → Celebi → Rayquaza and
/// never wait). Asking the bridge means one rule that holds however the trade arrives.
///
/// The clock starts when a trade actually ENTERS THE QUEUE, never at command submission, so a
/// typo, an illegal set or a blocked item costs the member nothing — which is exactly what the
/// bot's own "this was free" embed promises.
///
/// Every call FAILS OPEN. If the bridge is down or slow the trade proceeds: a limiter that takes
/// the service down with it is worse than no limiter.
/// </summary>
public static class FleetCooldown
{
    private const string BridgeBase = "http://127.0.0.1:3456";

    /// <summary>Roles that skip the cooldown. Mirrors the channel permissions Eric already uses.</summary>
    private static readonly string[] ExemptRoles =
    [
        "Elite Sysbot Access",
        "Premium Sysbot Access",
        "Owner",
    ];

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(3) };

    /// <summary>
    /// True when the member holds a role that skips the cooldown. Reads the cached guild user —
    /// a stale cache can only mean someone waits who shouldn't have to, never the reverse, and the
    /// caller falls back to allowing the trade on any error anyway.
    /// </summary>
    public static bool IsExempt(SocketCommandContext context)
    {
        try
        {
            if (context.User is not SocketGuildUser gu)
                return false;
            return gu.Roles.Any(r => ExemptRoles.Contains(r.Name, StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Asks the bridge whether this member may trade now.
    /// Returns null when they may; otherwise the minutes they still have to wait.
    /// </summary>
    public static async Task<int?> GetRemainingMinutesAsync(ulong userId, bool exempt)
    {
        if (exempt)
            return null;
        try
        {
            var url = $"{BridgeBase}/cooldown/check?user={userId}&exempt=0";
            using var resp = await Client.GetAsync(url).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            // Deliberately not pulling in a JSON dependency for two fields.
            if (body.Contains("\"allowed\":true", StringComparison.OrdinalIgnoreCase))
                return null;
            var mins = ExtractInt(body, "\"remainingMinutes\":");
            return mins > 0 ? mins : 1;
        }
        catch (Exception ex)
        {
            LogUtil.LogInfo($"[FleetCooldown] check failed, allowing trade: {ex.Message}", "FleetCooldown");
            return null;
        }
    }

    /// <summary>
    /// Starts the member's cooldown. Call ONLY once a trade has actually been accepted into the
    /// queue, so mistakes stay free.
    /// </summary>
    public static async Task StartAsync(ulong userId, bool exempt)
    {
        if (exempt)
            return;
        try
        {
            var json = $"{{\"user\":\"{userId}\"}}";
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await Client.PostAsync($"{BridgeBase}/cooldown/start", content).ConfigureAwait(false);
            _ = resp;
        }
        catch (Exception ex)
        {
            LogUtil.LogInfo($"[FleetCooldown] start failed (member gets a free pass): {ex.Message}", "FleetCooldown");
        }
    }

    private static int ExtractInt(string body, string key)
    {
        var i = body.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (i < 0)
            return 0;
        i += key.Length;
        var end = i;
        while (end < body.Length && (char.IsDigit(body[end]) || body[end] == ' '))
            end++;
        return int.TryParse(body.AsSpan(i, end - i).Trim(), out var v) ? v : 0;
    }
}
