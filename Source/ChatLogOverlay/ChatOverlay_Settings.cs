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
    public HashSet<string> PackageIdSet = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    public HashSet<string> DefNameSet = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    public float OverlayX = -1f;
    public float OverlayY = -1f;
    public float OverlayW = -1f;
    public float OverlayH = -1f;
    public float BackgroundOpacity = 0.35f;

    private List<string> _pkgTmp;
    private List<string> _defTmp;

    public bool HasValidOverlayRect => OverlayX >= 0f && OverlayY >= 0f && OverlayW > 0f && OverlayH > 0f;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref Mode, "Mode", ChatOverlayFilterMode.Off);
        Scribe_Values.Look(ref OverlayX, "OverlayX", -1f);
        Scribe_Values.Look(ref OverlayY, "OverlayY", -1f);
        Scribe_Values.Look(ref OverlayW, "OverlayW", -1f);
        Scribe_Values.Look(ref OverlayH, "OverlayH", -1f);
        Scribe_Values.Look(ref BackgroundOpacity, "BackgroundOpacity", 0.35f);

        ExposeHashSets();
    }

    private void ExposeHashSets()
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _pkgTmp = new List<string>(PackageIdSet);
            _defTmp = new List<string>(DefNameSet);
        }

        Scribe_Collections.Look(ref _pkgTmp, "PackageIds", LookMode.Value);
        Scribe_Collections.Look(ref _defTmp, "DefNames", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            RestoreHashSets();
        }
    }

    private void RestoreHashSets()
    {
        PackageIdSet.Clear();
        DefNameSet.Clear();

        if (_pkgTmp != null)
        {
            foreach (var s in _pkgTmp)
                if (!string.IsNullOrEmpty(s))
                    PackageIdSet.Add(s);
        }

        if (_defTmp != null)
        {
            foreach (var s in _defTmp)
                if (!string.IsNullOrEmpty(s))
                    DefNameSet.Add(s);
        }

        _pkgTmp = null;
        _defTmp = null;
    }
}