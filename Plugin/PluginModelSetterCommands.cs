namespace SharpTimerModelSetter
{
    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Core.Attributes.Registration;
    using CounterStrikeSharp.API.Modules.Commands;

    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {
        [ConsoleCommand("css_setmodel", "sets your model by index from cfg")]
        [CommandHelper(minArgs: 1, usage: "!setmodel [index]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void SetModelAsync(CCSPlayerController? player, CommandInfo command)
        {
            _ = SetModelAsync(player, command.GetArg(1), player.SteamID.ToString());
        }

        [ConsoleCommand("css_models", "lists all models")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void ListModels(CCSPlayerController? player, CommandInfo command)
        {
            string ResourcePrecacherCfg = Path.Join(GameDir + "/csgo/addons/counterstrikesharp/configs/plugins/SharpTimerMS", "SharpTimerMS.json");
            _ = PrintAllResources(player, ResourcePrecacherCfg, player.SteamID.ToString());
        }

        [ConsoleCommand("sharptimer_modelsetter_vip_only", "This is for the Secondary SharpTimerMS plugin. Wheter to only allow vip the access to it. Default value: false")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerMSVIPConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            Config.vipOnly = bool.TryParse(args, out bool vipOnlyValue) ? vipOnlyValue : args != "0" && Config.vipOnly;
        }

        [ConsoleCommand("sharptimer_modelsetter_set_model_on_spawn", "This is for the Secondary SharpTimerMS plugin. Wheter to set the first player model from the list on spawn. Default value: false")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerMSSpawnConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            Config.setOnSpawn = bool.TryParse(args, out bool setOnSpawnValue) ? setOnSpawnValue : args != "0" && Config.setOnSpawn;
        }
    }
}
