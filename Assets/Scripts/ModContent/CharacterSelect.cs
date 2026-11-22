using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Rewired;

// [MODCONTENT] Character select overlay shown between "New Game" and the run being created.
// Left/Right picks, Submit confirms, Cancel backs out. Works with the game's menu actions
// (keyboard + controller via Rewired) plus raw keyboard as a fallback.
public class CharacterSelect : MonoBehaviour
{
    private static CharacterSelect _open;
    public static bool isOpen { get { return _open != null; } }

    private Action<CharacterDef> _onConfirm;
    private Action _onCancel;
    private int _index;
    private float _openedAt;
    private Font _font;
    private Texture2D _white;
    private readonly Dictionary<string, Texture2D> _palettes = new Dictionary<string, Texture2D>();

    public static void Open(Action<CharacterDef> onConfirm, Action onCancel)
    {
        if (_open) { Destroy(_open.gameObject); }
        var go = new GameObject("CharacterSelect");
        DontDestroyOnLoad(go);
        _open = go.AddComponent<CharacterSelect>();
        _open._onConfirm = onConfirm;
        _open._onCancel = onCancel;
        _open._openedAt = Time.unscaledTime;
        var last = PlayerPrefs.GetString("ModContent.LastCharacter", CharacterRoster.DefaultId);
        _open._index = Mathf.Max(0, CharacterRoster.All.FindIndex(c => c.id == last));
    }

    private void Awake()
    {
        _font = Resources.Load<Font>("Fonts/uni0553-webfont");
        _white = new Texture2D(1, 1);
        _white.SetPixel(0, 0, Color.white);
        _white.Apply();
    }

    private void OnDestroy() { if (_open == this) _open = null; }

    private Rewired.Player Controller
    {
        get { return ReInput.players != null && ReInput.players.Count > 0 ? ReInput.players[0] : null; }
    }

    private bool Pressed(string action, Key key, Key alt = Key.None)
    {
        var c = Controller;
        if (c != null && c.GetButtonDown(action)) { return true; }
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) { return false; }
        return kb[key].wasPressedThisFrame || (alt != Key.None && kb[alt].wasPressedThisFrame);
    }

    private void Update()
    {
        // ignore the press that opened us
        if (Time.unscaledTime - _openedAt < 0.2f) { return; }

        var count = CharacterRoster.All.Count;
        if (Pressed("UILeft", Key.LeftArrow, Key.A)) { _index = (_index + count - 1) % count; UISound(false); }
        if (Pressed("UIRight", Key.RightArrow, Key.D)) { _index = (_index + 1) % count; UISound(false); }

        if (Pressed("UISubmit", Key.Enter, Key.Space))
        {
            var chosen = CharacterRoster.All[_index];
            PlayerPrefs.SetString("ModContent.LastCharacter", chosen.id);
            UISound(true);
            var cb = _onConfirm;
            Destroy(gameObject);
            if (cb != null) cb(chosen);
        }
        else if (Pressed("UICancel", Key.Escape, Key.Backspace))
        {
            if (UISounds.instance) { UISounds.instance.Cancel(); }
            var cb = _onCancel;
            Destroy(gameObject);
            if (cb != null) cb();
        }
    }

    private static void UISound(bool confirm)
    {
        if (!UISounds.instance) { return; }
        if (confirm) UISounds.instance.Confirm(); else UISounds.instance.OptionChange();
    }

    // ---------------- drawing ----------------

    private void Rect(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, _white);
        GUI.color = old;
    }

    private GUIStyle Style(int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = false)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, wordWrap = wrap };
        if (_font) { s.font = _font; }
        s.normal.textColor = color;
        return s;
    }

    private Texture2D Palette(string name)
    {
        if (string.IsNullOrEmpty(name)) { name = ""; }
        Texture2D t;
        if (!_palettes.TryGetValue(name, out t))
        {
            t = Resources.Load<Texture2D>("Palettes/Player/PlayerPalette" + name);
            if (t) { t.filterMode = FilterMode.Point; }
            _palettes[name] = t;
        }
        return t;
    }

    private void OnGUI()
    {
        GUI.depth = -1000;
        var w = Screen.width;
        var h = Screen.height;
        var u = h / 720f; // layout unit

        Rect(new Rect(0, 0, w, h), new Color(0.03f, 0.03f, 0.07f, 0.94f));

        var green = new Color(0.45f, 1f, 0.55f);
        var dim = new Color(0.6f, 0.65f, 0.75f);
        GUI.Label(new Rect(0, 30 * u, w, 60 * u), "SELECT YOUR ROBOT", Style((int)(40 * u), green, TextAnchor.MiddleCenter));

        // roster strip
        var roster = CharacterRoster.All;
        var cardW = 150 * u; var cardH = 60 * u; var gap = 12 * u;
        var totalW = roster.Count * cardW + (roster.Count - 1) * gap;
        var x0 = (w - totalW) / 2f; var y0 = 110 * u;
        for (int i = 0; i < roster.Count; i++)
        {
            var r = new Rect(x0 + i * (cardW + gap), y0, cardW, cardH);
            var sel = i == _index;
            Rect(r, sel ? new Color(0.2f, 0.55f, 0.3f) : new Color(0.12f, 0.13f, 0.2f));
            if (sel) { Rect(new Rect(r.x, r.yMax - 4 * u, r.width, 4 * u), green); }
            GUI.Label(r, roster[i].name, Style((int)(18 * u), sel ? Color.white : dim, TextAnchor.MiddleCenter));
        }

        // detail panel
        var c = roster[_index];
        var panel = new Rect(w / 2f - 480 * u, 200 * u, 960 * u, 430 * u);
        Rect(panel, new Color(0.08f, 0.09f, 0.14f));
        var px = panel.x + 30 * u; var py = panel.y + 24 * u;

        GUI.Label(new Rect(px, py, 600 * u, 50 * u), c.name, Style((int)(44 * u), Color.white));
        GUI.Label(new Rect(px, py + 52 * u, 600 * u, 30 * u), c.role.ToUpperInvariant(), Style((int)(22 * u), green));

        // palette swatch (or Samus tag)
        var swatchRect = new Rect(panel.xMax - 230 * u, py + 8 * u, 200 * u, 40 * u);
        if (c.samusSprite)
        {
            Rect(swatchRect, new Color(0.85f, 0.35f, 0.1f));
            GUI.Label(swatchRect, "SAMUS SPRITE", Style((int)(16 * u), Color.white, TextAnchor.MiddleCenter));
        }
        else
        {
            var pal = Palette(c.palette);
            if (pal) { GUI.DrawTexture(swatchRect, pal, ScaleMode.StretchToFill); }
            GUI.Label(new Rect(swatchRect.x, swatchRect.yMax + 4 * u, swatchRect.width, 24 * u),
                string.IsNullOrEmpty(c.head) ? "STANDARD LOOK" : (c.head + " HELM").ToUpperInvariant(), Style((int)(13 * u), dim, TextAnchor.MiddleCenter));
        }

        GUI.Label(new Rect(px, py + 100 * u, 520 * u, 90 * u), c.description, Style((int)(20 * u), new Color(0.9f, 0.92f, 1f), TextAnchor.UpperLeft, true));

        // stat bars
        var bars = new[] { "HEALTH", "SPEED", "DAMAGE", "FIRE RATE", "ENERGY" };
        var vals = new[] { c.barHealth, c.barSpeed, c.barDamage, c.barFireRate, c.barEnergy };
        var bx = panel.x + 590 * u; var by = py + 110 * u;
        for (int i = 0; i < bars.Length; i++)
        {
            var yy = by + i * 36 * u;
            GUI.Label(new Rect(bx, yy, 130 * u, 28 * u), bars[i], Style((int)(16 * u), dim, TextAnchor.MiddleLeft));
            for (int s = 0; s < 5; s++)
            {
                Rect(new Rect(bx + 140 * u + s * 32 * u, yy + 6 * u, 26 * u, 16 * u), s < vals[i] ? green : new Color(0.2f, 0.22f, 0.3f));
            }
        }

        // rule + kit
        var ry = py + 210 * u;
        GUI.Label(new Rect(px, ry, 110 * u, 28 * u), "STRENGTH", Style((int)(18 * u), green));
        GUI.Label(new Rect(px + 120 * u, ry, 780 * u, 50 * u), c.strength, Style((int)(18 * u), Color.white, TextAnchor.UpperLeft, true));
        GUI.Label(new Rect(px, ry + 54 * u, 110 * u, 28 * u), "FLAW", Style((int)(18 * u), new Color(1f, 0.35f, 0.35f)));
        GUI.Label(new Rect(px + 120 * u, ry + 54 * u, 780 * u, 50 * u), c.flaw, Style((int)(18 * u), new Color(1f, 0.85f, 0.85f), TextAnchor.UpperLeft, true));

        var kit = new List<string>();
        foreach (var item in c.startingItems)
        {
            MajorItemInfo info;
            kit.Add(ItemManager.items != null && ItemManager.items.TryGetValue(item, out info) ? info.fullName : item.ToString());
        }
        if (c.startingScrap > 0) { kit.Add(c.startingScrap + " scrap"); }
        GUI.Label(new Rect(px, ry + 108 * u, 110 * u, 28 * u), "KIT", Style((int)(18 * u), new Color(1f, 0.8f, 0.3f)));
        GUI.Label(new Rect(px + 120 * u, ry + 108 * u, 780 * u, 60 * u), kit.Count == 0 ? "Nothing. Find it all." : string.Join(", ", kit.ToArray()),
            Style((int)(18 * u), Color.white, TextAnchor.UpperLeft, true));

        GUI.Label(new Rect(0, h - 60 * u, w, 40 * u), "LEFT / RIGHT  choose      CONFIRM  start run      BACK  cancel",
            Style((int)(18 * u), dim, TextAnchor.MiddleCenter));
    }
}
