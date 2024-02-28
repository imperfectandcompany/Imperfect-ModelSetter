namespace SharpTimerModelSetter
{
    using System.Text.Json;
    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Core.Attributes;
    using CounterStrikeSharp.API.Core.Attributes.Registration;
    using MySqlConnector;
    using CounterStrikeSharp.API.Modules.Commands;
    using CounterStrikeSharp.API.Modules.Utils;
    using Microsoft.Extensions.Logging;

    [MinimumApiVersion(120)]
    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {
        public required PluginConfig Config { get; set; } = new PluginConfig();

        private readonly PrecacheContext PrecacheContext;

        private readonly PluginMigrations Migrations;

        public Plugin(PluginMigrations migrations, PrecacheContext context)
        {
            this.Migrations = migrations;
            this.PrecacheContext = context;
        }

        public string GameDir = string.Empty;

        private Dictionary<int, PlayerModels> playerModels = new Dictionary<int, PlayerModels>();

        public class PlayerModels
        {
            public string PlayerModelPath { get; set; }
        }

        public void OnConfigParsed(PluginConfig config)
        {
            GameDir = Server.GameDirectory;
            if (config.Version < this.Config.Version)
            {
                Logger.LogWarning("Configuration is out of date. Consider updating the plugin.");

                if (this.Migrations.HasInstruction(config.Version, this.Config.Version))
                {
                    base.Logger.LogWarning("Instruction for migrating your config file: {0}", this.Migrations.GetInstruction(config.Version, this.Config.Version));
                }
                else
                {
                    base.Logger.LogWarning("No migrating instruction available");
                }
            }

            if (string.IsNullOrEmpty(config.CreatePrecacheContextSignature.Get()))
            {
                throw new Exception("Signature is missing or invalid for 'CreatePrecacheContext'");
            }

            if (string.IsNullOrEmpty(config.PrecacheResourceSignature.Get()))
            {
                throw new Exception("Signature is missing or invalid for 'PrecacheResource'");
            }

            if (config.ResourceList.Count == 0)
            {
                base.Logger.LogWarning("'ResourceList' is empty, did you forget to populate the list with resources?");
            }

            this.Config = config;
        }

        public override void Load(bool hotReload)
        {
            if (hotReload)
            {
                Logger.LogWarning("Hotreloading {ModuleName} has no effect.", this.ModuleName);
            }

            this.PrecacheContext.Initialize();

            base.RegisterListener<Listeners.OnMapStart>(map =>
            {
                foreach (var resourcePath in this.Config.ResourceList)
                {
                    this.PrecacheContext.AddResource(resourcePath);
                }
            });

            RegisterEventHandler<EventPlayerSpawned>((@event, info) =>
            {
                if (@event.Userid == null) return HookResult.Continue;

                var player = @event.Userid;

                if (player.IsBot || !player.IsValid || player == null)
                {
                    return HookResult.Continue;
                }
                else
                {
                    _ = SetModelOnSpawn(player);
                    return HookResult.Continue;
                }
            });

            RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
            {
                if (@event.Userid.IsValid)
                {
                    var player = @event.Userid;

                    if (!player.IsValid || player.IsBot)
                    {
                        return HookResult.Continue;
                    }
                    else
                    {
                        OnPlayerConnect(player);
                        return HookResult.Continue;
                    }
                }
                else
                {
                    return HookResult.Continue;
                }
            });

            RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
            {
                if (@event.Userid.IsValid)
                {
                    var player = @event.Userid;

                    if (player.IsBot || !player.IsValid)
                    {
                        return HookResult.Continue;
                    }
                    else
                    {
                        OnPlayerDisconnect(player);
                        return HookResult.Continue;
                    }
                }
                else
                {
                    return HookResult.Continue;
                }
            });
        }

        public override void Unload(bool hotReload)
        {
            this.PrecacheContext.Release();
        }

        private void OnPlayerConnect(CCSPlayerController? player, bool isForBot = false)
        {
            try
            {
                if (player == null)
                {
                    SharpTimerMSError("Player object is null.");
                    return;
                }

                if (player.PlayerPawn == null)
                {
                    SharpTimerMSError("PlayerPawn is null.");
                    return;
                }

                int playerSlot = player.Slot;

                try
                {
                    playerModels[playerSlot] = new PlayerModels
                    {
                        PlayerModelPath = GetResourceValue(Path.Join(GameDir + "/csgo/addons/counterstrikesharp/configs/plugins/SharpTimerMS", "SharpTimerMS.json"))
                    };
                }
                finally
                {
                    if (playerModels[playerSlot] == null)
                    {
                        playerModels.Remove(playerSlot);
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerMSError($"Error in OnPlayerConnect: {ex.Message}");
            }
        }

        private void OnPlayerDisconnect(CCSPlayerController? player, bool isForBot = false)
        {
            if (player == null) return;

            try
            {
                playerModels.Remove(player.Slot);
            }
            catch (Exception ex)
            {
                SharpTimerMSError($"Error in OnPlayerDisconnect: {ex.Message}");
            }
        }

        [ConsoleCommand("css_setmodel", "sets your model by index from cfg")]
        [CommandHelper(minArgs: 1, usage: "!setmodel [index]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void SetModelAsync(CCSPlayerController? player, CommandInfo command)
        {
            _ = SetModelAsync(player, command.GetArg(1));
        }

        public async Task SetModelAsync(CCSPlayerController? player, string arg)
        {
            if (Config.vipOnly)
            {
                if (await GetVipStatus(player) == false)
                {
                    Server.NextFrame(() => player.PrintToChat($" {ChatColors.Gold}[SharpTimerModelSetter]{ChatColors.Default} You are not VIP."));
                    return;
                }
            }

            if (int.TryParse(arg, out var index))
            {
                string ResourcePrecacherCfg = Path.Join(GameDir + "/csgo/addons/counterstrikesharp/configs/plugins/SharpTimerMS", "SharpTimerMS.json");
                string modelpath = GetResourceValue(ResourcePrecacherCfg, index);
                if (string.IsNullOrEmpty(modelpath))
                {
                    Server.NextFrame(() => player.PrintToChat($" {ChatColors.Gold}[SharpTimerModelSetter]{ChatColors.Default} Model index invalid."));
                    return;
                }

                AddTimer(0.2f, () =>
                {
                    Server.NextFrame(() =>
                    {
                        if (player.IsBot || !player.IsValid || player == null) return;
                        player.Respawn();
                        player.Pawn.Value.SetModel(modelpath);
                        playerModels[player.Slot].PlayerModelPath = modelpath;
                        Console.WriteLine($"[SharpTimerModelSetter] Model set to {modelpath} for {player.PlayerName} from chat command");
                        string modelName = Path.GetFileNameWithoutExtension(modelpath);
                        player.PrintToChat($" {ChatColors.Gold}[SharpTimerModelSetter]{ChatColors.Default} Model set to: {modelName}");
                    });
                });
            }
            else
            {
                player.PrintToChat($" {ChatColors.Gold}[SharpTimerModelSetter]{ChatColors.Default} Model index invalid, idiot.");
            }
        }

        [ConsoleCommand("css_models", "lists all models")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void ListModels(CCSPlayerController? player, CommandInfo command)
        {
            string ResourcePrecacherCfg = Path.Join(GameDir + "/csgo/addons/counterstrikesharp/configs/plugins/SharpTimerMS", "SharpTimerMS.json");
            _ = PrintAllResources(player, ResourcePrecacherCfg);
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

        public async Task SetModelOnSpawn(CCSPlayerController player)
        {
            if (Config.setOnSpawn == false) return;

            if (Config.vipOnly == true)
            {
                if (await GetVipStatus(player) == false) return;
            }

            AddTimer(0.2f, () =>
            {
                if (player.IsBot || !player.IsValid || player == null) return;
                player.Pawn.Value.SetModel(playerModels[player.Slot].PlayerModelPath);
            });
        }

        public async Task PrintAllResources(CCSPlayerController? player, string filePath)
        {
            if (Config.vipOnly)
            {
                if (await GetVipStatus(player) == false)
                {
                    Server.NextFrame(() => player.PrintToChat($" {ChatColors.Gold}[SharpTimerModelSetter]{ChatColors.Default} You are not VIP."));
                    return;
                }
            }

            try
            {
                string jsonString = File.ReadAllText(filePath);

                // Trim leading characters, if any
                jsonString = jsonString.Trim();

                JsonDocument jsonDocument = JsonDocument.Parse(jsonString);
                JsonElement root = jsonDocument.RootElement;
                JsonElement resourcesArray = root.GetProperty("Resources");

                int index = 0;
                foreach (JsonElement resource in resourcesArray.EnumerateArray())
                {
                    player.PrintToChat($" {ChatColors.Gold}[SharpTimerModelSetter]{ChatColors.Default} #{index++}: {Path.GetFileNameWithoutExtension(resource.GetString())}");
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("JSON file not found.");
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing JSON. {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        public static string GetResourceValue(string filePath, int index = 0)
        {
            try
            {
                string jsonString = File.ReadAllText(filePath);

                // Trim leading characters, if any
                jsonString = jsonString.Trim();

                JsonDocument jsonDocument = JsonDocument.Parse(jsonString);
                JsonElement root = jsonDocument.RootElement;

                if (root.TryGetProperty("Resources", out JsonElement resourcesArray))
                {
                    if (resourcesArray.GetArrayLength() > index)
                    {
                        string resourceValue = resourcesArray[index].GetString();
                        return resourceValue;
                    }
                    else
                    {
                        Console.WriteLine($"Resource index {index} out of range.");
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine("No 'Resources' array found in JSON.");
                    return null;
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("JSON file not found.");
                return null;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing JSON. {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return null;
            }
        }

        private async Task<bool> GetVipStatus(CCSPlayerController? player)
        {
            var connectionString = GetConnectionStringFromConfigFile();
            var isVip = false;

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                const string query = "SELECT IsVip FROM PlayerStats WHERE SteamID = @SID";

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SID", player.SteamID.ToString());

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            isVip = reader.GetBoolean("IsVip");
                        }
                    }
                }
            }

            return isVip;
        }

        private string GetConnectionStringFromConfigFile()
        {
            string mysqlConfigFileName = "SharpTimer/mysqlConfig.json";
            string mySQLpath = Path.Join(Server.GameDirectory + "/csgo/cfg", mysqlConfigFileName);

            try
            {
                using (JsonDocument jsonConfig = LoadJson(mySQLpath))
                {
                    if (jsonConfig != null)
                    {
                        JsonElement root = jsonConfig.RootElement;

                        string host = root.TryGetProperty("MySqlHost", out var hostProperty) ? hostProperty.GetString() : "localhost";
                        string database = root.TryGetProperty("MySqlDatabase", out var databaseProperty) ? databaseProperty.GetString() : "database";
                        string username = root.TryGetProperty("MySqlUsername", out var usernameProperty) ? usernameProperty.GetString() : "root";
                        string password = root.TryGetProperty("MySqlPassword", out var passwordProperty) ? passwordProperty.GetString() : "root";
                        int port = root.TryGetProperty("MySqlPort", out var portProperty) ? portProperty.GetInt32() : 3306;
                        int timeout = root.TryGetProperty("MySqlTimeout", out var timeoutProperty) ? timeoutProperty.GetInt32() : 30;

                        return $"Server={host};Database={database};User ID={username};Password={password};Port={port};CharSet=utf8mb4;Connection Timeout={timeout};";
                    }
                    else
                    {
                        SharpTimerMSError($"mySQL json was null");
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerMSError($"Error in GetConnectionString: {ex.Message}");
            }

            return "Server=localhost;Database=database;User ID=root;Password=root;Port=3306;CharSet=utf8mb4;Connection Timeout=30;";
        }

        private JsonDocument LoadJson(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    return JsonDocument.Parse(json);
                }
                catch (Exception ex)
                {
                    SharpTimerMSError($"Error parsing JSON file: {path}, Error: {ex.Message}");
                }
            }

            return null;
        }

        public void SharpTimerMSError(string msg)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] \u001b[31m[SharpTimerMS ERROR] \u001b[37m{msg}");
        }
    }
}
