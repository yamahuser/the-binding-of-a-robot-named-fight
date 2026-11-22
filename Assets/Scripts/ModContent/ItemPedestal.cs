using UnityEngine;

// [MODCONTENT] Puts a pedestal under an item pickup: finds the floor below it, stands
// the pedestal there and rests the item on top.
public static class ItemPedestal
{
    public const float PedestalHeight = 12f / 16f;
    public const float ItemLift = 0.6f; // pickup sprite is 16px, pivot centred

    private static Sprite _sprite;
    private static Material _material;

    public static bool IsModItem(MajorItem item)
    {
        var v = (int)item;
        return (v >= 240 && v < 260) || (v >= 360 && v < 400);
    }

    public static float FindGround(Vector3 from, float maxDistance = 8f)
    {
        var hit = Physics2D.Raycast(from, Vector2.down, maxDistance, LayerMask.GetMask("Default", "DefaultOnly"));
        return hit.collider ? hit.point.y : from.y - 1.2f;
    }

    /// <summary>Stand a pedestal on the floor under the pickup and move the pickup onto it.</summary>
    public static GameObject PlaceUnder(GameObject pickup)
    {
        if (!pickup) { return null; }
        var pos = pickup.transform.position;
        var groundY = FindGround(pos);
        var pedestal = Create(new Vector3(pos.x, groundY, pos.z), pickup.transform.parent, pickup.GetComponent<SpriteRenderer>());
        pickup.transform.position = new Vector3(pos.x, groundY + PedestalHeight + ItemLift, pos.z);
        return pedestal;
    }

    public static GameObject Create(Vector3 groundPosition, Transform parent, SpriteRenderer sortLike = null)
    {
        if (!_sprite) { _sprite = Resources.Load<Sprite>("ModContent/ItemPedestal"); }
        if (!_material)
        {
            var shader = Shader.Find("Sprites/DefaultFlash");
            if (shader) { _material = new Material(shader); }
        }

        var go = new GameObject("ItemPedestal");
        go.transform.SetParent(parent, true);
        go.transform.position = groundPosition;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _sprite;
        if (_material) { sr.sharedMaterial = _material; }
        if (sortLike)
        {
            sr.sortingLayerID = sortLike.sortingLayerID;
            sr.sortingOrder = sortLike.sortingOrder - 1;
        }
        return go;
    }
}
