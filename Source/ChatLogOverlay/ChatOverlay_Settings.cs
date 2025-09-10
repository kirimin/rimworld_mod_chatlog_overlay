using System.Collections.Generic;
using UnityEngine;
using Verse;

public enum ChatOverlayFilterMode
{
    Off,
    Whitelist,
    Blacklist
}

public enum ChatOverlayDisplayLayer
{
    Standard,
    Background
}

public enum SpeakerNameFormat
{
    Japanese,
    Square,
    Parentheses,
    Angle,
    Colon
}

public enum ChatFontSize
{
    Tiny,
    Small,
    Medium
}

public class ChatOverlaySettings : ModSettings
{
    public ChatOverlayFilterMode Mode = ChatOverlayFilterMode.Off;
    public HashSet<string> PackageIdSet = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    public HashSet<string> DefNameSet = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    public HashSet<string> SpeakerNameSet = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    public float OverlayX = -1f;
    public float OverlayY = -1f;
    public float OverlayW = -1f;
    public float OverlayH = -1f;
    public float BackgroundOpacity = 0.35f;
    public ChatOverlayDisplayLayer DisplayLayer = ChatOverlayDisplayLayer.Standard;
    public bool ShowSpeakerName = true;
    public SpeakerNameFormat NameFormat = SpeakerNameFormat.Japanese;
    public bool EnableSpeakerFilter = false;
    
    public ChatFontSize FontSize = ChatFontSize.Small;
    public float TextColorR = 1.0f;
    public float TextColorG = 1.0f;
    public float TextColorB = 1.0f;
    public float TextColorA = 1.0f;

    private List<string> _pkgTmp;
    private List<string> _defTmp;
    private List<string> _speakerTmp;

    public bool HasValidOverlayRect => OverlayX >= 0f && OverlayY >= 0f && OverlayW > 0f && OverlayH > 0f;
    
    public Color TextColor
    {
        get => new Color(TextColorR, TextColorG, TextColorB, TextColorA);
        set
        {
            TextColorR = value.r;
            TextColorG = value.g;
            TextColorB = value.b;
            TextColorA = value.a;
        }
    }
    
    public GameFont GetGameFont()
    {
        switch (FontSize)
        {
            case ChatFontSize.Tiny:
                return GameFont.Tiny;
            case ChatFontSize.Small:
                return GameFont.Small;
            case ChatFontSize.Medium:
                return GameFont.Medium;
            default:
                return GameFont.Small;
        }
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Mode.GetHashCode();
            hash = hash * 31 + BackgroundOpacity.GetHashCode();
            hash = hash * 31 + DisplayLayer.GetHashCode();
            hash = hash * 31 + ShowSpeakerName.GetHashCode();
            hash = hash * 31 + NameFormat.GetHashCode();
            hash = hash * 31 + EnableSpeakerFilter.GetHashCode();
            hash = hash * 31 + FontSize.GetHashCode();
            hash = hash * 31 + TextColorR.GetHashCode();
            hash = hash * 31 + TextColorG.GetHashCode();
            hash = hash * 31 + TextColorB.GetHashCode();
            hash = hash * 31 + TextColorA.GetHashCode();
            return hash;
        }
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref Mode, "Mode", ChatOverlayFilterMode.Off);
        Scribe_Values.Look(ref OverlayX, "OverlayX", -1f);
        Scribe_Values.Look(ref OverlayY, "OverlayY", -1f);
        Scribe_Values.Look(ref OverlayW, "OverlayW", -1f);
        Scribe_Values.Look(ref OverlayH, "OverlayH", -1f);
        Scribe_Values.Look(ref BackgroundOpacity, "BackgroundOpacity", 0.35f);
        Scribe_Values.Look(ref DisplayLayer, "DisplayLayer", ChatOverlayDisplayLayer.Standard);
        Scribe_Values.Look(ref ShowSpeakerName, "ShowSpeakerName", true);
        Scribe_Values.Look(ref NameFormat, "NameFormat", SpeakerNameFormat.Japanese);
        Scribe_Values.Look(ref EnableSpeakerFilter, "EnableSpeakerFilter", false);
        Scribe_Values.Look(ref FontSize, "FontSize", ChatFontSize.Small);
        Scribe_Values.Look(ref TextColorR, "TextColorR", 1.0f);
        Scribe_Values.Look(ref TextColorG, "TextColorG", 1.0f);
        Scribe_Values.Look(ref TextColorB, "TextColorB", 1.0f);
        Scribe_Values.Look(ref TextColorA, "TextColorA", 1.0f);

        ExposeHashSets();
    }

    private void ExposeHashSets()
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _pkgTmp = new List<string>(PackageIdSet);
            _defTmp = new List<string>(DefNameSet);
            _speakerTmp = new List<string>(SpeakerNameSet);
        }

        Scribe_Collections.Look(ref _pkgTmp, "PackageIds", LookMode.Value);
        Scribe_Collections.Look(ref _defTmp, "DefNames", LookMode.Value);
        Scribe_Collections.Look(ref _speakerTmp, "SpeakerNames", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            RestoreHashSets();
        }
    }

    private void RestoreHashSets()
    {
        PackageIdSet.Clear();
        DefNameSet.Clear();
        SpeakerNameSet.Clear();

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

        if (_speakerTmp != null)
        {
            foreach (var s in _speakerTmp)
                if (!string.IsNullOrEmpty(s))
                    SpeakerNameSet.Add(s);
        }

        _pkgTmp = null;
        _defTmp = null;
        _speakerTmp = null;
    }
}