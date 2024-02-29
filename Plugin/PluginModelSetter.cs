namespace SharpTimerModelSetter
{
    using System.Text.Json;
    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Core;
    using MySqlConnector;
    using CounterStrikeSharp.API.Modules.Utils;

    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {
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
                    playerModels[playerSlot] = new PlayerModels();
                    playerModels[playerSlot].PlayerModelPath = GetResourceValue(Path.Join(GameDir + "/csgo/addons/counterstrikesharp/configs/plugins/SharpTimerMS", "SharpTimerMS.json"));

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

        public async Task SetModelAsync(CCSPlayerController? player, string arg, string SteamID)
        {
            if (Config.vipOnly)
            {
                if (await GetVipStatus(SteamID) == false)
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
            }
            else
            {
                Server.NextFrame(() => player.PrintToChat($" {ChatColors.Gold}[SharpTimerModelSetter]{ChatColors.Default} Model index invalid, idiot."));
            }
        }

        public async Task SetModelOnSpawn(CCSPlayerController player, string SteamID)
        {
            if (Config.setOnSpawn == false) return;

            if (Config.vipOnly == true)
            {
                if (await GetVipStatus(SteamID) == false) return;
            }

            AddTimer(2f, () => //2sec delay for when the player spawns while not fully connected c:
            {
                Server.NextFrame(() => 
                {
                    if (player.IsBot || !player.IsValid || player == null) return;
                    player.Pawn.Value.SetModel(playerModels[player.Slot].PlayerModelPath);
                });
            });
        }

        public async Task PrintAllResources(CCSPlayerController? player, string filePath, string SteamID)
        {
            if (Config.vipOnly)
            {
                if (await GetVipStatus(SteamID) == false)
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
                    Server.NextFrame(() => player.PrintToChat($" {ChatColors.Gold}[SharpTimerModelSetter]{ChatColors.Default} #{index++}: {Path.GetFileNameWithoutExtension(resource.GetString())}"));
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

        private async Task<bool> GetVipStatus(string SteamID)
        {
            var connectionString = GetConnectionStringFromConfigFile();
            var isVip = false;

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                const string query = "SELECT IsVip FROM PlayerStats WHERE SteamID = @SID";

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SID", SteamID);

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
