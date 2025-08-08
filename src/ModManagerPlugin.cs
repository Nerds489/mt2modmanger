// Based on your provided draft. Names and GUID updated for a clean drop-in.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using HarmonyLib;

namespace MT2ModManager.src
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class ModManagerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "OTM.MT2ModManager";
        public const string PluginName = "Monster Train 2 Mod Manager";
        public const string PluginVersion = "1.0.0";

        private bool showModMenu;
        private bool showSettingsMenu;
        private Rect windowRect = new(50, 50, 520, 440);
        private Rect settingsRect = new(600, 50, 460, 360);
        private Vector2 scrollPosition = Vector2.zero;
        private Vector2 settingsScrollPosition = Vector2.zero;

        private ConfigEntry<KeyCode> toggleKey;
        private ConfigEntry<bool> showOnStartup;
        private static readonly List<ModInfo> modInfos = [];
        private List<ModInfo> cachedMods = modInfos;
        private ModInfo selectedMod = null;
        private static readonly Dictionary<string, bool> dictionary1 = [];
        private static readonly Dictionary<string, bool> dictionary = dictionary1;
        private readonly Dictionary<string, bool> modStates = dictionary;

        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle buttonStyle;

        private Harmony _harmony;

        private void Awake()
        {
            toggleKey = Config.Bind("General", "ToggleKey", KeyCode.F1, "Key to toggle mod manager menu");
            showOnStartup = Config.Bind("General", "ShowOnStartup", false, "Show mod manager when the game starts");

            Logger.LogInfo($"{PluginName} loaded");
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            RefreshModList();
        }

        private void Start()
        {
            if (showOnStartup.Value) showModMenu = true;
        }

        private void OnDestroy()
        {
            try { _harmony?.UnpatchSelf(); } catch { /* ignore */ }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey.Value))
            {
                showModMenu = !showModMenu;
                if (showModMenu) RefreshModList();
            }
        }

        private void OnGUI()
        {
            InitializeStyles();
            if (showModMenu)
                windowRect = GUI.Window(12345, windowRect, ModManagerWindow, PluginName, boxStyle);

            if (showSettingsMenu && selectedMod != null)
                settingsRect = GUI.Window(12346, settingsRect, ModSettingsWindow, $"Settings - {selectedMod.Name}", boxStyle);
        }

        private void InitializeStyles()
        {
            if (headerStyle != null) return;
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            boxStyle = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(10, 10, 20, 10)
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                margin = new RectOffset(2, 2, 2, 2)
            };
        }

        private void ModManagerWindow(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Mod Manager", headerStyle);
            GUILayout.Space(10);

            int enabledCount = cachedMods.Count(m => m.IsEnabled);
            GUILayout.Label($"Mods: {cachedMods.Count} total, {enabledCount} enabled", GUI.skin.box);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", buttonStyle)) RefreshModList();
            if (GUILayout.Button("Enable All", buttonStyle)) ToggleAllMods(true);
            if (GUILayout.Button("Disable All", buttonStyle)) ToggleAllMods(false);
            if (GUILayout.Button("Open Mods Folder", buttonStyle)) OpenModsFolder();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Installed Mods:", GUI.skin.label);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(240));
            foreach (var mod in cachedMods)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);

                bool newEnabled = GUILayout.Toggle(mod.IsEnabled, "", GUILayout.Width(20));
                if (newEnabled != mod.IsEnabled) ToggleMod(mod, newEnabled);

                GUILayout.BeginVertical();
                GUILayout.Label(mod.Name ?? "(Unnamed)", GUI.skin.label);
                GUILayout.Label($"v{mod.Version} by {mod.Author}", GUI.skin.box);
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                GUILayout.BeginVertical(GUILayout.Width(100));
                if (GUILayout.Button("Settings", buttonStyle)) OpenModSettings(mod);
                if (GUILayout.Button("Info", buttonStyle)) ShowModInfo(mod);
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Export Mod List", buttonStyle)) ExportModList();
            if (GUILayout.Button("Manager Settings", buttonStyle)) OpenManagerSettings();
            if (GUILayout.Button("Close", buttonStyle)) showModMenu = false;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void ModSettingsWindow(int windowID)
        {
            GUILayout.BeginVertical();

            if (selectedMod?.Plugin != null)
            {
                GUILayout.Label($"Configuration for {selectedMod.Name}", headerStyle);
                GUILayout.Space(10);

                settingsScrollPosition = GUILayout.BeginScrollView(settingsScrollPosition);

                try
                {
                    var cfg = selectedMod.Plugin.Config;
                    var entriesField = typeof(ConfigFile).GetField("_entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (entriesField != null)
                    {
                        if (entriesField.GetValue(cfg) is System.Collections.IDictionary dict)
                        {
                            foreach (System.Collections.DictionaryEntry de in dict)
                            {
                                var def = (ConfigDefinition)de.Key;
                                var entry = (ConfigEntryBase)de.Value;

                                GUILayout.BeginHorizontal(GUI.skin.box);

                                GUILayout.BeginVertical(GUILayout.Width(260));
                                GUILayout.Label($"{def.Section} :: {def.Key}", GUI.skin.label);
                                var desc = entry.Description.Description;
                                if (!string.IsNullOrEmpty(desc)) GUILayout.Label(desc, GUI.skin.box);
                                GUILayout.EndVertical();

                                HandleConfigValue(entry);

                                GUILayout.EndHorizontal();
                            }
                        }
                        else
                        {
                            GUILayout.Label("Config entries not accessible for this mod.");
                        }
                    }
                    else
                    {
                        GUILayout.Label("Config entries not accessible for this mod.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Config enumeration failed: {ex.Message}");
                    GUILayout.Label("Unable to enumerate config for this mod.");
                }

                GUILayout.EndScrollView();
            }
            else
            {
                GUILayout.Label("No configuration available for this mod.", GUI.skin.label);
            }

            if (GUILayout.Button("Close", buttonStyle))
            {
                showSettingsMenu = false;
                selectedMod = null;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void HandleConfigValue(ConfigEntryBase configEntry)
        {
            try
            {
                object boxed = configEntry.BoxedValue;
                GUILayout.BeginVertical(GUILayout.Width(160));

                if (boxed is bool b)
                {
                    bool nv = GUILayout.Toggle(b, "", GUILayout.Width(60));
                    if (nv != b) configEntry.BoxedValue = nv;
                }
                else if (boxed is int i)
                {
                    int nv = (int)GUILayout.HorizontalSlider(i, 0, 100, GUILayout.Width(120));
                    GUILayout.Label(nv.ToString(), GUILayout.Width(40));
                    if (nv != i) configEntry.BoxedValue = nv;
                }
                else if (boxed is float f)
                {
                    float nv = GUILayout.HorizontalSlider(f, 0f, 10f, GUILayout.Width(120));
                    GUILayout.Label(nv.ToString("F2"), GUILayout.Width(50));
                    if (Math.Abs(nv - f) > 0.001f) configEntry.BoxedValue = nv;
                }
                else
                {
                    GUILayout.Label(boxed?.ToString() ?? "null", GUILayout.Width(140));
                }

                GUILayout.EndVertical();
            }
            catch (Exception ex)
            {
                GUILayout.Label("Error", GUILayout.Width(50));
                Logger.LogError($"Error handling config value: {ex.Message}");
            }
        }

        private void RefreshModList()
        {
            cachedMods.Clear();
            try
            {
                foreach (var kv in BepInEx.Bootstrap.Chainloader.PluginInfos)
                {
                    var pluginInfo = kv.Value;
                    var inst = pluginInfo.Instance;

                    var modInfo = new ModInfo
                    {
                        Name = pluginInfo.Metadata.Name,
                        Version = pluginInfo.Metadata.Version?.ToString() ?? "unknown",
                        Author = ExtractAuthor(pluginInfo.Metadata.GUID),
                        GUID = pluginInfo.Metadata.GUID,
                        IsEnabled = inst != null && inst.enabled,
                        Plugin = inst,
                        Description = pluginInfo.Metadata.Name
                    };
                    cachedMods.Add(modInfo);
                }
                cachedMods = [.. cachedMods.OrderBy(m => m.Name)];
                Logger.LogInfo($"Found {cachedMods.Count} mods");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error refreshing mod list: {ex.Message}");
            }
        }

        private static string ExtractAuthor(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return "Unknown";
            var parts = guid.Split('.');
            return parts.Length > 0 ? parts[0] : "Unknown";
        }

        private void ToggleMod(ModInfo mod, bool enabled)
        {
            try
            {
                if (mod.Plugin != null)
                {
                    mod.Plugin.enabled = enabled;
                    mod.IsEnabled = enabled;
                    modStates[mod.GUID] = enabled;
                    Logger.LogInfo($"Mod {mod.Name} {(enabled ? "enabled" : "disabled")}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error toggling mod {mod.Name}: {ex.Message}");
            }
        }

        private void ToggleAllMods(bool enabled)
        {
            foreach (var mod in cachedMods)
            {
                if (!string.Equals(mod.GUID, PluginGuid, StringComparison.OrdinalIgnoreCase))
                    ToggleMod(mod, enabled);
            }
        }

        private void OpenModSettings(ModInfo mod)
        {
            selectedMod = mod;
            showSettingsMenu = true;
        }

        private void ShowModInfo(ModInfo mod)
        {
            Logger.LogInfo($"[Mod Info] {mod.Name} v{mod.Version} by {mod.Author} | GUID: {mod.GUID}");
        }

        private void OpenModsFolder()
        {
            try
            {
                string pluginsPath = Path.Combine(Paths.BepInExRootPath, "plugins");
                if (Application.platform == RuntimePlatform.WindowsPlayer)
                    System.Diagnostics.Process.Start("explorer.exe", pluginsPath);
                else if (Application.platform == RuntimePlatform.OSXPlayer)
                    System.Diagnostics.Process.Start("open", pluginsPath);
                else
                    System.Diagnostics.Process.Start("xdg-open", pluginsPath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error opening mods folder: {ex.Message}");
            }
        }

        private void OpenManagerSettings()
        {
            selectedMod = cachedMods.FirstOrDefault(m => string.Equals(m.GUID, PluginGuid, StringComparison.OrdinalIgnoreCase));
            if (selectedMod != null) showSettingsMenu = true;
        }

        private void ExportModList()
        {
            try
            {
                var exportPath = Path.Combine(Paths.BepInExRootPath, "mod_list_export.txt");
                var lines = new List<string>
                {
                    "Monster Train 2 Mod List Export",
                    $"Generated on: {DateTime.Now}",
                    $"Total Mods: {cachedMods.Count}",
                    string.Empty
                };
                foreach (var mod in cachedMods)
                    lines.Add($"{(mod.IsEnabled ? "[ENABLED]" : "[DISABLED]")} {mod.Name} v{mod.Version} by {mod.Author}");

                File.WriteAllLines(exportPath, lines);
                Logger.LogInfo($"Mod list exported to: {exportPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error exporting mod list: {ex.Message}");
            }
        }
    }

    public class ModInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string GUID { get; set; }
        public string Description { get; set; }
        public bool IsEnabled { get; set; }
        public BaseUnityPlugin Plugin { get; set; }
    }
}
