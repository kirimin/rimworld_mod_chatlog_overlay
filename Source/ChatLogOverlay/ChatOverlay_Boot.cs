using HarmonyLib;
using System.Linq;
using UnityEngine;
using Verse;

// 複数の初期化方法を試す
[StaticConstructorOnStartup]
public static class ChatOverlay_Boot
{
    private static ChatOverlayWindow overlayWindow;

    // 直近保存した矩形（無駄書き込み防止用）
    private static Rect? _lastSavedRect;

    static ChatOverlay_Boot()
    {
        Initialize();
    }

    private static void Initialize()
    {
        try
        {
            var h = new Harmony("kirimin.chatlogoverlay"); // packageIdと一致させる
            h.PatchAll();

            // 即座にウィンドウを作成
            CreateOverlayWindow();
        }
        catch (System.Exception ex)
        {
            Log.Error($"[ChatOverlay] Failed to initialize MOD: {ex}");
        }
    }

    public static void CreateOverlayWindow()
    {
        if (Find.WindowStack == null) return;

        // 既に開いていれば何もしない
        if (Find.WindowStack.IsOpen<ChatOverlayWindow>())
            return;

        overlayWindow = new ChatOverlayWindow();
        Find.WindowStack.Add(overlayWindow);
    }

    public static void EnsureOverlayExists()
    {
        if (Find.WindowStack == null) return;
        if (!Find.WindowStack.IsOpen<ChatOverlayWindow>())
            CreateOverlayWindow();
    }

    // 位置/サイズの保存（必要時のみ書き込み）
    public static void TrySaveOverlayRect(bool force = false)
    {
        var s = ChatOverlayMod.Settings;
        var ws = Find.WindowStack;
        if (s == null || ws == null) return;

        // 参照可能なウィンドウを取得
        var win = overlayWindow;
        if (win == null || !ws.IsOpen<ChatOverlayWindow>())
            win = ws.Windows?.FirstOrDefault(w => w is ChatOverlayWindow) as ChatOverlayWindow;

        if (win == null) return;

        var r = win.CurrentRect;
        if (!force && _lastSavedRect.HasValue && ApproximatelyEqual(_lastSavedRect.Value, r))
            return;

        s.OverlayX = r.x;
        s.OverlayY = r.y;
        s.OverlayW = r.width;
        s.OverlayH = r.height;
        s.Write();

        _lastSavedRect = r;
    }

    private static bool ApproximatelyEqual(Rect a, Rect b)
    {
        return Mathf.Abs(a.x - b.x) < ChatOverlayConstants.RectComparisonTolerance &&
               Mathf.Abs(a.y - b.y) < ChatOverlayConstants.RectComparisonTolerance &&
               Mathf.Abs(a.width - b.width) < ChatOverlayConstants.RectComparisonTolerance &&
               Mathf.Abs(a.height - b.height) < ChatOverlayConstants.RectComparisonTolerance;
    }
}

// 進行中は一定間隔で自動保存
public class ChatOverlay_AutoSaveComponent : GameComponent
{
    private int _lastSaveTick;

    public ChatOverlay_AutoSaveComponent(Game game) { }

    public override void GameComponentTick()
    {
        int now = Find.TickManager?.TicksGame ?? 0;
        if (now - _lastSaveTick < ChatOverlayConstants.AutoSaveTickInterval) return;
        _lastSaveTick = now;

        ChatOverlay_Boot.TrySaveOverlayRect(force: false);
    }
}

// ゲーム初期化完了後に、オーバーレイ生成と自動保存コンポーネントを登録
[HarmonyPatch(typeof(Game), "FinalizeInit")]
public static class Game_FinalizeInit_Patch
{
    static void Postfix()
    {
        ChatOverlay_Boot.CreateOverlayWindow();

        if (Current.Game != null && Current.Game.GetComponent<ChatOverlay_AutoSaveComponent>() == null)
        {
            Current.Game.components.Add(new ChatOverlay_AutoSaveComponent(Current.Game));
        }
    }
}

// アプリ終了時にも最終的な位置/サイズを保存
[HarmonyPatch(typeof(Root), "Shutdown")]
public static class Root_Shutdown_Patch
{
    static void Prefix()
    {
        ChatOverlay_Boot.TrySaveOverlayRect(force: true);
    }
}

internal static class ChatOverlayConstants
{
    internal const int AutoSaveTickInterval = 300; // 約5秒
    internal const int MaxChatLines = 200;
    internal const int DuplicateToleranceTicks = 60;
    internal const float BottomThreshold = 6f;
    internal const float RectComparisonTolerance = 0.5f;
}