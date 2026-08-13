using System.Security.Permissions;
using BepInEx;
using Menu.Remix.MixedUI;
using UnityEngine;

#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace NoHiddenPipes;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class NoHiddenPipesMain : BaseUnityPlugin {
    public const string PLUGIN_GUID = "zohnannor.nohiddenpipes";
    public const string PLUGIN_NAME = "No Hidden Pipes";
    public const string PLUGIN_VERSION = "1.0.0";

    private bool initDone = false;
    public static NoHiddenPipesOptions Options;

    public void OnEnable() {
        On.RainWorld.OnModsInit += OnModsInit;
    }

    public void OnDisable() {
        On.RainWorld.OnModsInit -= OnModsInit;
    }

    private void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self) {
        orig(self);
        if (initDone) {
            return;
        }

        Options = new NoHiddenPipesOptions();
        MachineConnector.SetRegisteredOI(PLUGIN_GUID, Options);

        On.PlacedObject.FromString += PlacedObject_FromString;

        Logger.LogDebug($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        initDone = true;
    }

    public void PlacedObject_FromString(On.PlacedObject.orig_FromString orig, PlacedObject self, string[] s) {
        orig(self, s);
        if (self.type == PlacedObject.Type.ExitSymbolHidden) {
            self.active = !Options.Enabled.Value;
        }
    }
}

public class NoHiddenPipesOptions : OptionInterface {
    public readonly Configurable<bool> Enabled;
    private OpTab mainTab;
    private OpCheckBox _enabledCheckbox;

    private const string description = "Show hidden pipes. Disable this to toggle the mod's functionality without restarting the game.";

    public NoHiddenPipesOptions() {
        Enabled = config.Bind("enabled", true);
    }

    public override void Initialize() {
        base.Initialize();

        mainTab = new OpTab(this, "Main");
        Tabs = [mainTab];
        _enabledCheckbox = new OpCheckBox(Enabled, 5f, 527f) { description = description };

        mainTab.AddItems([
            _enabledCheckbox,
            new OpLabel(
                37f,
                530f,
                "Show hidden pipes"
            ) {
                alignment = FLabelAlignment.Left,
                description = description
            }
        ]);
    }
}
