using HarmonyLib;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using RimWorld;

public class ChatOverlayWindow : Window
{
    private Vector2 scroll;

    // 自動追従のための前回情報
    private float lastContentHeight;
    private float lastViewHeight;
    private int lastRevision = -1;
    private const float BottomThreshold = 6f;

    public ChatOverlayWindow()
    {
        doWindowBackground = false;
        absorbInputAroundWindow = false;
        draggable = true;
        forcePause = false;
        preventCameraMotion = false;
        resizeable = true;
        closeOnClickedOutside = false;

        // ここを変更（上位表示をやめて下位レイヤーへ）
        layer = WindowLayer.GameUI; // 旧: WindowLayer.SubSuper など

        // Esc/Enterで閉じない
        closeOnCancel = false;
        closeOnAccept = false;
    }

    // 現在のウィンドウ矩形を外部から安全に参照するための公開プロパティ
    public Rect CurrentRect => windowRect;

    // 初期サイズは保存値があればそれを使う
    public override Vector2 InitialSize
    {
        get
        {
            var s = ChatOverlayMod.Settings;
            float w = (s != null && s.OverlayW > 0f) ? s.OverlayW : 820f;
            float h = (s != null && s.OverlayH > 0f) ? s.OverlayH : 360f;
            return new Vector2(w, h);
        }
    }

    public override void PreOpen()
    {
        base.PreOpen();

        var s = ChatOverlayMod.Settings;
        if (s == null) return;

        // サイズ復元
        if (s.OverlayW > 0f && s.OverlayH > 0f)
            windowRect.size = new Vector2(s.OverlayW, s.OverlayH);

        // 位置復元（画面内にクランプ）
        float maxX = UI.screenWidth  - windowRect.width;
        float maxY = UI.screenHeight - windowRect.height;
        if (s.OverlayX >= 0f) windowRect.x = Mathf.Clamp(s.OverlayX, 0f, Mathf.Max(0f, maxX));
        if (s.OverlayY >= 0f) windowRect.y = Mathf.Clamp(s.OverlayY, 0f, Mathf.Max(0f, maxY));
    }

    public override void PostClose()
    {
        base.PostClose();

        var s = ChatOverlayMod.Settings;
        if (s == null) return;

        // 現在の位置/サイズを保存
        s.OverlayX = windowRect.x;
        s.OverlayY = windowRect.y;
        s.OverlayW = windowRect.width;
        s.OverlayH = windowRect.height;
        s.Write();
    }

    // Esc / Enter 無効化
    public override void OnCancelKeyPressed() { }
    public override void OnAcceptKeyPressed() { }

    public override void DoWindowContents(Rect inRect)
    {
        // 背景
        Widgets.DrawBoxSolid(inRect, new Color(0f, 0f, 0f, 0.35f));
        var view = inRect.ContractedBy(6f);

        var lines = ChatState.Lines;
        int revision = ChatState.Revision;

        bool wasAtBottom = scroll.y >= (lastContentHeight - lastViewHeight - BottomThreshold);

        var prevFont = Text.Font;
        var prevAnchor = Text.Anchor;
        var prevWrap = Text.WordWrap;

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.WordWrap = true;

        float textWidth = view.width - 16f;
        
        // 高さをキャッシュして再利用
        float[] lineHeights = new float[lines.Length];
        float contentHeight = 0f;
        for (int i = 0; i < lines.Length; i++)
        {
            lineHeights[i] = Text.CalcHeight(lines[i], textWidth);
            contentHeight += lineHeights[i];
        }

        if (revision != lastRevision && wasAtBottom)
        {
            scroll.y = Mathf.Max(0f, contentHeight - view.height);
        }

        Widgets.BeginScrollView(view, ref scroll, new Rect(0, 0, textWidth, Mathf.Max(contentHeight, view.height)));
        float y = 0f;
        for (int i = 0; i < lines.Length; i++)
        {
            float h = lineHeights[i]; // キャッシュされた値を使用
            Widgets.Label(new Rect(0, y, textWidth, h), lines[i]);
            y += h;
        }
        Widgets.EndScrollView();

        Text.Font = prevFont;
        Text.Anchor = prevAnchor;
        Text.WordWrap = prevWrap;

        lastRevision = revision;
        lastContentHeight = contentHeight;
        lastViewHeight = view.height;

        scroll.y = Mathf.Clamp(scroll.y, 0f, Mathf.Max(0f, contentHeight - view.height));
    }
}

//===================== フィルタ適用 =====================

[HarmonyPatch(typeof(PlayLog), nameof(PlayLog.Add))]
public static class Patch_PlayLog_Add
{
    private static readonly FieldInfo F_Initiator = AccessTools.Field(typeof(PlayLogEntry_Interaction), "initiator");
    private static readonly FieldInfo F_Recipient  = AccessTools.Field(typeof(PlayLogEntry_Interaction), "recipient");

    static void Postfix(LogEntry entry)
    {
        // 早期リターンを最適化
        if (entry == null || !(entry is PlayLogEntry_Interaction inter))
            return;
        
        if (!ChatOverlayFilter.ShouldInclude(entry))
            return;

        ChatOverlay_Boot.EnsureOverlayExists();

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

        string name = GetSubjectName(subjectPawn, initiator, recipient);
        return $"【{name}】{body}";
    }

    private static string GetSubjectName(Pawn subject, Thing initiator, Thing recipient)
    {
        if (subject != null) return subject.LabelShortCap;
        if (initiator is Pawn ip) return ip.LabelShortCap;
        if (recipient is Pawn rp) return rp.LabelShortCap;
        return initiator?.LabelCap ?? recipient?.LabelCap ?? "???";
    }
}

// Mod アセンブリ → packageId 対応表（packageId が取れない場合の補完）
static class ModAssemblyIndex
{
    private static readonly Dictionary<string, string> asmToPkg = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
    private static bool built;

    public static string ResolvePackageIdFromAssembly(string assemblyName)
    {
        if (!built) Build();
        if (string.IsNullOrEmpty(assemblyName)) return null;
        return asmToPkg.TryGetValue(assemblyName, out var pkg) ? pkg : null;
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

                // リフレクションを最適化
                var fi = AccessTools.Field(handler.GetType(), "loadedAssemblies");
                if (fi == null) continue;
                
                var list = fi.GetValue(handler) as System.Collections.IEnumerable;
                if (list == null) continue;

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
        catch { /* fail-safe */ }
    }
}

// フィルタ本体（設定連動 + packageId補完）
static class ChatOverlayFilter
{
    private static FieldInfo _fInteractionDef; // "interaction" or "interactionDef" or "def"

    public static bool ShouldInclude(LogEntry entry)
    {
        var settings = ChatOverlayMod.Settings ?? new ChatOverlaySettings();

        if (settings.Mode == ChatOverlayFilterMode.Off)
            return true;

        string defName = null;
        string packageId = null;
        string asmName = entry?.GetType()?.Assembly?.GetName()?.Name;

        if (entry is PlayLogEntry_Interaction inter)
        {
            var def = GetInteractionDef(inter);
            defName = def?.defName;
            packageId = def?.modContentPack?.PackageId;

            // 代替手段: リフレクションで直接フィールドを探索
            if (def == null)
            {
                var fields = typeof(PlayLogEntry_Interaction).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                
                // すべてのフィールドをチェック
                foreach (var field in fields)
                {
                    var value = field.GetValue(inter);
                    
                    if (value is InteractionDef interDef)
                    {
                        def = interDef;
                        defName = interDef.defName;
                        packageId = interDef.modContentPack?.PackageId;
                        break;
                    }
                }
            }
        }

        // packageId が取れない場合はアセンブリ名から推定
        string effectivePkg = !string.IsNullOrEmpty(packageId)
            ? packageId
            : ModAssemblyIndex.ResolvePackageIdFromAssembly(asmName);

        if (settings.Mode == ChatOverlayFilterMode.Whitelist)
        {
            bool hit =
                (!string.IsNullOrEmpty(effectivePkg) && settings.PackageIdSet.Contains(effectivePkg)) ||
                (!string.IsNullOrEmpty(defName) && settings.DefNameSet.Contains(defName));
            
            return hit;
        }

        // 将来: Blacklist
        if (settings.Mode == ChatOverlayFilterMode.Blacklist)
        {
            bool blocked =
                (!string.IsNullOrEmpty(effectivePkg) && settings.PackageIdSet.Contains(effectivePkg)) ||
                (!string.IsNullOrEmpty(defName) && settings.DefNameSet.Contains(defName));
            return !blocked;
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

    private static string lastText;
    private static int lastTick;
    private static int revision;

    private const int DuplicateToleranceTicks = 60;

    public static void Push(string s)
    {
        int now = Find.TickManager?.TicksGame ?? 0;

        lock (lockObject)
        {
            if (lastText != null && s == lastText && (now - lastTick) <= DuplicateToleranceTicks)
                return;

            if (buf.Count > 200) buf.Dequeue();
            buf.Enqueue(s);

            lastText = s;
            lastTick = now;
            revision++;
        }
    }

    public static void Clear()
    {
        lock (lockObject)
        {
            buf.Clear();
            lastText = null;
            lastTick = 0;
            revision++; // 変更通知
        }
    }

    public static string[] Lines
    {
        get
        {
            lock (lockObject)
            {
                return buf.ToArray();
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