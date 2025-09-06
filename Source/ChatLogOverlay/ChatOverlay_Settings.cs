using System.Collections.Generic;
using Verse;

public enum ChatOverlayFilterMode
{
    Off,
    Whitelist,
    Blacklist
}

public class ChatOverlaySettings : ModSettings
{
    public ChatOverlayFilterMode Mode = ChatOverlayFilterMode.Off;

    // チェック状態（packageId / defName）
    public HashSet<string> PackageIdSet = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    public HashSet<string> DefNameSet   = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    // オーバーレイの保存済み位置とサイズ（負値なら未保存）
    public float OverlayX = -1f;
    public float OverlayY = -1f;
    public float OverlayW = -1f;
    public float OverlayH = -1f;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref Mode, "Mode", ChatOverlayFilterMode.Off);

        // 位置/サイズ
        Scribe_Values.Look(ref OverlayX, "OverlayX", -1f);
        Scribe_Values.Look(ref OverlayY, "OverlayY", -1f);
        Scribe_Values.Look(ref OverlayW, "OverlayW", -1f);
        Scribe_Values.Look(ref OverlayH, "OverlayH", -1f);

        // HashSet は List 経由で保存/復元
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _pkgTmp = new List<string>(PackageIdSet);
            _defTmp = new List<string>(DefNameSet);
        }
        Scribe_Collections.Look(ref _pkgTmp, "PackageIds", LookMode.Value);
        Scribe_Collections.Look(ref _defTmp, "DefNames",  LookMode.Value);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            PackageIdSet.Clear();
            DefNameSet.Clear();
            if (_pkgTmp != null) foreach (var s in _pkgTmp) if (!string.IsNullOrEmpty(s)) PackageIdSet.Add(s);
            if (_defTmp != null) foreach (var s in _defTmp)  if (!string.IsNullOrEmpty(s)) DefNameSet.Add(s);
            _pkgTmp = null;
            _defTmp = null;
        }
    }

    private List<string> _pkgTmp;
    private List<string> _defTmp;
}