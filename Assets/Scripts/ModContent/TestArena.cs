using UnityEngine;

// [MODCONTENT] A big empty test room built at runtime far outside the level grid.
// Enter/Leave warps the player and swaps the camera limits; leaving puts you back
// exactly where you were. Nothing here touches the layout or the save.
public static class TestArena
{
    public const float Width = 112f;   // ~4.7 screens wide
    public const float Height = 42f;   // ~3 screens tall
    public const float WallThickness = 3f;
    private static readonly Vector2 Origin = new Vector2(-3000f, -3000f); // interior bottom-left

    private static GameObject _root;
    private static Vector3 _returnPosition;
    private static Bounds _returnLimits;
    private static bool _returnLimitX, _returnLimitY;
    private static Vector3 _savedPlayerStart;
    private static Vector2 _savedLastRoom;
    private static bool _haveSavedStart;

    public static bool inside { get { return _root != null; } }
    public static Transform root { get { return _root ? _root.transform : null; } }
    public static Vector3 Center { get { return new Vector3(Origin.x + Width / 2f, Origin.y + Height / 2f, 0); } }

    public static void Toggle(Player player)
    {
        if (inside) Leave(player); else Enter(player);
    }

    public static void Enter(Player player)
    {
        if (inside || !player) { return; }

        var cam = player.mainCamera ? player.mainCamera : MainCamera.instance;
        _returnPosition = player.transform.position;
        if (cam)
        {
            _returnLimitX = cam.limitCameraMovementX;
            _returnLimitY = cam.limitCameraMovementY;
            _returnLimits = new Bounds();
            _returnLimits.SetMinMax(new Vector3(cam.limitLeft, cam.limitBottom), new Vector3(cam.limitRight, cam.limitTop));
        }

        var game = SaveGameManager.activeGame;
        _haveSavedStart = game != null;
        if (game != null) { _savedPlayerStart = game.playerStart; _savedLastRoom = new Vector2(game.lastRoom.x, game.lastRoom.y); }

        Build(player.transform.position.z);

        var spawn = new Vector3(Origin.x + Width / 2f - 10f, Origin.y + 1.5f, player.transform.position.z);
        player.transform.position = spawn;

        if (cam)
        {
            var b = new Bounds();
            b.SetMinMax(new Vector3(Origin.x - 1, Origin.y - 1), new Vector3(Origin.x + Width + 1, Origin.y + Height + 1));
            cam.SetLimits(b, false);
            cam.SnapCamera(spawn);
        }

        // One dummy to shoot at, on the floor to the right of the spawn.
        DpsDummy.Spawn(new Vector3(spawn.x + 10f, Origin.y, 0), _root.transform);
        Debug.Log("[MODCONTENT] Entered test arena");
    }

    public static void Leave(Player player)
    {
        if (!inside) { return; }
        ProtectSave();
        Object.Destroy(_root);
        _root = null;
        if (!player) { return; }

        player.transform.position = _returnPosition;
        var cam = player.mainCamera ? player.mainCamera : MainCamera.instance;
        if (cam)
        {
            cam.SetLimits(_returnLimits, false);
            cam.limitCameraMovementX = _returnLimitX;
            cam.limitCameraMovementY = _returnLimitY;
            cam.SnapCamera(_returnPosition);
        }
        Debug.Log("[MODCONTENT] Left test arena");
    }

    /// <summary>Called every frame by the test bench: if the player left the arena some other
    /// way (death respawn, teleporter), tear it down without moving them.</summary>
    public static void CheckAbandoned(Player player)
    {
        if (!inside || !player) { return; }
        ProtectSave();
        var p = player.transform.position;
        var far = p.x < Origin.x - 20 || p.x > Origin.x + Width + 20 || p.y < Origin.y - 20 || p.y > Origin.y + Height + 20;
        if (far)
        {
            Object.Destroy(_root);
            _root = null;
            Debug.Log("[MODCONTENT] Test arena discarded (player left it)");
        }
    }

    /// <summary>Never let the save's respawn point end up inside the arena.</summary>
    private static void ProtectSave()
    {
        var game = SaveGameManager.activeGame;
        if (!_haveSavedStart || game == null) { return; }
        if (game.playerStart.x < Origin.x + Width + 100 && game.playerStart.y < Origin.y + Height + 100)
        {
            game.playerStart = _savedPlayerStart;
            game.lastRoom = new Int2D((int)_savedLastRoom.x, (int)_savedLastRoom.y);
        }
    }

    private static void Build(float z)
    {
        _root = new GameObject("ModContent TestArena");
        _root.transform.position = new Vector3(0, 0, z);

        var tile = Resources.Load<Sprite>("ModContent/ArenaTile");
        var backdrop = Resources.Load<Sprite>("ModContent/ArenaBackdrop");
        var shader = Shader.Find("Sprites/DefaultFlash");
        var mat = shader ? new Material(shader) : null;

        // Backdrop
        var bg = MakeSprite("Backdrop", backdrop, mat, new Vector2(Center.x, Center.y),
            new Vector2(Width + WallThickness * 2, Height + WallThickness * 2), "BehindTiles", -1000);
        bg.transform.SetParent(_root.transform, true);

        float t = WallThickness;
        // Floor, ceiling, left wall, right wall (solid, Default layer like level terrain)
        Solid("Floor", tile, mat, new Rect(Origin.x - t, Origin.y - t, Width + t * 2, t));
        Solid("Ceiling", tile, mat, new Rect(Origin.x - t, Origin.y + Height, Width + t * 2, t));
        Solid("WallLeft", tile, mat, new Rect(Origin.x - t, Origin.y, t, Height));
        Solid("WallRight", tile, mat, new Rect(Origin.x + Width, Origin.y, t, Height));
    }

    private static void Solid(string name, Sprite sprite, Material mat, Rect r)
    {
        var go = MakeSprite(name, sprite, mat, r.center, r.size, "Default", 10);
        go.layer = LayerMask.NameToLayer("Default");
        var box = go.AddComponent<BoxCollider2D>();
        box.size = r.size;
        go.transform.SetParent(_root.transform, true);
    }

    private static GameObject MakeSprite(string name, Sprite sprite, Material mat, Vector2 center, Vector2 size, string sortingLayer, int order)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(center.x, center.y, _root.transform.position.z);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (mat) { sr.sharedMaterial = mat; }
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = size;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = order;
        return go;
    }
}
