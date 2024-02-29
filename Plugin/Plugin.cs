namespace SharpTimerModelSetter
{
    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Core.Attributes;
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
                    _ = SetModelOnSpawn(player, player.SteamID.ToString());
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
    }
}
