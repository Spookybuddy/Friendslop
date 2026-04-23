using UnityEngine;
using UnityEngine.Events;

public class Interaction : Prop
{
    [Header("Interactable")]
    public float playerForce = 0.5f;
    public bool lockPlayer = false;
    public bool snapIntoPlace = false;
    public Vector2 threshold;
    public UnityEvent onInteract;
    public UnityEvent onCancel;
    public UnityEvent onMoveSide;
    public UnityEvent onMoveUp;
    public UnityEvent onJump;

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
            if (!player.GetComponentInParent<PlayerController>().LockIntoPlace(this, itemStorage)) return false;
            isHeld = true;
            onInteract.Invoke();
        }
        return false;
    }

    //Player stops interacting
    public override void Drop(Vector3 pos)
    {
        isHeld = false;
        onCancel.Invoke();
        return;
    }

    public void MoveInput(Vector2 mov)
    {

    }

    public void JumpInput()
    {
        onJump.Invoke();
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