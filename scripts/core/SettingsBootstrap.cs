using Godot;

namespace EndfieldZero.Core;

public partial class SettingsBootstrap : Node
{
    public static PlayerPreferences RuntimeDefaults { get; private set; }
    public static PlayerPreferences ActivePreferences { get; private set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        RuntimeDefaults ??= UserSettingsStore.CaptureRuntimeDefaults();
        ActivePreferences ??= UserSettingsStore.Load(RuntimeDefaults);
        UserSettingsStore.Apply(ActivePreferences);
    }

    public static void UpdateActivePreferences(PlayerPreferences preferences)
    {
        ActivePreferences = preferences?.Clone();
    }
}
