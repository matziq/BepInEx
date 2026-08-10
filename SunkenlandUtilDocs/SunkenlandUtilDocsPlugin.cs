using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ModSettingsMenu.Api;
using SunkenlandLocalizationAPI.Api;
using UnityEngine;

namespace SunkenlandUtilDocs;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(UtilPluginGuid)]
[BepInDependency(LocalizationPluginGuid)]
[BepInDependency(SettingsMenuPluginGuid)]
public sealed class SunkenlandUtilDocsPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "local.sunkenland.util.docs";
    public const string PluginName = "Sunkenland Util Setting Guide";
    public const string PluginVersion = "2.0.0";

    private const string UtilPluginGuid = "satroki.sunkenland.util";
    private const string LocalizationPluginGuid = "IceBoxStudio.Sunkenland.LocalizationAPI";
    private const string SettingsMenuPluginGuid = "IceBoxStudio.Sunkenland.ModSettingsMenu";

    private ConfigFile _utilConfig;
    private Dictionary<string, ConfigEntryBase> _utilEntries;
    private ConfigEntry<UtilityPreset> _preset;
    private bool _applyingPreset;
    private string _lastSummary;
    private float _nextSummaryRefresh;

    private static readonly FieldInfo DescriptionField = typeof(ConfigEntryBase).GetField(
        "<Description>k__BackingField",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly IReadOnlyDictionary<string, SettingDoc> SettingDocs =
        new[]
        {
            Number("StackAmount", "Stack multiplier (safe 1-25,000; restart)",
                "Multiplies each stackable item's original stack limit after resources load. Values <= 1 leave original limits unchanged. The utility has no native maximum; the guide limits editing to 25,000 to prevent integer overflow. Restart required.",
                IntRange(1, 25000)),
            Number("MaxEnergy", "Additional maximum energy (safe 0-1,000)",
                "Adds this amount to the player's maximum energy whenever stats are calculated. 0 adds nothing. Safe editing range: 0 to 1,000.",
                FloatRange(0f, 1000f)),
            Number("MaxAir", "Additional maximum air (safe 0-1,000)",
                "Adds this amount to the player's maximum air whenever stats are calculated. 0 adds nothing. Safe editing range: 0 to 1,000.",
                FloatRange(0f, 1000f)),
            Number("MaxHealth", "Additional maximum health (safe 0-1,000)",
                "Adds this amount to the player's maximum health whenever stats are calculated. 0 adds nothing. Safe editing range: 0 to 1,000.",
                FloatRange(0f, 1000f)),
            Number("Defence", "Additional body and head defence (safe 0-500)",
                "Adds this amount to both body defence and head defence. 0 adds nothing. Safe editing range: 0 to 500.",
                IntRange(0, 500)),
            Number("MaxItemsAmount", "Additional bag slots (100 total-slot hard cap)",
                "Adds slots to the player's bag when its maximum is set. The utility caps the final total at 100 slots, so the effective maximum addition depends on the game's base slot count. Safe editing range: 0 to 100.",
                IntRange(0, 100)),
            Number("CollectableByToolHitDropRate", "Items produced per tool hit (safe 1-100; restart)",
                "Sets the amount produced by CollectableByToolHit interactions. Despite the original 'multiplier' label, this is an exact amount, not multiplication of the normal drop. Safe editing range: 1 to 100. Restart required.",
                IntRange(1, 100)),
            Number("AirConsumtionRate", "Air-consumption multiplier (0 = no use; 1 = normal)",
                "Multiplies air consumption. 0 stops air consumption, 0.5 halves it, 1 is normal, and 2 doubles it. Safe editing range: 0 to 5.",
                FloatRange(0f, 5f)),
            Number("AirTankRatio", "Air-tank efficiency multiplier (1 = normal; must be > 0)",
                "Multiplies the amount of air supplied by tanks. 2 doubles tank efficiency. Exactly 1 leaves game behavior unchanged. Safe editing range: 0.1 to 10. Restart required.",
                FloatRange(0.1f, 10f)),
            Number("EnergyConsumptionRate", "Energy-consumption multiplier (0 = no use; 1 = normal)",
                "Multiplies energy consumption. 0 stops consumption, 0.5 halves it, 1 is normal, and 2 doubles it. Safe editing range: 0 to 5.",
                FloatRange(0f, 5f)),
            Number("StaminaRecoveryRate", "Stamina-recovery multiplier (safe 0-20; 1 normal)",
                "Multiplies stamina regenerated per second. 0 disables recovery, 1 is normal, and 2 doubles recovery. Safe editing range: 0 to 20.",
                FloatRange(0f, 20f)),
            Number("HealthRecoveryRate", "Health-recovery multiplier (safe 0-20; 1 normal)",
                "Multiplies health recovery. 0 disables recovery, 1 is normal, and 2 doubles recovery. Safe editing range: 0 to 20.",
                FloatRange(0f, 20f)),
            Number("FoodConsumtionRate", "Food-consumption multiplier (0 = no use; 1 = normal)",
                "Multiplies food consumption. 0 stops consumption, 0.5 halves it, 1 is normal, and 2 doubles it. Safe editing range: 0 to 5.",
                FloatRange(0f, 5f)),
            Number("WaterConsumtionRate", "Water-consumption multiplier (0 = no use; 1 = normal)",
                "Multiplies water consumption. 0 stops consumption, 0.5 halves it, 1 is normal, and 2 doubles it. Safe editing range: 0 to 5.",
                FloatRange(0f, 5f)),
            Number("AdditionalSwimSpped", "Additional swim speed (safe 0-20)",
                "Adds this value directly to the game's swim-speed value. This is additive, not a multiplier. Safe editing range: 0 to 20.",
                FloatRange(0f, 20f)),
            Number("AdditionalWalkSpped", "Additional walk and sprint speed (safe 0-20)",
                "Adds this value directly to both walk speed and sprint speed. This is additive, not a multiplier. Safe editing range: 0 to 20.",
                FloatRange(0f, 20f)),
            Toggle("WorldSensor", "World sensor", "ON: shows the nearest supported interactable within 200 units and enables the ore-sensor update. OFF: hides and stops the sensor."),
            Text("SensorFilter", "Sensor ignore prefixes (comma-separated)",
                "Comma-separated object-name prefixes to ignore, such as chair,table,cabinet. Matching is case-insensitive and starts at the beginning of the internal object name. Empty means no filter."),
            Text("SensorPriority", "Sensor priority prefixes (comma-separated)",
                "Comma-separated object-name prefixes to prefer over nearer ordinary objects within 200 units. Matching is case-insensitive. Empty means nearest-object behavior only."),
            Toggle("ScanOre", "Ore sensor", "ON: while World Sensor is also on, tracks the nearest matching ore node within 300 units. OFF: does not scan ore."),
            Text("ScanOreTypes", "Ore types (empty = all; comma-separated)",
                "Limits ore scanning to exact enum names separated by commas. Empty scans all types. Known values: MineCopper, MineIron, MineSulfur, MineAnatase, MineQuartz, Clay, IronOreVeins, CopperOreVeins, Other. Invalid names can prevent sensor initialization."),
            Number("SensorSpan", "Sensor refresh interval (safe 1-300 frames)",
                "Number of UI update frames between world scans. Lower values refresh more often and cost more CPU. Safe editing range: 1 to 300.",
                IntRange(1, 300)),
            Number("SensorX", "Sensor horizontal position in pixels",
                "Positive values position from the left edge. Negative values position from the right edge; for example, -150 is 150 pixels from the right. Safe editing range: -4,000 to 4,000.",
                FloatRange(-4000f, 4000f)),
            Number("SensorY", "Sensor vertical position in pixels",
                "Positive values position from the bottom edge. Negative values position from the top edge; for example, -75 is 75 pixels from the top. Safe editing range: -4,000 to 4,000.",
                FloatRange(-4000f, 4000f)),
            Number("SensorScale", "Sensor UI scale (1 = normal; must be positive)",
                "Scales the sensor panel and text. 1 is normal size, 0.5 is half size, and 2 is double size. Safe editing range: 0.5 to 3.",
                FloatRange(0.5f, 3f)),
            Toggle("DamageArmor", "Armor durability damage", "ON: armor takes durability damage normally. OFF: skips the game's armor-damage method, preventing durability loss."),
            Number("MetalProcessingDuration", "Metal processing seconds (0 = game default 30)",
                "Overrides furnace and steel-furnace processing duration when greater than 0. Smaller positive values process faster. 0 preserves the game's value. Safe editing range: 0 to 300.",
                FloatRange(0f, 300f)),
            Number("DecomposeTime", "Decompose-table seconds (0 = game default 30)",
                "Overrides decompose-table processing time when greater than 0. Smaller positive values process faster. 0 preserves the game's value. Safe editing range: 0 to 300.",
                IntRange(0, 300)),
            Number("SawmillNeedTime", "Sawmill seconds (0 = game default 30)",
                "Overrides sawmill processing time when greater than 0. Smaller positive values process faster. 0 preserves the game's value. Safe editing range: 0 to 300.",
                IntRange(0, 300)),
            Number("FirearmsRecoveryTime", "Firearm recovery seconds (0 = game default 30)",
                "Overrides firearm recovery time while the station is working when greater than 0. Smaller positive values process faster. 0 preserves the game's value. Safe editing range: 0 to 300.",
                IntRange(0, 300)),
            Number("HeadLightBatteryPowerConsumption", "Headlight battery use/second (0 = game default 0.01)",
                "Overrides headlight battery consumption per second only when greater than 0. Smaller positive values last longer. 0 and negative values preserve the game's value; negative values do not recharge the battery. No hard maximum is enforced."),
            Number("NVDBatteryPowerConsumption", "Night-vision battery use/second (0 = game default 0.01)",
                "Overrides night-vision battery consumption per second only when greater than 0. Smaller positive values last longer. 0 and negative values preserve the game's value; negative values do not recharge the battery. No hard maximum is enforced."),
            Toggle("SleepAnytime", "Sleep at any time", "ON: the state-authoritative player is continually allowed to sleep regardless of the normal schedule. OFF: uses normal sleep restrictions."),
            Toggle("DestroyReturnAll", "Full materials on demolition", "ON: removes the game's reduced-refund calculation so demolition returns all configured materials. OFF: uses normal refunds. Restart required because this is applied when Harmony patches load."),
            Number("BoatSpeedRate", "Boat engine-power multiplier (safe 0-10; 1 normal)",
                "Multiplies engine power when each boat spawns. 0 removes engine power, 1 is normal, and 2 doubles it. Safe editing range: 0 to 10; reload the world to affect newly spawned boats.",
                FloatRange(0f, 10f)),
            Number("EnemyDisplayCount", "Enemy marker threshold (safe 0-100; restart)",
                "Changes the enemy-count threshold used by location display logic. 5 preserves the original method. Safe editing range: 0 to 100. Restart required.",
                IntRange(0, 100))
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

        _utilConfig = utilPlugin.Config;
        _utilEntries = _utilConfig.ToDictionary(
            entry => entry.Key.Key,
            entry => entry.Value,
            StringComparer.OrdinalIgnoreCase);

        var localizer = LocalizationApi.For(PluginGuid);
        localizer.RegisterJson(Path.Combine(
            Path.GetDirectoryName(Info.Location),
            "SunkenlandUtilDocs.Localization.json"));
        var applied = 0;

        foreach (var entry in _utilConfig)
        {
            if (!SettingDocs.TryGetValue(entry.Key.Key, out var doc))
                continue;

            DescriptionField.SetValue(entry.Value, new ConfigDescription(doc.Description, doc.Range));
            localizer.RegisterConfigDisplayName(entry.Value, $"setting.{doc.Key}.name");

            var resolvedName = LocalizationApi.GetConfigDisplayName(entry.Value);
            if (string.Equals(resolvedName, entry.Value.Definition.Key, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Setting label '{doc.Key}' fell back to its original name.");

            applied++;
        }

        _preset = Config.Bind(
            "Presets",
            "Preset",
            UtilityPreset.Custom,
            "Selecting a preset immediately replaces all Util Plugin values. Custom preserves manual settings. Settings marked as restart-required still need a game restart.");
        localizer.RegisterConfigDisplayName(_preset, "setting.Preset.name");
        foreach (UtilityPreset preset in Enum.GetValues(typeof(UtilityPreset)))
            localizer.RegisterConfigDisplayValue(_preset, preset, $"preset.{preset}");

        _preset.SettingChanged += OnPresetChanged;
        _utilConfig.SettingChanged += OnUtilSettingChanged;

        Logger.LogInfo($"Loaded utility preset selection: {_preset.Value}.");
        if (_preset.Value == UtilityPreset.Custom)
        {
            _utilConfig.Save();
            RefreshSummary(true);
        }
        else
        {
            ApplyPreset(_preset.Value);
        }

        Logger.LogInfo($"Applied safe controls and verified detailed guidance for {applied} Sunkenland Util settings.");
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextSummaryRefresh)
            return;

        _nextSummaryRefresh = Time.unscaledTime + 2f;
        RefreshSummary(false);
    }

    private void OnPresetChanged(object sender, EventArgs args)
    {
        if (_preset.Value == UtilityPreset.Custom)
            return;

        ApplyPreset(_preset.Value);
    }

    private void OnUtilSettingChanged(object sender, SettingChangedEventArgs args)
    {
        _nextSummaryRefresh = 0f;
        if (_applyingPreset || _preset.Value == UtilityPreset.Custom)
            return;

        _preset.Value = UtilityPreset.Custom;
    }

    private void ApplyPreset(UtilityPreset preset)
    {
        var overrides = GetPresetOverrides(preset);
        var saveOnConfigSet = _utilConfig.SaveOnConfigSet;
        _applyingPreset = true;
        try
        {
            _utilConfig.SaveOnConfigSet = false;
            foreach (var entry in _utilEntries.Values)
                entry.BoxedValue = entry.DefaultValue;

            foreach (var value in overrides)
                _utilEntries[value.Key].BoxedValue = value.Value;

            _utilConfig.Save();
        }
        finally
        {
            _utilConfig.SaveOnConfigSet = saveOnConfigSet;
            _applyingPreset = false;
        }

        _nextSummaryRefresh = 0f;
        RefreshSummary(true);
        Logger.LogInfo($"Applied {preset} preset. Restart the game for settings marked restart-required.");
    }

    private static IReadOnlyDictionary<string, object> GetPresetOverrides(UtilityPreset preset)
    {
        switch (preset)
        {
            case UtilityPreset.Vanilla:
                return new Dictionary<string, object>();
            case UtilityPreset.RelaxedSurvival:
                return new Dictionary<string, object>
                {
                    ["StackAmount"] = 10,
                    ["MaxEnergy"] = 50f,
                    ["MaxAir"] = 50f,
                    ["MaxHealth"] = 50f,
                    ["Defence"] = 10,
                    ["MaxItemsAmount"] = 20,
                    ["CollectableByToolHitDropRate"] = 2,
                    ["AirConsumtionRate"] = 0.5f,
                    ["EnergyConsumptionRate"] = 0.5f,
                    ["StaminaRecoveryRate"] = 2f,
                    ["HealthRecoveryRate"] = 2f,
                    ["FoodConsumtionRate"] = 0.5f,
                    ["WaterConsumtionRate"] = 0.5f,
                    ["WorldSensor"] = true,
                    ["ScanOre"] = true,
                    ["MetalProcessingDuration"] = 15f,
                    ["DecomposeTime"] = 15,
                    ["SawmillNeedTime"] = 15,
                    ["FirearmsRecoveryTime"] = 15,
                    ["BoatSpeedRate"] = 1.25f,
                    ["EnemyDisplayCount"] = 10
                };
            case UtilityPreset.Builder:
                return new Dictionary<string, object>
                {
                    ["StackAmount"] = 1000,
                    ["MaxEnergy"] = 250f,
                    ["MaxAir"] = 250f,
                    ["MaxHealth"] = 250f,
                    ["Defence"] = 100,
                    ["MaxItemsAmount"] = 75,
                    ["CollectableByToolHitDropRate"] = 10,
                    ["AirConsumtionRate"] = 0f,
                    ["EnergyConsumptionRate"] = 0f,
                    ["StaminaRecoveryRate"] = 10f,
                    ["HealthRecoveryRate"] = 10f,
                    ["FoodConsumtionRate"] = 0f,
                    ["WaterConsumtionRate"] = 0f,
                    ["AdditionalSwimSpped"] = 3f,
                    ["AdditionalWalkSpped"] = 3f,
                    ["WorldSensor"] = true,
                    ["ScanOre"] = true,
                    ["SensorSpan"] = 15,
                    ["DamageArmor"] = false,
                    ["MetalProcessingDuration"] = 2f,
                    ["DecomposeTime"] = 2,
                    ["SawmillNeedTime"] = 2,
                    ["FirearmsRecoveryTime"] = 2,
                    ["SleepAnytime"] = true,
                    ["DestroyReturnAll"] = true,
                    ["BoatSpeedRate"] = 3f,
                    ["EnemyDisplayCount"] = 20
                };
            case UtilityPreset.Explorer:
                return new Dictionary<string, object>
                {
                    ["StackAmount"] = 100,
                    ["MaxEnergy"] = 100f,
                    ["MaxAir"] = 100f,
                    ["MaxHealth"] = 100f,
                    ["Defence"] = 30,
                    ["MaxItemsAmount"] = 40,
                    ["CollectableByToolHitDropRate"] = 3,
                    ["AirConsumtionRate"] = 0.5f,
                    ["AirTankRatio"] = 2f,
                    ["EnergyConsumptionRate"] = 0.5f,
                    ["StaminaRecoveryRate"] = 3f,
                    ["HealthRecoveryRate"] = 3f,
                    ["FoodConsumtionRate"] = 0.5f,
                    ["WaterConsumtionRate"] = 0.5f,
                    ["AdditionalSwimSpped"] = 2f,
                    ["AdditionalWalkSpped"] = 2f,
                    ["WorldSensor"] = true,
                    ["ScanOre"] = true,
                    ["SensorSpan"] = 10,
                    ["SleepAnytime"] = true,
                    ["BoatSpeedRate"] = 2f,
                    ["EnemyDisplayCount"] = 15
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown utility preset.");
        }
    }

    private void RefreshSummary(bool force)
    {
        if (_utilEntries == null)
            return;

        var summary = BuildSummary();
        if (!force && string.Equals(summary, _lastSummary, StringComparison.Ordinal))
            return;

        _lastSummary = summary;
        ModSettingsRegistry.Register(
            PluginGuid,
            new ModSettingsModOptions
            {
                Name = "Util Guide & Presets",
                Description = summary,
                Author = "Local companion plugin",
                Version = PluginVersion
            });
    }

    private string BuildSummary()
    {
        var text = new StringBuilder();
        text.AppendLine("LIVE CONFIGURATION");
        text.AppendLine($"Preset: {_preset?.Value.ToString() ?? UtilityPreset.Custom.ToString()}");
        text.AppendLine($"Player bonuses: +{Value("MaxHealth")} health, +{Value("MaxEnergy")} energy, +{Value("MaxAir")} air, +{Value("Defence")} body/head defence");
        text.AppendLine($"Inventory: base slots + {Value("MaxItemsAmount")} (100 total hard cap); stack limits x {Value("StackAmount")}");
        text.AppendLine($"Needs: air x{Value("AirConsumtionRate")}, energy x{Value("EnergyConsumptionRate")}, food x{Value("FoodConsumtionRate")}, water x{Value("WaterConsumtionRate")}");
        text.AppendLine($"Recovery: stamina x{Value("StaminaRecoveryRate")}, health x{Value("HealthRecoveryRate")}; tool-hit output {Value("CollectableByToolHitDropRate")}");
        text.AppendLine($"Movement: swim +{Value("AdditionalSwimSpped")}, walk/sprint +{Value("AdditionalWalkSpped")}; boat engine x{Value("BoatSpeedRate")}");
        text.AppendLine($"Machines (seconds): metal {Duration("MetalProcessingDuration")}, decompose {Duration("DecomposeTime")}, sawmill {Duration("SawmillNeedTime")}, firearm recovery {Duration("FirearmsRecoveryTime")}");
        text.AppendLine($"Sensors: world {OnOff("WorldSensor")}, ore {OnOff("ScanOre")}, refresh every {Value("SensorSpan")} frames");

        var runtime = GetRuntimeSummary();
        if (!string.IsNullOrEmpty(runtime))
        {
            text.AppendLine();
            text.AppendLine("RUNTIME EFFECTIVE VALUES");
            text.AppendLine(runtime);
        }

        text.AppendLine();
        text.AppendLine("APPLY TIMING");
        text.AppendLine("Immediate/stat refresh: player bonuses, needs, recovery, movement, armor damage, sleep, and machine times.");
        text.AppendLine("World reload/new spawn: sensors, boat power, and some inventory values.");
        text.Append("Game restart: stack limits, air-tank ratio, tool-hit output, demolition refund, and enemy marker threshold.");
        return text.ToString();
    }

    private string GetRuntimeSummary()
    {
        var globalType = FindGameType("Global");
        var global = globalType?.GetField("code", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        var player = ReadMember(global, "Player");
        if (player == null)
            return "Enter a world to populate player, inventory, movement, and item-stack values.";

        var lines = new List<string>
        {
            $"Player maxima: {Format(ReadMember(player, "MaxHealth"))} health, {Format(ReadMember(player, "MaxEnergy"))} energy, {Format(ReadMember(player, "MaxAir"))} air",
            $"Current defence: {Format(ReadMember(player, "DefenceBody"))} body, {Format(ReadMember(player, "DefenceHead"))} head"
        };

        var storage = ReadMember(player, "PlayerStorage");
        if (storage != null)
            lines.Add($"Current inventory capacity: {Format(ReadMember(storage, "MaxItemsAmount"))} slots");

        var walkerType = FindGameType("FPSRigidBodyWalker");
        var walker = walkerType?.GetField("code", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (walker != null)
            lines.Add($"Current movement: walk {Format(ReadMember(walker, "walkSpeed"))}, sprint {Format(ReadMember(walker, "sprintSpeed"))}, swim {Format(ReadMember(walker, "swimSpeed"))}");

        var stackSummary = GetStackSummary();
        if (!string.IsNullOrEmpty(stackSummary))
            lines.Add(stackSummary);

        return string.Join("\n", lines);
    }

    private string GetStackSummary()
    {
        if (!Chainloader.PluginInfos.TryGetValue(UtilPluginGuid, out var utilInfo))
            return null;

        var field = utilInfo.Instance.GetType().GetField("stackBakDict", BindingFlags.NonPublic | BindingFlags.Static);
        if (field?.GetValue(null) is not IDictionary stacks || stacks.Count == 0)
            return null;

        var original = stacks.Values.Cast<object>().Select(Convert.ToInt32).ToArray();
        var multiplier = Convert.ToInt32(_utilEntries["StackAmount"].BoxedValue, CultureInfo.InvariantCulture);
        var effectiveMultiplier = multiplier > 1 ? multiplier : 1;
        var min = (long)original.Min() * effectiveMultiplier;
        var max = (long)original.Max() * effectiveMultiplier;
        return $"Stackable catalog: {stacks.Count} items; effective stack range {min:N0} to {max:N0}";
    }

    private static Type FindGameType(string name) =>
        AppDomain.CurrentDomain.GetAssemblies()
                 .Select(assembly => assembly.GetType(name, false))
                 .FirstOrDefault(type => type != null);

    private static object ReadMember(object target, string name)
    {
        if (target == null)
            return null;

        var type = target.GetType();
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
            return property.GetValue(target);

        return type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target);
    }

    private string Value(string key) => Format(_utilEntries[key].BoxedValue);

    private string Duration(string key)
    {
        var value = Convert.ToSingle(_utilEntries[key].BoxedValue, CultureInfo.InvariantCulture);
        return value > 0f ? Format(value) : "game default";
    }

    private string OnOff(string key) => Convert.ToBoolean(_utilEntries[key].BoxedValue) ? "ON" : "OFF";

    private static string Format(object value)
    {
        if (value is float number)
            return number.ToString("0.##", CultureInfo.InvariantCulture);
        if (value is double doubleNumber)
            return doubleNumber.ToString("0.##", CultureInfo.InvariantCulture);
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static AcceptableValueRange<int> IntRange(int min, int max) => new(min, max);

    private static AcceptableValueRange<float> FloatRange(float min, float max) => new(min, max);

    private static SettingDoc Number(
        string key,
        string displayName,
        string description,
        AcceptableValueBase range = null) =>
        new(key, displayName, description, range);

    private static SettingDoc Text(string key, string displayName, string description) =>
        new(key, displayName, description, null);

    private static SettingDoc Toggle(string key, string displayName, string description) =>
        new(key, $"{displayName} (checkbox)", description, null);

    private sealed class SettingDoc
    {
        public SettingDoc(
            string key,
            string displayName,
            string description,
            AcceptableValueBase range)
        {
            Key = key;
            DisplayName = displayName;
            Description = description;
            Range = range;
        }

        public string Key { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public AcceptableValueBase Range { get; }
    }

    public enum UtilityPreset
    {
        Custom,
        Vanilla,
        RelaxedSurvival,
        Builder,
        Explorer
    }
}
