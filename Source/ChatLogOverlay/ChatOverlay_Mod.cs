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

    // UI 状態
    private Vector2 _scrollMods;
    private Vector2 _scrollDefs;
    private string _searchMods = "";
    private string _searchDefs = "";

    public ChatOverlayMod(ModContentPack content) : base(content)
    {

        Settings = GetSettings<ChatOverlaySettings>();

        var h = new Harmony("kirimin.ChatLogOverlay");
        h.PatchAll();

        LongEventHandler.ExecuteWhenFinished(() =>
        {
            if (Find.WindowStack != null)
            {
                if (!Find.WindowStack.IsOpen<ChatOverlayWindow>())
                    Find.WindowStack.Add(new ChatOverlayWindow());
            }
            else
            {
                Log.Warning("[ChatOverlay] WindowStack is null at ExecuteWhenFinished");
            }
        });
    }

    public override string SettingsCategory() => "Chatlog Overlay";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        // --- オーバーレイ表示/非表示トグルボタン ---
        bool isOpen = Find.WindowStack?.IsOpen<ChatOverlayWindow>() ?? false;
        string btnLabel = isOpen ? "Hide Overlay" : "Show Overlay";
        if (Widgets.ButtonText(listing.GetRect(30f), btnLabel))
        {
            if (isOpen)
            {
                // ウィンドウを閉じる
                var win = Find.WindowStack.Windows.FirstOrDefault(w => w is ChatOverlayWindow);
                if (win != null) win.Close();
            }
            else
            {
                // ウィンドウを開く
                Find.WindowStack.Add(new ChatOverlayWindow());
            }
        }

        // ログ全クリア
        if (Widgets.ButtonText(listing.GetRect(30f), "Clear overlay logs"))
        {
            ChatState.Clear();
        }
        listing.Gap();

        // --- 既存の設定UI ---
        // モード選択
        listing.Label("Filter mode");
        if (listing.RadioButton("Off (show all)", Settings.Mode == ChatOverlayFilterMode.Off))
        {
            if (Settings.Mode != ChatOverlayFilterMode.Off)
            {
                Settings.Mode = ChatOverlayFilterMode.Off;
                Settings.Write(); // 追加
            }
        }
        if (listing.RadioButton("Whitelist (only selected)", Settings.Mode == ChatOverlayFilterMode.Whitelist))
        {
            if (Settings.Mode != ChatOverlayFilterMode.Whitelist)
            {
                Settings.Mode = ChatOverlayFilterMode.Whitelist;
                Settings.Write(); // 追加
            }
        }
        // ...Blacklistも同様
        listing.GapLine();

        // プリセットボタン
        var rectBtns = listing.GetRect(30f);
        float bw = (rectBtns.width - 18f) / 3f;

        // 1: 全Mod選択
        if (Widgets.ButtonText(new Rect(rectBtns.x, rectBtns.y, bw, 30f), "Select All Mods"))
        {
            foreach (var m in LoadedModManager.RunningModsListForReading)
                Settings.PackageIdSet.Add(m.PackageId);
            Settings.Write(); // 追加
        }

        // 2: クリア
        if (Widgets.ButtonText(new Rect(rectBtns.x + bw + 9f, rectBtns.y, bw, 30f), "Clear All"))
        {
            Settings.PackageIdSet.Clear();
            Settings.DefNameSet.Clear();
            Settings.Write(); // 追加
        }

        // 3: すべての Interaction を選択
        if (Widgets.ButtonText(new Rect(rectBtns.x + (bw + 9f) * 2f, rectBtns.y, bw, 30f), "Select all interaction types"))
        {
            foreach (var d in DefDatabase<InteractionDef>.AllDefsListForReading)
                if (!string.IsNullOrEmpty(d?.defName))
                    Settings.DefNameSet.Add(d.defName);
            Settings.Write(); // 追加
        }
        listing.Gap();

        // Mods セクション
        listing.Label("Mods (packageId) - check to include");
        var searchRectM = listing.GetRect(24f);
        _searchMods = Widgets.TextField(searchRectM, _searchMods ?? "");
        var boxMods = listing.GetRect(180f);
        Widgets.DrawBox(boxMods);
        var innerMods = new Rect(0, 0, boxMods.width - 16f, Mathf.Max(180f, LoadedModManager.RunningModsListForReading.Count * 28f));
        Widgets.BeginScrollView(boxMods, ref _scrollMods, innerMods);
        float y = 0f;

        foreach (var m in LoadedModManager.RunningModsListForReading.OrderBy(m => m.Name))
        {
            if (!string.IsNullOrEmpty(_searchMods))
            {
                var t = _searchMods.ToLowerInvariant();
                if (!(m.Name?.ToLowerInvariant().Contains(t) == true || m.PackageId?.ToLowerInvariant().Contains(t) == true))
                    continue;
            }

            bool on = Settings.PackageIdSet.Contains(m.PackageId);
            var row = new Rect(0, y, innerMods.width, 24f);
            bool prev = on;
            Widgets.CheckboxLabeled(row, $"{m.Name} ({m.PackageId})", ref on);
            if (on) Settings.PackageIdSet.Add(m.PackageId); else Settings.PackageIdSet.Remove(m.PackageId);
            if (prev != on) Settings.Write(); // 追加
            y += 24f;
        }
        Widgets.EndScrollView();
        listing.Gap();

        // InteractionDef セクション（必要な人向け）
        listing.Label("Interaction types (InteractionDef.defName)");
        var searchRectD = listing.GetRect(24f);
        _searchDefs = Widgets.TextField(searchRectD, _searchDefs ?? "");
        var defs = DefDatabase<InteractionDef>.AllDefsListForReading
            .OrderBy(d => d.modContentPack?.Name ?? "Core")
            .ThenBy(d => d.label ?? d.defName)
            .ToList();

        var boxDefs = listing.GetRect(Mathf.Min(220f, inRect.height - listing.CurHeight - 30f));
        Widgets.DrawBox(boxDefs);
        float innerH = Mathf.Max(220f, defs.Count * 24f);
        var innerDefs = new Rect(0, 0, boxDefs.width - 16f, innerH);
        Widgets.BeginScrollView(boxDefs, ref _scrollDefs, innerDefs);
        y = 0f;
        foreach (var d in defs)
        {
            if (!string.IsNullOrEmpty(_searchDefs))
            {
                var t = _searchDefs.ToLowerInvariant();
                if (!((d.label ?? d.defName).ToLowerInvariant().Contains(t) ||
                      (d.defName ?? "").ToLowerInvariant().Contains(t) ||
                      (d.modContentPack?.Name ?? "").ToLowerInvariant().Contains(t)))
                    continue;
            }
            bool on = Settings.DefNameSet.Contains(d.defName);
            var row = new Rect(0, y, innerDefs.width, 24f);
            bool prev = on;
            Widgets.CheckboxLabeled(row, $"{d.label ?? d.defName} [{d.defName}] - {(d.modContentPack?.Name ?? "Core")}", ref on);
            if (on) Settings.DefNameSet.Add(d.defName); else Settings.DefNameSet.Remove(d.defName);
            if (prev != on) Settings.Write(); // 追加
            y += 24f;
        }
        Widgets.EndScrollView();

        listing.End();
    }

    private void RenderCheckboxList<T>(Listing_Standard listing, string label, IEnumerable<T> items, 
        Func<T, string> getDisplayName, Func<T, string> getKey, HashSet<string> targetSet, 
        ref Vector2 scroll, ref string searchText, float height = 180f)
    {
        listing.Label(label);
        var searchRect = listing.GetRect(24f);
        searchText = Widgets.TextField(searchRect, searchText ?? "");

        var searchTextLocal = searchText; // ローカル変数にコピー

        var box = listing.GetRect(height);
        Widgets.DrawBox(box);

        var filteredItems = string.IsNullOrEmpty(searchTextLocal) 
            ? items 
            : items.Where(item => getDisplayName(item).ToLowerInvariant().Contains(searchTextLocal.ToLowerInvariant()));

        var itemList = filteredItems.ToList();
        var inner = new Rect(0, 0, box.width - 16f, Mathf.Max(height, itemList.Count * 24f));

        Widgets.BeginScrollView(box, ref scroll, inner);
        float y = 0f;

        foreach (var item in itemList)
        {
            string key = getKey(item);
            bool on = targetSet.Contains(key);
            var row = new Rect(0, y, inner.width, 24f);
            bool prev = on;

            Widgets.CheckboxLabeled(row, getDisplayName(item), ref on);

            if (on) targetSet.Add(key); 
            else targetSet.Remove(key);

            if (prev != on) Settings.Write();
            y += 24f;
        }

        Widgets.EndScrollView();
    }
}