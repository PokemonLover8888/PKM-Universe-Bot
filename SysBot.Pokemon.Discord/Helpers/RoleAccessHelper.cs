using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

internal static class RoleAccessHelper
{
    /// <summary>
    /// Re-fetches the invoking member's roles fresh via REST and re-checks sudo + the required role
    /// set. The bots run without the Server Members privileged intent, so a role granted AFTER a
    /// member was first cached is never pushed over the gateway — the cached <c>SocketGuildUser.Roles</c>
    /// can be stale and wrongly deny a genuine role holder. This bypasses that cache. Returns false on
    /// any failure so callers fall back to their normal (cached) decision.
    /// </summary>
    public static async Task<bool> HasRoleFreshAsync(ICommandContext context, DiscordManager mgr, string roleName)
    {
        try
        {
            if (context.Guild is null || context.Client is not DiscordSocketClient socket)
                return false;

            var restUser = await socket.Rest.GetGuildUserAsync(context.Guild.Id, context.User.Id).ConfigureAwait(false);
            if (restUser is null)
                return false;

            var names = restUser.RoleIds
                .Select(id => context.Guild.GetRole(id)?.Name)
                .Where(n => n is not null)
                .Select(n => n!)
                .ToList();

            return mgr.CanUseSudo(names) || mgr.GetHasRoleAccess(roleName, names);
        }
        catch
        {
            return false;
        }
    }
}
