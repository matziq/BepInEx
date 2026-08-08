using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using SunkenlandLocalizationAPI.Api;

namespace SunkenlandUtilDocs;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(UtilPluginGuid)]
[BepInDependency(LocalizationPluginGuid)]
public sealed class SunkenlandUtilDocsPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "local.sunkenland.util.docs";
    public const string PluginName = "Sunkenland Util Setting Guide";
    public const string PluginVersion = "1.0.0";

    private const string UtilPluginGuid = "satroki.sunkenland.util";
    private const string LocalizationPluginGuid = "IceBoxStudio.Sunkenland.LocalizationAPI";

    private static readonly FieldInfo DescriptionField = typeof(ConfigEntryBase).GetField(
        "<Description>k__BackingField",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly IReadOnlyDictionary<string, SettingDoc> SettingDocs =
        new[]
        {
            Number("StackAmount", "Stack multiplier (1 = normal; no hard maximum)",
                "Multiplies each stackable item's original stack limit after resources load. Values <= 1 leave original limits unchanged. No hard maximum is enforced; very large products can overflow or destabilize inventory logic. Restart required."),
            Number("MaxEnergy", "Additional maximum energy (0 = none; no hard maximum)",
                "Adds this amount to the player's maximum energy whenever stats are calculated. 0 adds nothing; negative values reduce the maximum. No hard maximum is enforced."),
            Number("MaxAir", "Additional maximum air (0 = none; no hard maximum)",
                "Adds this amount to the player's maximum air whenever stats are calculated. 0 adds nothing; negative values reduce the maximum. No hard maximum is enforced."),
            Number("MaxHealth", "Additional maximum health (0 = none; no hard maximum)",
                "Adds this amount to the player's maximum health whenever stats are calculated. 0 adds nothing; negative values reduce the maximum. No hard maximum is enforced."),
            Number("Defence", "Additional body and head defence (0 = none; no hard maximum)",
                "Adds this amount to both body defence and head defence. 0 adds nothing; negative values reduce defence. No hard maximum is enforced."),
            Number("MaxItemsAmount", "Additional bag slots (100 total-slot hard cap)",
                "Adds slots to the player's bag when its maximum is set. The utility caps the final total at 100 slots, so the effective maximum addition depends on the game's base slot count. Negative values can reduce slots."),
            Number("CollectableByToolHitDropRate", "Items produced per tool hit (1 = normal; no hard maximum)",
                "Sets the amount produced by CollectableByToolHit interactions. Despite the original 'multiplier' label, this is an exact amount, not multiplication of the normal drop. Values <= 1 leave game behavior unchanged. No hard maximum is enforced. Restart required."),
            Number("AirConsumtionRate", "Air-consumption multiplier (0 = no use; 1 = normal)",
                "Multiplies air consumption. 0 stops air consumption, 0.5 halves it, 1 is normal, and 2 doubles it. Negative values reverse the change and can refill air. No hard maximum is enforced."),
            Number("AirTankRatio", "Air-tank efficiency multiplier (1 = normal; must be > 0)",
                "Multiplies the amount of air supplied by tanks. 2 doubles tank efficiency. Values <= 0 and exactly 1 leave game behavior unchanged; 0 does not disable tank air. No hard maximum is enforced. Restart required."),
            Number("EnergyConsumptionRate", "Energy-consumption multiplier (0 = no use; 1 = normal)",
                "Multiplies energy consumption. 0 stops consumption, 0.5 halves it, 1 is normal, and 2 doubles it. Negative values reverse the change and can restore energy. No hard maximum is enforced."),
            Number("StaminaRecoveryRate", "Stamina-recovery multiplier (1 = normal; no hard maximum)",
                "Multiplies stamina regenerated per second. 0 disables recovery, 1 is normal, and 2 doubles recovery. Negative values drain stamina instead. No hard maximum is enforced."),
            Number("HealthRecoveryRate", "Health-recovery multiplier (1 = normal; no hard maximum)",
                "Multiplies health recovery. 0 disables recovery, 1 is normal, and 2 doubles recovery. Negative values can turn recovery into damage. No hard maximum is enforced."),
            Number("FoodConsumtionRate", "Food-consumption multiplier (0 = no use; 1 = normal)",
                "Multiplies food consumption. 0 stops consumption, 0.5 halves it, 1 is normal, and 2 doubles it. Negative values reverse the change and can refill food. No hard maximum is enforced."),
            Number("WaterConsumtionRate", "Water-consumption multiplier (0 = no use; 1 = normal)",
                "Multiplies water consumption. 0 stops consumption, 0.5 halves it, 1 is normal, and 2 doubles it. Negative values reverse the change and can refill water. No hard maximum is enforced."),
            Number("AdditionalSwimSpped", "Additional swim speed (0 = none; no hard maximum)",
                "Adds this value directly to the game's swim-speed value. This is additive, not a multiplier. 0 makes no change; negative values slow swimming. No hard maximum is enforced."),
            Number("AdditionalWalkSpped", "Additional walk and sprint speed (0 = none; no hard maximum)",
                "Adds this value directly to both walk speed and sprint speed. This is additive, not a multiplier. 0 makes no change; negative values slow movement. No hard maximum is enforced."),
            Toggle("WorldSensor", "World sensor", "ON: shows the nearest supported interactable within 200 units and enables the ore-sensor update. OFF: hides and stops the sensor."),
            Text("SensorFilter", "Sensor ignore prefixes (comma-separated)",
                "Comma-separated object-name prefixes to ignore, such as chair,table,cabinet. Matching is case-insensitive and starts at the beginning of the internal object name. Empty means no filter."),
            Text("SensorPriority", "Sensor priority prefixes (comma-separated)",
                "Comma-separated object-name prefixes to prefer over nearer ordinary objects within 200 units. Matching is case-insensitive. Empty means nearest-object behavior only."),
            Toggle("ScanOre", "Ore sensor", "ON: while World Sensor is also on, tracks the nearest matching ore node within 300 units. OFF: does not scan ore."),
            Text("ScanOreTypes", "Ore types (empty = all; comma-separated)",
                "Limits ore scanning to exact enum names separated by commas. Empty scans all types. Known values: MineCopper, MineIron, MineSulfur, MineAnatase, MineQuartz, Clay, IronOreVeins, CopperOreVeins, Other. Invalid names can prevent sensor initialization."),
            Number("SensorSpan", "Sensor refresh interval in frames (30 default; no hard maximum)",
                "Number of UI update frames between world scans. Lower values refresh more often and cost more CPU. Values <= 0 scan every frame. No hard maximum is enforced."),
            Number("SensorX", "Sensor horizontal position in pixels",
                "Positive values position from the left edge. Negative values position from the right edge; for example, -150 is 150 pixels from the right. No hard bounds are enforced, so large values can move the panel off-screen."),
            Number("SensorY", "Sensor vertical position in pixels",
                "Positive values position from the bottom edge. Negative values position from the top edge; for example, -75 is 75 pixels from the top. No hard bounds are enforced, so large values can move the panel off-screen."),
            Number("SensorScale", "Sensor UI scale (1 = normal; must be positive)",
                "Scales the sensor panel and text. 1 is normal size, 0.5 is half size, and 2 is double size. No validation or hard maximum is enforced; zero or negative values can make the UI unusable."),
            Toggle("DamageArmor", "Armor durability damage", "ON: armor takes durability damage normally. OFF: skips the game's armor-damage method, preventing durability loss."),
            Number("MetalProcessingDuration", "Metal processing seconds (0 = game default 30)",
                "Overrides furnace and steel-furnace processing duration when greater than 0. Smaller positive values process faster. 0 or a negative value preserves the game's value. No hard maximum is enforced."),
            Number("DecomposeTime", "Decompose-table seconds (0 = game default 30)",
                "Overrides decompose-table processing time when greater than 0. Smaller positive values process faster. 0 or a negative value preserves the game's value. No hard maximum is enforced."),
            Number("SawmillNeedTime", "Sawmill seconds (0 = game default 30)",
                "Overrides sawmill processing time when greater than 0. Smaller positive values process faster. 0 or a negative value preserves the game's value. No hard maximum is enforced."),
            Number("FirearmsRecoveryTime", "Firearm recovery seconds (0 = game default 30)",
                "Overrides firearm recovery time while the station is working when greater than 0. Smaller positive values process faster. 0 or a negative value preserves the game's value. No hard maximum is enforced."),
            Number("HeadLightBatteryPowerConsumption", "Headlight battery use/second (0 = game default 0.01)",
                "Overrides headlight battery consumption per second only when greater than 0. Smaller positive values last longer. 0 and negative values preserve the game's value; negative values do not recharge the battery. No hard maximum is enforced."),
            Number("NVDBatteryPowerConsumption", "Night-vision battery use/second (0 = game default 0.01)",
                "Overrides night-vision battery consumption per second only when greater than 0. Smaller positive values last longer. 0 and negative values preserve the game's value; negative values do not recharge the battery. No hard maximum is enforced."),
            Toggle("SleepAnytime", "Sleep at any time", "ON: the state-authoritative player is continually allowed to sleep regardless of the normal schedule. OFF: uses normal sleep restrictions."),
            Toggle("DestroyReturnAll", "Full materials on demolition", "ON: removes the game's reduced-refund calculation so demolition returns all configured materials. OFF: uses normal refunds. Restart required because this is applied when Harmony patches load."),
            Number("BoatSpeedRate", "Boat engine-power multiplier (1 = normal; no hard maximum)",
                "Multiplies engine power when each boat spawns. 0 removes engine power, 1 is normal, and 2 doubles it. Negative values reverse power. No hard maximum is enforced; reload the world to affect newly spawned boats."),
            Number("EnemyDisplayCount", "Enemy marker threshold (5 = game default; no hard maximum)",
                "Changes the enemy-count threshold used by location display logic. 5 preserves the original method. No hard maximum is enforced. Restart required.")
        }.ToDictionary(doc => doc.Key, StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        if (DescriptionField == null)
        {
            Logger.LogError("Cannot locate BepInEx config description field; setting guide was not applied.");
            return;
        }

        if (!Chainloader.PluginInfos.TryGetValue(UtilPluginGuid, out var pluginInfo) ||
            pluginInfo.Instance is not BaseUnityPlugin utilPlugin)
        {
            Logger.LogError("Sunkenland Util Plugin is loaded but its configuration could not be accessed.");
            return;
        }

        var localizer = LocalizationApi.For(UtilPluginGuid);
        var applied = 0;

        foreach (var entry in utilPlugin.Config)
        {
            if (!SettingDocs.TryGetValue(entry.Key.Key, out var doc))
                continue;

            DescriptionField.SetValue(entry.Value, new ConfigDescription(doc.Description));
            localizer.RegisterConfigDisplayName(entry.Value, doc.DisplayName);
            applied++;
        }

        utilPlugin.Config.Save();
        Logger.LogInfo($"Applied detailed guidance to {applied} Sunkenland Util settings.");
    }

    private static SettingDoc Number(string key, string displayName, string description) =>
        new(key, displayName, description);

    private static SettingDoc Text(string key, string displayName, string description) =>
        new(key, displayName, description);

    private static SettingDoc Toggle(string key, string displayName, string description) =>
        new(key, $"{displayName} (checkbox)", description);

    private sealed class SettingDoc
    {
        public SettingDoc(string key, string displayName, string description)
        {
            Key = key;
            DisplayName = displayName;
            Description = description;
        }

        public string Key { get; }

        public string DisplayName { get; }

        public string Description { get; }
    }
}
