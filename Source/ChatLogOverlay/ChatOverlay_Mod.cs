using HarmonyLib;
using RimWorld;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using System;

public class ChatOverlayMod : Mod
{
    public static ChatOverlaySettings Settings;

    private Vector2 _scrollMods;
    private Vector2 _scrollDefs;
    private string _searchMods = "";
    private string _searchDefs = "";
    
    private enum SettingsTab { General, Filters, Advanced }
    private SettingsTab currentTab = SettingsTab.General;

    private static readonly (SettingsTab Tab, string Label)[] TabData = {
        (SettingsTab.General, "General"),
        (SettingsTab.Filters, "Filters"),
        (SettingsTab.Advanced, "Advanced")
    };

    public ChatOverlayMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<ChatOverlaySettings>();
        var h = new Harmony("kirimin.ChatLogOverlay");
        h.PatchAll();
    }

    public override string SettingsCategory() => "Chatlog Overlay";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        DrawTabButtons(listing);
        listing.Gap();

        switch (currentTab)
        {
            case SettingsTab.General:
                DrawGeneralTab(listing, inRect);
                break;
            case SettingsTab.Filters:
                DrawFiltersTab(listing, inRect);
                break;
            case SettingsTab.Advanced:
                DrawAdvancedTab(listing, inRect);
                break;
        }

        listing.End();
    }

    private void DrawTabButtons(Listing_Standard listing)
    {
        var tabRect = listing.GetRect(30f);
        float tabWidth = tabRect.width / TabData.Length;
        Color originalColor = GUI.color;

        for (int i = 0; i < TabData.Length; i++)
        {
            var (tab, label) = TabData[i];
            var rect = new Rect(tabRect.x + i * tabWidth, tabRect.y, 
                               i < TabData.Length - 1 ? tabWidth - 2f : tabWidth, 30f);
            
            bool isActive = currentTab == tab;
            
            if (!isActive)
                GUI.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            
            if (Widgets.ButtonText(rect, label))
                currentTab = tab;
            
            GUI.color = originalColor;

            if (isActive)
            {
                var underlineRect = new Rect(rect.x, rect.yMax - 2f, rect.width, 2f);
                Widgets.DrawBoxSolid(underlineRect, Color.white);
            }
        }
    }

    private void DrawGeneralTab(Listing_Standard listing, Rect inRect)
    {
        bool isVisible = ChatOverlayRenderer.IsVisible;
        string btnLabel = isVisible ? "Hide Overlay" : "Show Overlay";
        if (Widgets.ButtonText(listing.GetRect(30f), btnLabel))
        {
            ChatOverlayRenderer.IsVisible = !isVisible;
        }

        if (Widgets.ButtonText(listing.GetRect(30f), "Clear overlay logs"))
        {
            ChatState.Clear();
        }
        listing.Gap();

        DrawOpacitySlider(listing);
        DrawUsageInstructions(listing);
    }

    private void DrawOpacitySlider(Listing_Standard listing)
    {
        listing.Label($"Background Opacity: {Settings.BackgroundOpacity:F2} (0.0 = Transparent, 1.0 = Opaque)");
        float newOpacity = listing.Slider(Settings.BackgroundOpacity, 0.0f, 1.0f);
        if (Math.Abs(newOpacity - Settings.BackgroundOpacity) > 0.001f)
        {
            Settings.BackgroundOpacity = newOpacity;
            Settings.Write();
        }
        listing.Gap();
    }

    private void DrawUsageInstructions(Listing_Standard listing)
    {
        var instructions = new[]
        {
            "How to use:",
            "• Drag the title bar (top edge) to move the overlay",
            "• Drag the bottom-right corner to resize",
            "• Use the Filters tab to control which logs appear",
            "• The overlay appears behind game UI elements"
        };

        foreach (var instruction in instructions)
        {
            listing.Label(instruction);
        }
    }

    private void DrawFiltersTab(Listing_Standard listing, Rect inRect)
    {
        DrawFilterModeSelection(listing);
        listing.GapLine();

        if (Settings.Mode != ChatOverlayFilterMode.Off)
        {
            DrawModControlButtons(listing);
            listing.Gap();
            DrawModsSection(listing, inRect);
        }
        else
        {
            listing.Label("Filter is currently disabled. All interaction logs will be shown.");
        }
    }

    private void DrawFilterModeSelection(Listing_Standard listing)
    {
        listing.Label("Filter mode");
        
        var modes = new[]
        {
            (ChatOverlayFilterMode.Off, "Off (show all)"),
            (ChatOverlayFilterMode.Whitelist, "Whitelist (only selected)")
        };

        foreach (var (mode, label) in modes)
        {
            if (listing.RadioButton(label, Settings.Mode == mode) && Settings.Mode != mode)
            {
                Settings.Mode = mode;
                Settings.Write();
            }
        }
    }

    private void DrawModControlButtons(Listing_Standard listing)
    {
        var buttonRect = listing.GetRect(30f);
        float buttonWidth = (buttonRect.width - 9f) / 2f;

        // 型を明示的に指定
        var buttons = new (string Label, Action Action)[]
        {
            ("Select All Mods", () => {
                foreach (var m in LoadedModManager.RunningModsListForReading)
                    Settings.PackageIdSet.Add(m.PackageId);
                Settings.Write();
            }),
            ("Clear All Mods", () => {
                Settings.PackageIdSet.Clear();
                Settings.Write();
            })
        };

        for (int i = 0; i < buttons.Length; i++)
        {
            var (label, action) = buttons[i];
            var rect = new Rect(buttonRect.x + i * (buttonWidth + 9f), buttonRect.y, buttonWidth, 30f);
            if (Widgets.ButtonText(rect, label))
                action();
        }
    }

    private void DrawAdvancedTab(Listing_Standard listing, Rect inRect)
    {
        listing.Label("Advanced Settings");
        listing.Gap();

        if (Settings.Mode == ChatOverlayFilterMode.Whitelist)
        {
            DrawInteractionControlButtons(listing);
            listing.Gap();
            DrawInteractionDefsSection(listing, inRect);
        }
        else
        {
            listing.Label("Advanced settings are only available when using Whitelist mode.");
            listing.Label("Switch to Whitelist mode in the Filters tab to access these options.");
        }
    }

    private void DrawInteractionControlButtons(Listing_Standard listing)
    {
        var buttonRect = listing.GetRect(30f);
        float buttonWidth = (buttonRect.width - 9f) / 2f;

        // 型を明示的に指定
        var buttons = new (string Label, Action Action)[]
        {
            ("Select All Interactions", () => {
                foreach (var d in DefDatabase<InteractionDef>.AllDefsListForReading)
                    if (!string.IsNullOrEmpty(d?.defName))
                        Settings.DefNameSet.Add(d.defName);
                Settings.Write();
            }),
            ("Clear All Interactions", () => {
                Settings.DefNameSet.Clear();
                Settings.Write();
            })
        };

        for (int i = 0; i < buttons.Length; i++)
        {
            var (label, action) = buttons[i];
            var rect = new Rect(buttonRect.x + i * (buttonWidth + 9f), buttonRect.y, buttonWidth, 30f);
            if (Widgets.ButtonText(rect, label))
                action();
        }
    }

    private void DrawInteractionDefsSection(Listing_Standard listing, Rect inRect)
    {
        listing.Label("Interaction types (InteractionDef.defName)");
        listing.Label("Note: These settings are for fine-tuning specific interaction types.");
        
        var searchRect = listing.GetRect(24f);
        _searchDefs = Widgets.TextField(searchRect, _searchDefs ?? "");
        
        var defs = DefDatabase<InteractionDef>.AllDefsListForReading
            .OrderBy(d => d.modContentPack?.Name ?? "Core")
            .ThenBy(d => d.label ?? d.defName)
            .ToList();

        DrawScrollableDefList(listing, inRect, defs, _searchDefs, ref _scrollDefs, Settings.DefNameSet);
    }

    private void DrawModsSection(Listing_Standard listing, Rect inRect)
    {
        listing.Label("Mods (packageId) - check to include");
        var searchRect = listing.GetRect(24f);
        _searchMods = Widgets.TextField(searchRect, _searchMods ?? "");
        
        var mods = LoadedModManager.RunningModsListForReading.OrderBy(m => m.Name).ToList();
        
        var remainingHeight = inRect.height - listing.CurHeight - 50f;
        var boxRect = listing.GetRect(Mathf.Max(200f, remainingHeight));
        Widgets.DrawBox(boxRect);
        
        var innerRect = new Rect(0, 0, boxRect.width - 16f, Mathf.Max(200f, mods.Count * 28f));
        Widgets.BeginScrollView(boxRect, ref _scrollMods, innerRect);
        
        float y = 0f;
        foreach (var mod in mods)
        {
            if (!IsModVisible(mod, _searchMods)) continue;

            bool isSelected = Settings.PackageIdSet.Contains(mod.PackageId);
            var row = new Rect(0, y, innerRect.width, 24f);
            bool prev = isSelected;
            
            Widgets.CheckboxLabeled(row, $"{mod.Name} ({mod.PackageId})", ref isSelected);
            
            if (isSelected) Settings.PackageIdSet.Add(mod.PackageId);
            else Settings.PackageIdSet.Remove(mod.PackageId);
            
            if (prev != isSelected) Settings.Write();
            y += 24f;
        }
        Widgets.EndScrollView();
    }

    private void DrawScrollableDefList(Listing_Standard listing, Rect inRect, 
        List<InteractionDef> defs, string searchTerm, ref Vector2 scroll, HashSet<string> selectedSet)
    {
        var remainingHeight = inRect.height - listing.CurHeight - 50f;
        var boxRect = listing.GetRect(Mathf.Max(200f, remainingHeight));
        Widgets.DrawBox(boxRect);
        
        var innerRect = new Rect(0, 0, boxRect.width - 16f, Mathf.Max(200f, defs.Count * 24f));
        Widgets.BeginScrollView(boxRect, ref scroll, innerRect);
        
        float y = 0f;
        foreach (var def in defs)
        {
            if (!IsDefVisible(def, searchTerm)) continue;

            bool isSelected = selectedSet.Contains(def.defName);
            var row = new Rect(0, y, innerRect.width, 24f);
            bool prev = isSelected;
            
            string label = $"{def.label ?? def.defName} [{def.defName}] - {def.modContentPack?.Name ?? "Core"}";
            Widgets.CheckboxLabeled(row, label, ref isSelected);
            
            if (isSelected) selectedSet.Add(def.defName);
            else selectedSet.Remove(def.defName);
            
            if (prev != isSelected) Settings.Write();
            y += 24f;
        }
        Widgets.EndScrollView();
    }

    private static bool IsModVisible(ModContentPack mod, string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm)) return true;
        
        var term = searchTerm.ToLowerInvariant();
        return (mod.Name?.ToLowerInvariant().Contains(term) == true) ||
               (mod.PackageId?.ToLowerInvariant().Contains(term) == true);
    }

    private static bool IsDefVisible(InteractionDef def, string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm)) return true;
        
        var term = searchTerm.ToLowerInvariant();
        return ((def.label ?? def.defName).ToLowerInvariant().Contains(term)) ||
               ((def.defName ?? "").ToLowerInvariant().Contains(term)) ||
               ((def.modContentPack?.Name ?? "").ToLowerInvariant().Contains(term));
    }
}

[HarmonyPatch(typeof(Game), "FinalizeInit")]
public static class Game_FinalizeInit_Patch
{
    static void Postfix()
    {
        if (Current.Game != null && Current.Game.GetComponent<ChatOverlay_AutoSaveComponent>() == null)
        {
            Current.Game.components.Add(new ChatOverlay_AutoSaveComponent(Current.Game));
        }
    }
}

[HarmonyPatch(typeof(Root), "Shutdown")]
public static class Root_Shutdown_Patch
{
    static void Prefix()
    {
        ChatOverlay_Boot.TrySaveOverlayRect(ChatOverlayRenderer.GetCurrentRect(), force: true);
    }
}