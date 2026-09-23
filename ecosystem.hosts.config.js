// ============================================================================
//  PM2 supervisor for the multi-tenant bot hosts.
//  PM2 restarts a host the instant it exits (~15s back up) instead of waiting on
//  a polling watchdog. exp_backoff_restart_delay grows the delay on repeat crashes
//  so a crash-loop can NEVER hammer Discord into an IP ban.
//  Rule: one bot per game per host (SysCord<T>.Runner is static per game type).
//  Reboot-persisted via `pm2 save` -> pm2-resurrect.cmd (same as the governor).
// ============================================================================
//
//  TWO KINDS OF PATH BELOW — they are NOT interchangeable:
//
//    script : which BINARY to run. Every host runs the same build; there is no
//             canary in flight. Ship a new build by replacing these two exes.
//    cwd    : that host's DATA directory — logs/, records/, tradecodes.json,
//             finalcode.png, rest-proxy.txt are all written relative to it.
//             *** Never repoint a cwd to tidy it up. *** The bot loses its trade
//             records and its members' saved trade codes if you do.
//
//  History, so the odd cwd names make sense: between 2026-08 and 2026-09 each fix
//  was canaried by publishing to its own folder (publish-eggfix, -batchfix,
//  -worlds26, -otfix, -eggballfix) and pointing one host at it. Every one of those
//  fixes is now in the shared build, so on 2026-09-23 all hosts were collapsed back
//  onto STD/SV. The canary folders survive ONLY as data directories for the hosts
//  that accumulated state while running from them. Do not delete them.
//
//  To canary again: publish to a new folder, point ONE host's `script` at it, and
//  leave its `cwd` alone — exactly as the EGGBALLFIX entry did.
// ============================================================================
const STD = 'C:\\Users\\ericr\\source\\repos\\ZE-FusionBot\\publish-multitenant\\SysBot.Pokemon.ConsoleApp.exe';
const SV  = 'C:\\Users\\ericr\\source\\repos\\ZE-FusionBot\\publish-multitenant-sv\\SysBot.Pokemon.ConsoleApp.exe';

// Data directories. The names are historical; what matters is which host owns which.
const R = 'C:\\Users\\ericr\\source\\repos\\ZE-FusionBot\\';
const DATA_MAIN = R + 'publish-multitenant';     // host-A, host-I, host-F
const DATA_SV   = R + 'publish-multitenant-sv';  // host-D, host-E
const DATA_BCH  = R + 'publish-eggfix';          // host-B, host-C, host-H
const DATA_G    = R + 'publish-worlds26';        // host-G
const DATA_J    = R + 'publish-otfix';           // host-J

const D = 'C:\\Users\\ericr\\OneDrive\\Desktop';
const cfg = (b) => `${D}\\${b}\\config.json`;

const common = {
  interpreter: 'none',                 // run the .exe directly, not via node
  autorestart: true,
  exp_backoff_restart_delay: 15000,    // 15s, grows on repeat crashes = ban safety
  min_uptime: 30000,                   // must stay up 30s to be considered stable
  max_memory_restart: '2600M',         // recycle a host if it balloons past 2.6GB
  kill_timeout: 8000,
  env: { DISCORD_REST_PROXY: 'http://127.0.0.1:3460/api/v10/' },
};

module.exports = {
  apps: [
    { name: 'host-A', script: STD, cwd: DATA_MAIN, args: [cfg('Celebi-SWSH-Bot')], ...common },
    // Flareon (Let's Go / PB7) split out of host-A so its console can be freed for save work
    // without taking Celebi's trades down with it.
    { name: 'host-I', script: STD, cwd: DATA_MAIN, args: [cfg('Flareon-LGPE-Bot')], ...common },
    // Diancie (Legends Z-A / PA9) + Dialga (BDSP / PB8) split into their OWN processes: in host-A they
    // cross-wired trade DMs through Celebi (the first-loaded tenant). Solo hosts = each owns its own DMs.
    { name: 'host-G', script: STD, cwd: DATA_G,   args: [cfg('Diance-PLZA-Bot')], ...common },
    { name: 'host-H', script: STD, cwd: DATA_BCH, args: [cfg('Dialga-BDSP-Bot')], ...common },
    { name: 'host-B', script: STD, cwd: DATA_BCH, args: [cfg('Giratina-BDSP-Bot'), cfg('Floette-PLZA-Bot')], ...common },
    // Hoopa (Legends Z-A / PA9) split out of host-C for the SAME reason Dialga and Diancie left
    // host-A: as the SECOND-loaded tenant its trade DMs were being sent by Rayquaza, the
    // first-loaded one. Reported 2026-09-07 ("why is Rayquaza DMing me when I trade with Hoopa").
    // Solo hosts = each bot owns its own DMs.
    { name: 'host-C', script: STD, cwd: DATA_BCH, args: [cfg('Rayquaza-BDSP-Bot')], ...common },
    { name: 'host-J', script: STD, cwd: DATA_J,   args: [cfg('Hoopa-PLZA-Bot')], ...common },
    { name: 'host-D', script: SV,  cwd: DATA_SV,  args: [cfg('Mew-SV-Bot')], ...common },
    { name: 'host-E', script: SV,  cwd: DATA_SV,  args: [cfg('Meloetta-SV-Bot')], ...common },
    // Shaymin (Legends Arceus / PA8) runs in its OWN process: sharing a host with a Legends Z-A
    // (PA9) bot like Floette cross-wires their Switch connections (Floette grabbed 10.0.0.159).
    { name: 'host-F', script: STD, cwd: DATA_MAIN, args: [cfg('Shaymin-PLA-Bot')], ...common },
  ],
};
