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
    private Vector2 _scrollGeneral; // Generalタブ用のスクロール変数を追加
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

    // タイトル描画用のヘルパーメソッド
    private void DrawSectionTitle(Listing_Standard listing, string title)
    {
        var prevFont = Text.Font;
        var prevColor = GUI.color;
        
        Text.Font = GameFont.Medium; // より大きなフォント
        GUI.color = new Color(0.9f, 0.9f, 0.6f, 1f); // 少し黄色がかった色で強調
        
        listing.Label(title);
        
        Text.Font = prevFont;
        GUI.color = prevColor;
        
        listing.Gap(6f); // タイトル後に小さなギャップ
    }

    // 代替案：下線付きタイトル
    private void DrawSectionTitleWithUnderline(Listing_Standard listing, string title)
    {
        var prevFont = Text.Font;
        var prevColor = GUI.color;
        
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        
        var titleRect = listing.GetRect(Text.LineHeight);
        Widgets.Label(titleRect, title);
        
        // 下線を描画
        var underlineRect = new Rect(titleRect.x, titleRect.yMax - 1f, titleRect.width * 0.8f, 1f);
        Widgets.DrawBoxSolid(underlineRect, new Color(0.7f, 0.7f, 0.7f, 0.8f));
        
        Text.Font = prevFont;
        GUI.color = prevColor;
        
        listing.Gap(8f);
    }

    // 代替案：インデント付きタイトル
    private void DrawSectionTitleIndented(Listing_Standard listing, string title)
    {
        var prevFont = Text.Font;
        var prevColor = GUI.color;
        
        Text.Font = GameFont.Small;
        GUI.color = new Color(0.8f, 0.8f, 1f, 1f); // 薄い青色
        
        var titleRect = listing.GetRect(Text.LineHeight);
        var indentedRect = new Rect(titleRect.x - 8f, titleRect.y, titleRect.width, titleRect.height);
        
        // 背景を少し暗くする
        Widgets.DrawBoxSolid(indentedRect.ExpandedBy(4f, 2f), new Color(0f, 0f, 0f, 0.1f));
        
        Widgets.Label(titleRect, $"■ {title}"); // 記号を付けて強調
        
        Text.Font = prevFont;
        GUI.color = prevColor;
        
        listing.Gap(6f);
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
        var availableHeight = inRect.height - listing.CurHeight - 10f;
        var scrollRect = listing.GetRect(availableHeight);
        
        // 十分に大きな高さを確保（RimWorldの標準的な手法）
        var viewRect = new Rect(0, 0, scrollRect.width - 16f, 1500f);
        Widgets.BeginScrollView(scrollRect, ref _scrollGeneral, viewRect);
        
        var contentListing = new Listing_Standard();
        contentListing.Begin(viewRect);
        
        DrawGeneralTabContent(contentListing);
        
        contentListing.End();
        Widgets.EndScrollView();
    }

    private void DrawGeneralTabContent(Listing_Standard listing)
    {
        // コントロールボタン
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

        // 設定項目（タイトルを強調）
        DrawOpacitySlider(listing);
        DrawDisplayLayerSelection(listing);
        DrawSpeakerNameOption(listing);
        DrawFontSizeSelection(listing);
        DrawTextColorPicker(listing);
        DrawUsageInstructions(listing);
    }

    private void DrawOpacitySlider(Listing_Standard listing)
    {
        DrawSectionTitle(listing, "Background Opacity");
        
        listing.Label($"Current: {Settings.BackgroundOpacity:F2} (0.0 = Transparent, 1.0 = Opaque)");
        float newOpacity = listing.Slider(Settings.BackgroundOpacity, 0.0f, 1.0f);
        if (Math.Abs(newOpacity - Settings.BackgroundOpacity) > 0.001f)
        {
            Settings.BackgroundOpacity = newOpacity;
            Settings.Write();
        }
        listing.Gap();
    }

    private void DrawDisplayLayerSelection(Listing_Standard listing)
    {
        DrawSectionTitle(listing, "Display Layer");
        
        var layers = new[]
        {
            (ChatOverlayDisplayLayer.Standard, "Standard"),
            (ChatOverlayDisplayLayer.Background, "Background (behind all UI elements)")
        };

        foreach (var (layer, label) in layers)
        {
            if (listing.RadioButton(label, Settings.DisplayLayer == layer) && Settings.DisplayLayer != layer)
            {
                Settings.DisplayLayer = layer;
                Settings.Write();
            }
        }
        listing.Gap();
    }

    private void DrawSpeakerNameOption(Listing_Standard listing)
    {
        DrawSectionTitle(listing, "Speaker Name Settings");
        
        bool prevValue = Settings.ShowSpeakerName;
        listing.CheckboxLabeled("Show speaker name", ref Settings.ShowSpeakerName);
        if (prevValue != Settings.ShowSpeakerName)
        {
            Settings.Write();
        }

        if (Settings.ShowSpeakerName)
        {
            listing.Gap(4f);
            
            // サブタイトル用のインデント付きスタイル
            var prevFont = Text.Font;
            var prevColor = GUI.color;
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            listing.Label("  Format:");
            Text.Font = prevFont;
            GUI.color = prevColor;
            
            var formats = new[]
            {
                (SpeakerNameFormat.Japanese, "    Japanese style (【Name】)"),
                (SpeakerNameFormat.Square, "    Square brackets ([Name])"),
                (SpeakerNameFormat.Parentheses, "    Parentheses ((Name))"),
                (SpeakerNameFormat.Angle, "    Angle brackets (<Name>)"),
                (SpeakerNameFormat.Colon, "    Colon (Name:)")
            };

            foreach (var (format, label) in formats)
            {
                if (listing.RadioButton(label, Settings.NameFormat == format) && Settings.NameFormat != format)
                {
                    Settings.NameFormat = format;
                    Settings.Write();
                }
            }
        }
        
        listing.Gap();
    }

    private void DrawFontSizeSelection(Listing_Standard listing)
    {
        DrawSectionTitle(listing, "Font Size");
        
        var fontSizes = new[]
        {
            (ChatFontSize.Tiny, "Tiny"),
            (ChatFontSize.Small, "Small"),
            (ChatFontSize.Medium, "Medium")
        };

        foreach (var (fontSize, label) in fontSizes)
        {
            if (listing.RadioButton(label, Settings.FontSize == fontSize) && Settings.FontSize != fontSize)
            {
                Settings.FontSize = fontSize;
                Settings.Write();
                
                // フォントサイズ変更時に高さキャッシュをクリア
                ChatOverlayRenderer.ClearHeightCache();
            }
        }
        listing.Gap();
    }

    private void DrawTextColorPicker(Listing_Standard listing)
    {
        DrawSectionTitle(listing, "Text Color");
        
        // 色プレビュー
        var colorPreviewRect = listing.GetRect(30f);
        var previewRect = new Rect(colorPreviewRect.x, colorPreviewRect.y, 100f, 30f);
        Widgets.DrawBoxSolid(previewRect, Settings.TextColor);
        Widgets.DrawBox(previewRect);
        
        // プリセット色ボタン
        var buttonRect = new Rect(previewRect.xMax + 10f, previewRect.y, colorPreviewRect.width - previewRect.width - 10f, 30f);
        float buttonWidth = (buttonRect.width - 30f) / 4f;
        
        var presetColors = new[]
        {
            ("White", Color.white),
            ("Yellow", Color.yellow),
            ("Green", Color.green),
            ("Cyan", Color.cyan)
        };
        
        for (int i = 0; i < presetColors.Length; i++)
        {
            var (colorName, color) = presetColors[i];
            var rect = new Rect(buttonRect.x + i * (buttonWidth + 10f), buttonRect.y, buttonWidth, 30f);
            
            if (Widgets.ButtonText(rect, colorName))
            {
                Settings.TextColor = color;
                Settings.Write();
            }
        }
        
        // RGBスライダー
        listing.Gap();
        DrawColorSlider(listing, "Red", ref Settings.TextColorR);
        DrawColorSlider(listing, "Green", ref Settings.TextColorG);
        DrawColorSlider(listing, "Blue", ref Settings.TextColorB);
        DrawColorSlider(listing, "Alpha", ref Settings.TextColorA);
        
        listing.Gap();
    }

    private void DrawColorSlider(Listing_Standard listing, string label, ref float value)
    {
        listing.Label($"  {label}: {value:F2}"); // インデントを追加
        float newValue = listing.Slider(value, 0.0f, 1.0f);
        if (Math.Abs(newValue - value) > 0.001f)
        {
            value = newValue;
            Settings.Write();
        }
    }

    private void DrawUsageInstructions(Listing_Standard listing)
    {
        DrawSectionTitleWithUnderline(listing, "How to Use"); // 下線付きスタイルを使用
        
        var instructions = new[]
        {
            "• Drag the title bar (top edge) to move the overlay",
            "• Drag the bottom-right corner to resize",
            "• Use the Filters tab to control which logs appear",
            "• Change display layer to show behind/above UI elements"
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
        DrawSectionTitle(listing, "Filter Mode");
        
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
        DrawSectionTitle(listing, "Advanced Settings");

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
        DrawSectionTitle(listing, "Interaction Types");
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
        DrawSectionTitle(listing, "Mods");
        listing.Label("Check mods to include their interaction logs:");
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