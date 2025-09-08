using HarmonyLib;
using System.Linq;
using UnityEngine;
using Verse;

// 複数の初期化方法を試す
[StaticConstructorOnStartup]
public static class ChatOverlay_Boot
{
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
            Log.Message("[ChatOverlay] Boot initialized successfully");
        }
        catch (System.Exception ex)
        {
            Log.Error($"[ChatOverlay] Failed to initialize Boot: {ex}");
        }
    }

    // 位置/サイズの保存（ChatOverlayRendererから呼び出される）
    public static void TrySaveOverlayRect(Rect rect, bool force = false)
    {
        var settings = ChatOverlayMod.Settings;
        if (settings == null) return;

        if (!force && ShouldSkipSave(rect)) return;

        SaveRect(settings, rect);
        _lastSavedRect = rect;
    }

    private static bool ShouldSkipSave(Rect rect)
    {
        return _lastSavedRect.HasValue && 
               ApproximatelyEqual(_lastSavedRect.Value, rect);
    }

    private static void SaveRect(ChatOverlaySettings settings, Rect rect)
    {
        settings.OverlayX = rect.x;
        settings.OverlayY = rect.y;
        settings.OverlayW = rect.width;
        settings.OverlayH = rect.height;
        settings.Write();
    }

    private static bool ApproximatelyEqual(Rect a, Rect b)
    {
        const float tolerance = ChatOverlayConstants.RectComparisonTolerance;
        return Mathf.Abs(a.x - b.x) < tolerance &&
               Mathf.Abs(a.y - b.y) < tolerance &&
               Mathf.Abs(a.width - b.width) < tolerance &&
               Mathf.Abs(a.height - b.height) < tolerance;
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
        if (now - _lastSaveTick < ChatOverlayConstants.AutoSaveTickInterval) 
            return;

        _lastSaveTick = now;
        ChatOverlay_Boot.TrySaveOverlayRect(ChatOverlayRenderer.GetCurrentRect(), force: false);
    }
}

// Harmonyパッチは ChatOverlay_Mod.cs に移動
internal static class ChatOverlayConstants
{
    internal const int AutoSaveTickInterval = 300; // 約5秒
    internal const int MaxChatLines = 200;
    internal const int DuplicateToleranceTicks = 60;
    internal const float BottomThreshold = 6f;
    internal const float RectComparisonTolerance = 0.5f;
}