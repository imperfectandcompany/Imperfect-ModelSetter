# SharpTimerModelSetter
⚠️Currently not compatible with windows

This plugin is a secondary Plugin that works with SharpTimer in tandem.
It will pre-cache and set the players PlayerModel on Spawn.

## Player Commands:
* `!models` - lists all models loaded
* `!setmodel <index>` - sets the model by index

## Server CFG vars:
* `sharptimer_modelsetter_vip_only` - Wheter to only allow vip the access to it or not. Default value: false
* `sharptimer_modelsetter_set_model_on_spawn` - Wheter to set the first player model from the list on spawn or not. Default value: true

## How to add models:
Make sure you are running the [MultiAddonManager](https://github.com/Source2ZE/MultiAddonManager) MetaMod and mount your vpk with all the custom Models

*If you are running ResourcePrecacher make sure to remove it as this plugin already has the same functionality and is based uppon ResourcePrecacher*

Then head to `/csgo/addons/counterstrikesharp/configs/plugins/SharpTimerMS` and open `SharpTimerMS.json`.
After that add your models to the `"Resources"` array, like this:

```jsonc
{
  "Resources": [
     "characters/models/dea/choi/choi.vmdl",
     "characters/models/dea/9s/nier_9s.vmdl",
     // ..
     // ..
     // ..
  ],
  "CreatePrecacheContextSignature": {
    // ...
  },
  "PrecacheResourceSignature": {
    // ...
  },
  "ConfigVersion": 1
}
```
*Note that the first model in the list will be the default model applied on spawn regardless of vip*

On the final step make sure all your vpk assets are sitting in `/csgo/.../.../` in your server files otherwise animations will break!
