using UnityEngine;
using UnityEngine.Events;

public class Interaction : Prop
{
    [Header("Interactable")]
    public float playerForce = 0.5f;
    public bool lockPlayer = false;
    public UnityEvent onInteract;

    //Use the item storage as the position for the player to be moved to
    public override void Start()
    {
        if (transform.childCount > 0) itemStorage = transform.GetChild(0);
    }

    //Interactable object, mainly for beds & trail selection
    public override bool Grab(Transform player)
    {
        if (isHeld) return false;

        //If transform for player, move player to that transform
        if (itemStorage != null) {
            if (!player.GetComponentInParent<PlayerController>().LockIntoPlace(this, itemStorage, playerForce)) return false;
            isHeld = true;
            onInteract.Invoke();
        }
        return false;
    }

    //Player stops interacting
    public override void Drop(Vector3 pos)
    {
        isHeld = false;
        return;
    }

    public override void Throw(Vector3 pos, Vector3 direction)
    {
        return;
    }

    public override void SnowData(float depth)
    {
        return;
    }
}