﻿namespace SharpTimerModelSetter
{
    using CounterStrikeSharp.API.Core;
    using Microsoft.Extensions.DependencyInjection;

    public class PluginServices : IPluginServiceCollection<Plugin>
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<PluginMigrations>();
            serviceCollection.AddSingleton<PrecacheContext>();
        }
    }
}
