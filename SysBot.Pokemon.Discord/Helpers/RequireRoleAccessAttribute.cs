using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

/// <summary>
/// Requires an assigned role in order to accept commands. Can be used by sudo users if satisfied.
/// </summary>
public sealed class RequireRoleAccessAttribute(string RoleName) : PreconditionAttribute
{
    // Create a field to store the specified name

    // Create a constructor so the name can be specified

    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        var mgr = SysCordSettings.Manager;
        if (mgr.Config.AllowGlobalSudo && mgr.CanUseSudo(context.User.Id))
            return PreconditionResult.FromSuccess();

        // Check if this user is a Guild User, which is the only context where roles exist
        if (context.User is not SocketGuildUser gUser)
            return PreconditionResult.FromError("You must be sending the message from a guild to run this command.");

        var roleNames = gUser.Roles.Select(z => z.Name).ToList();
        if (mgr.CanUseSudo(roleNames))
            return PreconditionResult.FromSuccess();

        if (mgr.GetHasRoleAccess(RoleName, roleNames))
            return PreconditionResult.FromSuccess();

        // Cached roles missed — they can be stale without the Server Members intent. Re-check fresh
        // over REST so a member who got the role after being cached isn't wrongly blocked.
        if (await RoleAccessHelper.HasRoleFreshAsync(context, mgr, RoleName).ConfigureAwait(false))
            return PreconditionResult.FromSuccess();

        return PreconditionResult.FromError("You do not have the required role to run this command.");
    }
}
