using FishNet.Object;
using UnityEngine;

public class Prop : NetworkBehaviour
{
    //Base class for furniture & items
    public bool isHeld = false;
    protected Vector3 worldScale;
    protected Transform itemStorage;

    //Init
    public virtual void Start()
    {
        itemStorage = transform.parent.parent;
        worldScale = transform.localScale;
    }

    //Player grabs prop
    public virtual bool Grab(Transform player)
    {
        //Already being held
        if (isHeld) return false;
        ServerHeld(true);
        //isHeld = true;
        transform.SetParent(player, false);
        return true;
    }

    //Player drops prop
    public virtual void Drop(Vector3 pos)
    {
        ServerHeld(false);
        //isHeld = false;
        transform.SetParent(itemStorage, true);
        transform.localScale = worldScale;
        transform.position = pos;
    }

    //Player throws prop
    public virtual void Throw(Vector3 pos, Vector3 direction)
    {
        ServerHeld(false);
        //isHeld = false;
        transform.SetParent(itemStorage, true);
        transform.localScale = worldScale;
        transform.position += pos;
    }

    public virtual void SnowData(float depth)
    {

    }

    [ServerRpc(RequireOwnership = false)]
    public void ServerHeld(bool val)
    {
        ClientHeld(val);
    }

    [ObserversRpc]
    public void ClientHeld(bool val)
    {
        isHeld = val;
    }
}