using System;
using System.Linq;
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
    public const string PLUGIN_VERSION = "1.1.0";

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

        On.ShortcutGraphics.GenerateSprites += ShortcutGraphics_GenerateSprites;

        Logger.LogDebug($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        initDone = true;
    }

    private void ShortcutGraphics_GenerateSprites(On.ShortcutGraphics.orig_GenerateSprites orig, ShortcutGraphics self) {
        if (self.room?.roomSettings == null || self.room.shortcuts == null) {
            orig(self);
            return;
        }

        foreach (var pObj in self.room.roomSettings.placedObjects) {
            if (pObj.type != PlacedObject.Type.ExitSymbolHidden) {
                continue;
            }

            var tile = self.room.GetTilePosition(pObj.pos);
            int index = Array.FindIndex(self.room.shortcuts, s => s.StartTile == tile);
            if (index == -1) {
                pObj.active = !Options.ShowDeadEnds.Value;
                continue;
            }

            var shortcut = self.room.shortcuts[index];
            bool isRealPipe =
                shortcut.shortCutType == ShortcutData.Type.Normal ||
                (shortcut.shortCutType == ShortcutData.Type.RoomExit
                    && self.room.abstractRoom.connections[shortcut.destNode] >= 0);

            pObj.active = isRealPipe
               ? !Options.Enabled.Value
               : !Options.ShowDeadEnds.Value;

        }

        orig(self);
    }
}

public class NoHiddenPipesOptions : OptionInterface {
    public readonly Configurable<bool> Enabled;
    public readonly Configurable<bool> ShowDeadEnds;

    private OpTab mainTab;
    private OpCheckBox _enabledCheckbox;
    private OpCheckBox _deadEndPipesCheckbox;

    private const string descNormal = "Show hidden pipes. Disable this to toggle the mod's functionality without restarting the game.";
    private const string descDeadEnds = "Show hidden dead-ends. There are a couple of hidden pipes in the game that don't lead anywhere and serve no purpose. Disable this to toggle the mod's functionality without restarting the game.";

    public NoHiddenPipesOptions() {
        Enabled = config.Bind("enabled", true);
        ShowDeadEnds = config.Bind("show_dead_ends", false);
    }

    public override void Initialize() {
        base.Initialize();

        mainTab = new OpTab(this, "Main");
        Tabs = [mainTab];

        _enabledCheckbox = new OpCheckBox(Enabled, 5f, 527f) { description = descNormal };
        _deadEndPipesCheckbox = new OpCheckBox(ShowDeadEnds, 5f, 487f) { description = descDeadEnds };

        mainTab.AddItems([
            _enabledCheckbox,
            new OpLabel(37f, 530f, "Show hidden pipes") {
                alignment = FLabelAlignment.Left,
                description = descNormal
            },
            _deadEndPipesCheckbox,
            new OpLabel(37f, 490f, "Show hidden dead-ends") {
                alignment = FLabelAlignment.Left,
                description = descDeadEnds
            }
        ]);
    }
}
