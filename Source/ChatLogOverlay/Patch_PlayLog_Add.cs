using HarmonyLib;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using RimWorld;
using System.Collections.ObjectModel;

public static class ChatOverlayRenderer
{
    private static Vector2 scroll;
    private static Rect overlayRect = new Rect(50f, 50f, 820f, 360f);
    private static bool isDragging = false;
    private static bool isResizing = false;
    private static Vector2 dragOffset;
    private static Vector2 resizeStartSize;
    private static Vector2 resizeStartPos;
    private static bool isVisible = true;

    private static readonly Dictionary<(string text, float width, GameFont font), float> heightCache = 
        new Dictionary<(string, float, GameFont), float>();
    private static float lastTextWidth = -1f;
    private static int lastCachedRevision = -1;
    private static GameFont lastCachedFont = GameFont.Small;
    private static float[] cachedLineHeights;
    private static float cachedContentHeight;

    private static ChatOverlaySettings cachedSettings;
    private static int settingsUpdateCount = -1;

    private static float lastContentHeight;
    private static float lastViewHeight;
    private static int lastRevision = -1;
    private const float BottomThreshold = 6f;
    private const int MaxCacheSize = 500;

    static ChatOverlayRenderer()
    {
        LoadSettings();
    }

    private static void LoadSettings()
    {
        var s = ChatOverlayMod.Settings;
        if (s?.HasValidOverlayRect == true)
        {
            overlayRect = new Rect(s.OverlayX, s.OverlayY, s.OverlayW, s.OverlayH);
        }
        RefreshSettingsCache();
    }

    private static void RefreshSettingsCache()
    {
        var settings = ChatOverlayMod.Settings;
        if (settings != null)
        {
            cachedSettings = settings;
            settingsUpdateCount = settings.GetHashCode(); // 簡易的な変更検知
        }
    }

    private static ChatOverlaySettings GetCachedSettings()
    {
        var settings = ChatOverlayMod.Settings;
        if (settings == null) return null;
        
        int currentHash = settings.GetHashCode();
        if (cachedSettings == null || settingsUpdateCount != currentHash)
        {
            cachedSettings = settings;
            settingsUpdateCount = currentHash;
        }
        
        return cachedSettings;
    }

    private static void SaveSettings()
    {
        ChatOverlay_Boot.TrySaveOverlayRect(overlayRect, force: false);
    }

    public static Rect GetCurrentRect() => overlayRect;
    
    public static bool IsVisible 
    { 
        get => isVisible; 
        set => isVisible = value; 
    }

    public static void ClearHeightCache()
    {
        heightCache.Clear();
        lastCachedRevision = -1;
        cachedLineHeights = null;
    }

    public static void DrawOverlay()
    {
        if (!isVisible) return;

        HandleInput();

        var settings = GetCachedSettings();
        float opacity = settings?.BackgroundOpacity ?? 0.35f;
        Widgets.DrawBoxSolid(overlayRect, new Color(0f, 0f, 0f, opacity));
        
        var view = overlayRect.ContractedBy(6f);
        var lines = ChatState.Lines;
        int revision = ChatState.Revision;

        bool wasAtBottom = scroll.y >= (lastContentHeight - lastViewHeight - BottomThreshold);

        var prevFont = Text.Font;
        var prevAnchor = Text.Anchor;
        var prevWrap = Text.WordWrap;
        var prevColor = GUI.color;

        GameFont currentFont = settings?.GetGameFont() ?? GameFont.Small;
        Color textColor = settings?.TextColor ?? Color.white;
        
        Text.Font = currentFont;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.WordWrap = true;
        GUI.color = textColor;

        float textWidth = view.width - 16f;
        
        bool needsHeightRecalc = revision != lastCachedRevision || 
                                Mathf.Abs(textWidth - lastTextWidth) > 0.1f ||
                                currentFont != lastCachedFont ||
                                cachedLineHeights == null ||
                                cachedLineHeights.Length != lines.Count;

        if (needsHeightRecalc)
        {
            UpdateHeightCache(lines, textWidth, currentFont);
            lastCachedRevision = revision;
            lastTextWidth = textWidth;
            lastCachedFont = currentFont;
        }

        if (revision != lastRevision && wasAtBottom)
        {
            scroll.y = Mathf.Max(0f, cachedContentHeight - view.height);
        }

        Widgets.BeginScrollView(view, ref scroll, new Rect(0, 0, textWidth, Mathf.Max(cachedContentHeight, view.height)));
        float y = 0f;
        for (int i = 0; i < lines.Count; i++)
        {
            float h = cachedLineHeights[i];
            Widgets.Label(new Rect(0, y, textWidth, h), lines[i]);
            y += h;
        }
        Widgets.EndScrollView();

        Text.Font = prevFont;
        Text.Anchor = prevAnchor;
        Text.WordWrap = prevWrap;
        GUI.color = prevColor;

        lastRevision = revision;
        lastContentHeight = cachedContentHeight;
        lastViewHeight = view.height;

        scroll.y = Mathf.Clamp(scroll.y, 0f, Mathf.Max(0f, cachedContentHeight - view.height));

        DrawResizeHandle();
    }

    private static void UpdateHeightCache(ReadOnlyCollection<string> lines, float textWidth, GameFont font)
    {
        // メモリリーク対策：より積極的なクリア
        if (heightCache.Count > MaxCacheSize)
        {
            // 古いエントリを削除（LRU的な実装）
            var keysToRemove = heightCache.Keys.Take(heightCache.Count - MaxCacheSize / 2).ToList();
            foreach (var key in keysToRemove)
            {
                heightCache.Remove(key);
            }
        }

        cachedLineHeights = new float[lines.Count];
        cachedContentHeight = 0f;

        for (int i = 0; i < lines.Count; i++)
        {
            var key = (lines[i], textWidth, font);
            if (!heightCache.TryGetValue(key, out float height))
            {
                height = Text.CalcHeight(lines[i], textWidth);
                heightCache[key] = height;
            }
            cachedLineHeights[i] = height;
            cachedContentHeight += height;
        }
    }

    private static bool HandleInput()
    {
        Event current = Event.current;
        Vector2 mousePos = current.mousePosition;

        if (current.type == EventType.MouseDown && current.button == 0)
        {
            Rect resizeHandle = new Rect(overlayRect.xMax - 20f, overlayRect.yMax - 20f, 20f, 20f);
            if (resizeHandle.Contains(mousePos))
            {
                isResizing = true;
                resizeStartSize = overlayRect.size;
                resizeStartPos = mousePos;
                current.Use();
                return true;
            }

            Rect titleBar = new Rect(overlayRect.x, overlayRect.y, overlayRect.width, 25f);
            if (titleBar.Contains(mousePos))
            {
                isDragging = true;
                dragOffset = mousePos - new Vector2(overlayRect.x, overlayRect.y);
                current.Use();
                return true;
            }

            return false;
        }
        else if (current.type == EventType.MouseUp && current.button == 0)
        {
            if (isDragging || isResizing)
            {
                SaveSettings();
                isDragging = false;
                isResizing = false;
                return true;
            }
        }
        else if (current.type == EventType.MouseDrag && current.button == 0)
        {
            if (isResizing)
            {
                Vector2 delta = mousePos - resizeStartPos;
                overlayRect.width = Mathf.Max(200f, resizeStartSize.x + delta.x);
                overlayRect.height = Mathf.Max(100f, resizeStartSize.y + delta.y);
                current.Use();
                return true;
            }
            else if (isDragging)
            {
                overlayRect.position = mousePos - dragOffset;
                overlayRect.x = Mathf.Clamp(overlayRect.x, 0f, UI.screenWidth - overlayRect.width);
                overlayRect.y = Mathf.Clamp(overlayRect.y, 0f, UI.screenHeight - overlayRect.height);
                current.Use();
                return true;
            }
        }

        return false;
    }

    private static void DrawResizeHandle()
    {
        Rect resizeHandle = new Rect(overlayRect.xMax - 20f, overlayRect.yMax - 20f, 20f, 20f);
        Widgets.DrawBoxSolid(resizeHandle, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        
        Rect titleBar = new Rect(overlayRect.x, overlayRect.y, overlayRect.width, 25f);
        Widgets.DrawBoxSolid(titleBar, new Color(0.2f, 0.2f, 0.2f, 0.3f));
    }
}

[HarmonyPatch(typeof(MapInterface), "MapInterfaceOnGUI_AfterMainTabs")]
public static class MapInterface_OnGUI_AfterMainTabs_Patch
{
    static void Prefix()
    {
        var settings = ChatOverlayMod.Settings;
        if (settings?.DisplayLayer == ChatOverlayDisplayLayer.Standard)
        {
            ChatOverlayRenderer.DrawOverlay();
        }
    }
}

[HarmonyPatch(typeof(MapInterface), "MapInterfaceOnGUI_BeforeMainTabs")]
public static class MapInterface_OnGUI_BeforeMainTabs_Patch
{
    static void Prefix()
    {
        var settings = ChatOverlayMod.Settings;
        if (settings?.DisplayLayer == ChatOverlayDisplayLayer.Background)
        {
            ChatOverlayRenderer.DrawOverlay();
        }
    }
}

[HarmonyPatch(typeof(PlayLog), nameof(PlayLog.Add))]
public static class Patch_PlayLog_Add
{
    private static readonly FieldInfo F_Initiator = AccessTools.Field(typeof(PlayLogEntry_Interaction), "initiator");
    private static readonly FieldInfo F_Recipient = AccessTools.Field(typeof(PlayLogEntry_Interaction), "recipient");

    static void Postfix(LogEntry entry)
    {
        if (!(entry is PlayLogEntry_Interaction inter) || !ChatOverlayFilter.ShouldInclude(entry))
            return;

        string text = FormatInteraction(inter);
        if (!string.IsNullOrEmpty(text))
        {
            ChatState.Push(text);
        }
    }

    private static string FormatInteraction(PlayLogEntry_Interaction inter)
    {
        var initiator = F_Initiator?.GetValue(inter) as Thing;
        var recipient = F_Recipient?.GetValue(inter) as Thing;

        var subjectPawn = initiator as Pawn ?? recipient as Pawn;
        var pov = (Thing)subjectPawn ?? initiator ?? recipient;

        string body = inter.ToGameStringFromPOV(pov, forceLog: true);
        if (string.IsNullOrEmpty(body))
            body = inter.ToGameStringFromPOV(null, forceLog: true);
        if (string.IsNullOrEmpty(body))
            return null;

        var settings = ChatOverlayMod.Settings;
        bool showSpeakerName = settings?.ShowSpeakerName ?? true;

        if (showSpeakerName)
        {
            string name = GetSubjectName(subjectPawn, initiator, recipient);
            string formattedName = FormatSpeakerName(name, settings?.NameFormat ?? SpeakerNameFormat.Japanese);
            return $"{formattedName}{body}";
        }
        else
        {
            return body;
        }
    }

    private static string FormatSpeakerName(string name, SpeakerNameFormat format)
    {
        switch (format)
        {
            case SpeakerNameFormat.Japanese:
                return $"【{name}】";
            case SpeakerNameFormat.Square:
                return $"[{name}] ";
            case SpeakerNameFormat.Parentheses:
                return $"({name}) ";
            case SpeakerNameFormat.Angle:
                return $"<{name}> ";
            case SpeakerNameFormat.Colon:
                return $"{name}: ";
            default:
                return $"【{name}】";
        }
    }

    private static string GetSubjectName(Pawn subject, Thing initiator, Thing recipient)
    {
        if (subject != null) return subject.LabelShortCap;
        if (initiator is Pawn ip) return ip.LabelShortCap;
        if (recipient is Pawn rp) return rp.LabelShortCap;
        return initiator?.LabelCap ?? recipient?.LabelCap ?? "???";
    }
}

static class ModAssemblyIndex
{
    private static readonly Dictionary<string, string> asmToPkg = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
    private static bool built;

    public static string ResolvePackageIdFromAssembly(string assemblyName)
    {
        if (!built) Build();
        return string.IsNullOrEmpty(assemblyName) ? null : 
               asmToPkg.TryGetValue(assemblyName, out var pkg) ? pkg : null;
    }

    private static void Build()
    {
        built = true;
        try
        {
            foreach (var m in LoadedModManager.RunningModsListForReading)
            {
                var handler = m.assemblies;
                if (handler == null) continue;

                var fi = AccessTools.Field(handler.GetType(), "loadedAssemblies");
                if (!(fi?.GetValue(handler) is System.Collections.IEnumerable list)) continue;

                foreach (var obj in list)
                {
                    if (obj is Assembly asm)
                    {
                        var name = asm.GetName().Name;
                        if (!asmToPkg.ContainsKey(name))
                            asmToPkg[name] = m.PackageId;
                    }
                }
            }
        }
        catch { }
    }
}

static class ChatOverlayFilter
{
    private static FieldInfo _fInteractionDef;
    private static readonly FieldInfo F_Initiator = AccessTools.Field(typeof(PlayLogEntry_Interaction), "initiator");
    private static readonly FieldInfo F_Recipient = AccessTools.Field(typeof(PlayLogEntry_Interaction), "recipient");
    private static readonly Dictionary<System.Type, FieldInfo[]> fieldCache = 
        new Dictionary<System.Type, FieldInfo[]>();

    public static bool ShouldInclude(LogEntry entry)
    {
        var settings = ChatOverlayMod.Settings;
        if (settings == null) return true;

        if (settings.Mode == ChatOverlayFilterMode.Off && !settings.EnableSpeakerFilter)
            return true;

        if (settings.EnableSpeakerFilter && entry is PlayLogEntry_Interaction inter)
        {
            if (!IsSpeakerAllowed(inter, settings))
                return false;
        }

        if (settings.Mode == ChatOverlayFilterMode.Off)
            return true;

        string defName = null;
        string packageId = null;
        string asmName = entry?.GetType()?.Assembly?.GetName()?.Name;

        if (entry is PlayLogEntry_Interaction interaction)
        {
            var def = GetInteractionDef(interaction);
            defName = def?.defName;
            packageId = def?.modContentPack?.PackageId;

            if (def == null)
            {
                var entryType = typeof(PlayLogEntry_Interaction);
                if (!fieldCache.TryGetValue(entryType, out var fields))
                {
                    fields = entryType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                    fieldCache[entryType] = fields;
                }
                
                foreach (var field in fields)
                {
                    if (field.GetValue(interaction) is InteractionDef interDef)
                    {
                        defName = interDef.defName;
                        packageId = interDef.modContentPack?.PackageId;
                        break;
                    }
                }
            }
        }

        string effectivePkg = !string.IsNullOrEmpty(packageId)
            ? packageId
            : ModAssemblyIndex.ResolvePackageIdFromAssembly(asmName);

        if (settings.Mode == ChatOverlayFilterMode.Whitelist)
        {
            bool packageMatches = !string.IsNullOrEmpty(effectivePkg) && settings.PackageIdSet.Contains(effectivePkg);
            bool defMatches = !string.IsNullOrEmpty(defName) && settings.DefNameSet.Contains(defName);
            
            bool hasPackageFilter = settings.PackageIdSet.Count > 0;
            bool hasDefFilter = settings.DefNameSet.Count > 0;
            
            if (hasPackageFilter && hasDefFilter)
            {
                return packageMatches && defMatches;
            }
            else if (hasPackageFilter)
            {
                return packageMatches;
            }
            else if (hasDefFilter)
            {
                return defMatches;
            }
            else
            {
                return true;
            }
        }

        if (settings.Mode == ChatOverlayFilterMode.Blacklist)
        {
            bool blocked =
                (!string.IsNullOrEmpty(effectivePkg) && settings.PackageIdSet.Contains(effectivePkg)) ||
                (!string.IsNullOrEmpty(defName) && settings.DefNameSet.Contains(defName));
            return !blocked;
        }

        return true;
    }

    private static bool IsSpeakerAllowed(PlayLogEntry_Interaction inter, ChatOverlaySettings settings)
    {
        if (!settings.EnableSpeakerFilter || settings.SpeakerNameSet.Count == 0)
            return true;

        var initiator = F_Initiator?.GetValue(inter) as Thing;
        var recipient = F_Recipient?.GetValue(inter) as Thing;

        var subjectPawn = initiator as Pawn ?? recipient as Pawn;
        
        if (subjectPawn?.LabelShortCap != null)
        {
            return settings.SpeakerNameSet.Contains(subjectPawn.LabelShortCap);
        }
        
        if (initiator is Pawn ip && ip.LabelShortCap != null)
        {
            return settings.SpeakerNameSet.Contains(ip.LabelShortCap);
        }
        
        if (recipient is Pawn rp && rp.LabelShortCap != null)
        {
            return settings.SpeakerNameSet.Contains(rp.LabelShortCap);
        }

        return true;
    }

    private static Def GetInteractionDef(PlayLogEntry_Interaction inter)
    {
        if (_fInteractionDef == null)
        {
            _fInteractionDef =
                AccessTools.Field(typeof(PlayLogEntry_Interaction), "intDef") ??
                AccessTools.Field(typeof(PlayLogEntry_Interaction), "interaction") ??
                AccessTools.Field(typeof(PlayLogEntry_Interaction), "interactionDef") ??
                AccessTools.Field(typeof(PlayLogEntry_Interaction), "def");
        }
        
        return _fInteractionDef?.GetValue(inter) as Def;
    }
}

static class ChatState
{
    private static readonly Queue<string> buf = new Queue<string>(256);
    private static readonly object lockObject = new object();
    private static ReadOnlyCollection<string> cachedLines;
    private static bool linesCacheDirty = true;

    private static string lastText;
    private static int lastTick;
    private static int lastHashCode;
    private static int revision;

    private const int DuplicateToleranceTicks = 60;

    public static void Push(string s)
    {
        int now = Find.TickManager?.TicksGame ?? 0;
        int hashCode = s?.GetHashCode() ?? 0;

        lock (lockObject)
        {
            if (lastHashCode == hashCode && lastText != null && s == lastText && (now - lastTick) <= DuplicateToleranceTicks)
                return;

            if (buf.Count > 200) buf.Dequeue();
            buf.Enqueue(s);

            lastText = s;
            lastTick = now;
            lastHashCode = hashCode;
            revision++;
            linesCacheDirty = true;
        }
    }

    public static void Clear()
    {
        lock (lockObject)
        {
            buf.Clear();
            lastText = null;
            lastTick = 0;
            lastHashCode = 0;
            revision++;
            linesCacheDirty = true;
        }
    }

    public static ReadOnlyCollection<string> Lines
    {
        get
        {
            lock (lockObject)
            {
                if (linesCacheDirty)
                {
                    cachedLines = new ReadOnlyCollection<string>(buf.ToArray());
                    linesCacheDirty = false;
                }
                return cachedLines;
            }
        }
    }

    public static int Revision
    {
        get
        {
            lock (lockObject) return revision;
        }
    }
}