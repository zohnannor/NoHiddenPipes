using System;
using System.Linq;
using System.Security.Permissions;
using BepInEx;
using Menu.Remix.MixedUI;
using MoreSlugcats;
using RWCustom;
using UnityEngine;

#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace NoHiddenPipes;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class NoHiddenPipesMain : BaseUnityPlugin {
    public const string PLUGIN_GUID = "zohnannor.nohiddenpipes";
    public const string PLUGIN_NAME = "No Hidden Pipes";
    public const string PLUGIN_VERSION = "1.2.0";

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
        if (
            self.room?.roomSettings == null
                || self.room.shortcuts == null
                || self.room?.game?.IsArenaSession == true
                || self.room?.world?.singleRoomWorld == true
        ) {
            orig(self);
            return;
        }

        // go through all placed objects and find `ExitSymbolHidden`
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

            // if we found an `ExitSymbolHidden` placed over a real pipe,
            // change the active state based on `ShowHiddenPipes`. if it's a
            // dead-end, use `ShowDeadEnds` for it.
            pObj.active = isRealPipe
               ? !Options.ShowHiddenPipes.Value
               : !Options.ShowDeadEnds.Value;
        }

        orig(self);

        // after the game's logic has run (and we want to unhide broken
        // shelters), do the same thing the game does but place the shortcut
        // sprite.
        if (!Options.ShowBrokenShelters.Value || self.room.world == null) {
            return;
        }

        for (int l = 0; l < self.room.shortcuts.Length; l++) {
            var shortcut = self.room.shortcuts[l];
            int node = shortcut.destNode;

            bool isBrokenShelter =
                self.entranceSprites[l, 0] == null
                    && shortcut.shortCutType == ShortcutData.Type.RoomExit
                    && node >= 0
                    && node < self.room.abstractRoom.connections.Length
                    && self.room.abstractRoom.connections[node] is int targetId
                    && targetId >= 0
                    && self.room.world.GetAbstractRoom(targetId) is AbstractRoom targetRoom
                    && targetRoom.shelter
                    && targetRoom.shelterIndex >= 0
                    && targetRoom.shelterIndex < self.room.world.brokenShelters.Length
                    && self.room.world.brokenShelters[targetRoom.shelterIndex];

            if (!isBrokenShelter) {
                continue;
            }

            self.entranceSprites[l, 0] = new FSprite("ShortcutDots") {
                rotation = Custom.AimFromOneVectorToAnother(new Vector2(0f, 0f), -IntVector2.ToVector2(self.room.ShorcutEntranceHoleDirection(shortcut.StartTile)))
            };
            self.entranceSpriteLocations[l] = self.room.MiddleOfTile(shortcut.StartTile) + IntVector2.ToVector2(self.room.ShorcutEntranceHoleDirection(shortcut.StartTile)) * 15f;
            if ((ModManager.MMF && MMF.cfgShowUnderwaterShortcuts.Value) || (self.room.water && self.room.waterInFrontOfTerrain && self.room.PointSubmerged(self.entranceSpriteLocations[l] + new Vector2(0f, 5f)))) {
                string waterContainerName = (ModManager.MMF && MMF.cfgShowUnderwaterShortcuts.Value) ? "GrabShaders" : "Items";
                self.camera.ReturnFContainer(waterContainerName).AddChild(self.entranceSprites[l, 0]);
            } else {
                self.camera.ReturnFContainer("Shortcuts").AddChild(self.entranceSprites[l, 0]);
                self.camera.ReturnFContainer("Water").AddChild(self.entranceSprites[l, 1]);
            }
        }
    }
}

public class NoHiddenPipesOptions : OptionInterface {
    public readonly Configurable<bool> ShowHiddenPipes;
    public readonly Configurable<bool> ShowDeadEnds;
    public readonly Configurable<bool> ShowBrokenShelters;

    private OpTab mainTab;
    private OpCheckBox _hiddenPipesCheckbox;
    private OpCheckBox _deadEndPipesCheckbox;
    private OpCheckBox _brokenSheltersCheckbox;

    private const string descShared = "Disable this to toggle the mod's functionality without restarting the game.";
    private const string descNormal = $"Show hidden pipes. {descShared}";
    private const string descDeadEnds = $"Show hidden dead-ends. There are a couple of hidden pipes in the game that don't lead anywhere and serve no purpose. {descShared}";
    private const string descBrokenShelters = $"Show broken shelters. {descShared}";

    public NoHiddenPipesOptions() {
        ShowHiddenPipes = config.Bind("enabled", true);
        ShowDeadEnds = config.Bind("show_dead_ends", false);
        ShowBrokenShelters = config.Bind("show_broken_shelters", true);
    }

    public override void Initialize() {
        base.Initialize();

        mainTab = new OpTab(this, "Main");
        Tabs = [mainTab];

        _hiddenPipesCheckbox = new OpCheckBox(ShowHiddenPipes, 5f, 527f) {
            description = descNormal
        };
        _deadEndPipesCheckbox = new OpCheckBox(ShowDeadEnds, 5f, 487f) {
            description = descDeadEnds
        };
        _brokenSheltersCheckbox = new OpCheckBox(ShowBrokenShelters, 5f, 447f) {
            description = descBrokenShelters
        };

        mainTab.AddItems([
            _hiddenPipesCheckbox,
            new OpLabel(37f, 530f, "Show hidden pipes") {
                alignment = FLabelAlignment.Left,
                description = descNormal
            },
            _deadEndPipesCheckbox,
            new OpLabel(37f, 490f, "Show hidden dead-ends") {
                alignment = FLabelAlignment.Left,
                description = descDeadEnds
            },
            _brokenSheltersCheckbox,
            new OpLabel(37f, 450f, "Show broken shelters") {
                alignment = FLabelAlignment.Left,
                description = descBrokenShelters
            }
        ]);
    }
}
