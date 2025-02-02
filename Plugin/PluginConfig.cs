﻿namespace SharpTimerModelSetter
{
    using CounterStrikeSharp.API.Core;

    using System.Text.Json.Serialization;

    public sealed class PluginConfig : BasePluginConfig
    {
        public bool vipOnly = false;
        public bool setOnSpawn = true;
        
        [JsonPropertyName("Resources")]
        public HashSet<string> ResourceList { get; set; } = new HashSet<string>();

        public bool Log { get; set; } = true;

        public WIN_LINUX<string> CreatePrecacheContextSignature { get; set; } = new(string.Empty, string.Empty);

        public WIN_LINUX<string> PrecacheResourceSignature { get; set; } = new(string.Empty, string.Empty);

        public override int Version { get; set; } = 3;
    }
}
