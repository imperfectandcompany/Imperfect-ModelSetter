namespace SharpTimerModelSetter
{
    using CounterStrikeSharp.API.Core;

    public sealed partial class Plugin : BasePlugin
    {
        public override string ModuleName => "SharpTimerModelSetter";

        public override string ModuleAuthor => "Nexd @ Eternar (https://eternar.dev) Modified by DEAFPS";

        public override string ModuleDescription => "Automatically precache and apply playermodels.";

        public override string ModuleVersion => "1.0.0 " +
#if RELEASE
            "(release)";
#else
            "(debug)";
#endif
    }
}
