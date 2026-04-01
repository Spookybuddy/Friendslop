using UnityEngine;

public class Furniture : Prop
{
    [Header("Furniture")]
    public Decor furniture;
    public GameObject placementOutline;
    public GameObject navModifier;
    public Vector3 point;
    public bool visible;

    public override bool Grab(Transform player)
    {
        bool ret = base.Grab(player);
        if (!ret) return false;
        point = transform.position - Vector3.up * furniture.offset;
        Outline(itemStorage);
        Outline(true);
        navModifier.SetActive(false);
        transform.localPosition = furniture.holdOffset;
        transform.localEulerAngles = furniture.holdRotation;
        transform.localScale = furniture.holdScale;
        return true;
    }

    public override void Drop(Vector3 pos)
    {
        Outline(false);
        Outline(transform);
        navModifier.SetActive(true);
        base.Drop(point);
    }

    public override void Throw(Vector3 pos, Vector3 direction)
    {
        Drop(pos);
    }

    public bool MoveOutline(RaycastHit pos)
    {
        point = pos.point + furniture.offset * Vector3.up;
        placementOutline.transform.position = point;
        return pos.normal.y >= furniture.minYNormal;
    }

    private void Outline(Transform parent)
    {
        placementOutline.transform.SetParent(parent, true);
        placementOutline.transform.localPosition = Vector3.zero;
    }

    public void Outline(bool on)
    {
        visible = on;
        placementOutline.SetActive(on);
    }
}