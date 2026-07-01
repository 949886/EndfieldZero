using Godot;
using Cherry.UI;
using Cherry.World;

namespace Cherry.Core;

public static class UserSettingsStore
{
    private const string SettingsPath = "user://settings.cfg";
    private const string GraphicsSection = "graphics";
    private const string AudioSection = "audio";
    private const string GameSection = "game";

    public static PlayerPreferences CaptureRuntimeDefaults()
    {
        var preferences = new PlayerPreferences();
        var window = GetRootWindow();
        if (window != null)
        {
            preferences.DisplayMode = MapDisplayMode(window);
            preferences.WindowSize = window.Size;
        }

        preferences.VSyncEnabled = DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled;
        preferences.FpsLimit = Engine.MaxFps;
        preferences.DefaultViewMode = SettingsBootstrap.ActivePreferences?.DefaultViewMode
            ?? GameCamera.Instance?.ViewMode
            ?? CameraViewMode.Orthographic3D;
        preferences.MasterVolume = ReadBusVolumeLinear("Master");
        preferences.MusicVolume = ReadBusVolumeLinear("Music");
        preferences.SfxVolume = ReadBusVolumeLinear("SFX");
        preferences.ShowDebugHud = DebugHud.Instance?.Visible ?? true;

        var defaults = GameSettings.CreateDefaultSnapshot();
        preferences.TicksPerSecond = defaults.TicksPerSecond;
        preferences.AIEvalInterval = defaults.AIEvalInterval;
        preferences.PawnWanderRadiusBlocks = defaults.PawnWanderRadiusBlocks;
        preferences.EnableSelectionOutline = defaults.EnableSelectionOutline;
        preferences.SelectionOutlineWidth = defaults.SelectionOutlineWidth;
        preferences.SelectionOutlineOffset = defaults.SelectionOutlineOffset;
        preferences.SelectionOutlineColorR = defaults.SelectionOutlineColorR;
        preferences.SelectionOutlineColorG = defaults.SelectionOutlineColorG;
        preferences.SelectionOutlineColorB = defaults.SelectionOutlineColorB;
        preferences.SelectionOutlineColorA = defaults.SelectionOutlineColorA;
        preferences.EnemyBaseMoveSpeedBlocksPerSecond = defaults.EnemyBaseMoveSpeedBlocksPerSecond;
        preferences.EnemyDetectionRangeBlocks = defaults.EnemyDetectionRangeBlocks;
        preferences.EnemyFleeHpPercent = defaults.EnemyFleeHpPercent;
        preferences.HostileDamageMultiplier = defaults.HostileDamageMultiplier;
        preferences.HostileCooldownMultiplier = defaults.HostileCooldownMultiplier;
        preferences.HostileRangeMultiplier = defaults.HostileRangeMultiplier;
        preferences.RaidImmediateAttackChance = defaults.RaidImmediateAttackChance;
        preferences.RaidCountThreatDivisor = defaults.RaidCountThreatDivisor;
        preferences.RaidMaxCount = defaults.RaidMaxCount;
        preferences.RaidStatBonusCap = defaults.RaidStatBonusCap;
        preferences.BowBaseDamage = defaults.BowBaseDamage;
        preferences.BowCooldownTicks = defaults.BowCooldownTicks;
        preferences.CrossbowBaseDamage = defaults.CrossbowBaseDamage;
        preferences.CrossbowCooldownTicks = defaults.CrossbowCooldownTicks;
        preferences.RifleBaseDamage = defaults.RifleBaseDamage;
        preferences.RifleCooldownTicks = defaults.RifleCooldownTicks;
        preferences.DaysPerSeason = defaults.DaysPerSeason;
        preferences.WeatherChangeIntervalHours = defaults.WeatherChangeIntervalHours;
        return preferences;
    }

    public static PlayerPreferences Load(PlayerPreferences defaults)
    {
        var preferences = defaults.Clone();
        var config = new ConfigFile();
        if (config.Load(SettingsPath) != Error.Ok)
            return preferences;

        preferences.DisplayMode = (DisplayModePreference)(int)config.GetValue(GraphicsSection, "display_mode", (int)preferences.DisplayMode);
        preferences.WindowSize = new Vector2I(
            (int)config.GetValue(GraphicsSection, "window_width", preferences.WindowSize.X),
            (int)config.GetValue(GraphicsSection, "window_height", preferences.WindowSize.Y));
        preferences.VSyncEnabled = (bool)config.GetValue(GraphicsSection, "vsync", preferences.VSyncEnabled);
        preferences.FpsLimit = (int)config.GetValue(GraphicsSection, "fps_limit", preferences.FpsLimit);
        preferences.DefaultViewMode = (CameraViewMode)(int)config.GetValue(GraphicsSection, "default_view_mode", (int)preferences.DefaultViewMode);

        preferences.MasterVolume = (float)(double)config.GetValue(AudioSection, "master_volume", preferences.MasterVolume);
        preferences.MusicVolume = (float)(double)config.GetValue(AudioSection, "music_volume", preferences.MusicVolume);
        preferences.SfxVolume = (float)(double)config.GetValue(AudioSection, "sfx_volume", preferences.SfxVolume);
        preferences.AudioInBackground = (bool)config.GetValue(AudioSection, "audio_in_background", preferences.AudioInBackground);

        preferences.ShowTutorial = (bool)config.GetValue(GameSection, "show_tutorial", preferences.ShowTutorial);
        preferences.ShowDebugHud = (bool)config.GetValue(GameSection, "show_debug_hud", preferences.ShowDebugHud);
        preferences.ShowAdvancedSettings = (bool)config.GetValue(GameSection, "show_advanced_settings", preferences.ShowAdvancedSettings);
        preferences.EnableSelectionOutline = (bool)config.GetValue("advanced.selection", "EnableSelectionOutline", preferences.EnableSelectionOutline);

        foreach (var spec in SettingsFieldRegistry.AdvancedFields)
        {
            string section = $"advanced.{spec.SectionSuffix}";
            Variant rawValue = config.GetValue(section, spec.PreferencePropertyName, Variant.CreateFrom(spec.GetPreferenceValue(preferences)));
            spec.SetPreferenceValue(preferences, (double)rawValue);
        }

        return preferences;
    }

    public static Error Save(PlayerPreferences preferences)
    {
        var config = new ConfigFile();
        config.SetValue(GraphicsSection, "display_mode", (int)preferences.DisplayMode);
        config.SetValue(GraphicsSection, "window_width", preferences.WindowSize.X);
        config.SetValue(GraphicsSection, "window_height", preferences.WindowSize.Y);
        config.SetValue(GraphicsSection, "vsync", preferences.VSyncEnabled);
        config.SetValue(GraphicsSection, "fps_limit", preferences.FpsLimit);
        config.SetValue(GraphicsSection, "default_view_mode", (int)preferences.DefaultViewMode);

        config.SetValue(AudioSection, "master_volume", preferences.MasterVolume);
        config.SetValue(AudioSection, "music_volume", preferences.MusicVolume);
        config.SetValue(AudioSection, "sfx_volume", preferences.SfxVolume);
        config.SetValue(AudioSection, "audio_in_background", preferences.AudioInBackground);

        config.SetValue(GameSection, "show_tutorial", preferences.ShowTutorial);
        config.SetValue(GameSection, "show_debug_hud", preferences.ShowDebugHud);
        config.SetValue(GameSection, "show_advanced_settings", preferences.ShowAdvancedSettings);
        config.SetValue("advanced.selection", "EnableSelectionOutline", preferences.EnableSelectionOutline);

        foreach (var spec in SettingsFieldRegistry.AdvancedFields)
        {
            string section = $"advanced.{spec.SectionSuffix}";
            config.SetValue(section, spec.PreferencePropertyName, spec.GetPreferenceValue(preferences));
        }

        return config.Save(SettingsPath);
    }

    public static void Apply(PlayerPreferences preferences)
    {
        ApplyGraphics(preferences);
        ApplyAudio(preferences);
        ApplyGameplay(preferences);
        GameSettings.ApplyUserOverrides(preferences);
    }

    private static void ApplyGraphics(PlayerPreferences preferences)
    {
        var window = GetRootWindow();
        if (window != null)
        {
            switch (preferences.DisplayMode)
            {
                case DisplayModePreference.Windowed:
                    window.Mode = Window.ModeEnum.Windowed;
                    window.Borderless = false;
                    window.Size = preferences.WindowSize;
                    break;
                case DisplayModePreference.Fullscreen:
                    window.Borderless = false;
                    window.Mode = Window.ModeEnum.Fullscreen;
                    break;
                case DisplayModePreference.BorderlessFullscreen:
                    window.Mode = Window.ModeEnum.Fullscreen;
                    window.Borderless = true;
                    break;
            }
        }

        DisplayServer.WindowSetVsyncMode(preferences.VSyncEnabled
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = Mathf.Max(preferences.FpsLimit, 0);
        GameCamera.Instance?.SetViewMode(preferences.DefaultViewMode);
    }

    private static void ApplyAudio(PlayerPreferences preferences)
    {
        WriteBusVolumeLinear("Master", preferences.MasterVolume);
        WriteBusVolumeLinear("Music", preferences.MusicVolume);
        WriteBusVolumeLinear("SFX", preferences.SfxVolume);
    }

    private static void ApplyGameplay(PlayerPreferences preferences)
    {
        if (DebugHud.Instance != null)
            DebugHud.Instance.Visible = preferences.ShowDebugHud;
    }

    private static Window GetRootWindow()
    {
        return (Engine.GetMainLoop() as SceneTree)?.Root;
    }

    private static DisplayModePreference MapDisplayMode(Window window)
    {
        if (window.Mode == Window.ModeEnum.Fullscreen)
            return window.Borderless ? DisplayModePreference.BorderlessFullscreen : DisplayModePreference.Fullscreen;

        return DisplayModePreference.Windowed;
    }

    private static float ReadBusVolumeLinear(string busName)
    {
        int index = AudioServer.GetBusIndex(busName);
        if (index < 0)
            return 1f;

        return Mathf.DbToLinear(AudioServer.GetBusVolumeDb(index));
    }

    private static void WriteBusVolumeLinear(string busName, float value)
    {
        int index = AudioServer.GetBusIndex(busName);
        if (index < 0)
            return;

        float clamped = Mathf.Clamp(value, 0f, 1f);
        AudioServer.SetBusVolumeDb(index, clamped <= 0.0001f ? -80f : Mathf.LinearToDb(clamped));
    }
}
