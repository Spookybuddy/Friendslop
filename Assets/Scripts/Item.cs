using UnityEngine;

public class Item : MonoBehaviour
{
    public ItemObject item;
    public Rigidbody rig;
    public bool isFalling;
    private float airtime;
    private const float GRAV = 9;
    public bool isHeld;
    public bool asleep;

    // Start is called before the first frame update
    void Start()
    {
        if (Physics.SphereCast(transform.position, 0.2f, Vector3.down, out RaycastHit hit, 0.3f)) Gravity(false);
        else Gravity(true);
    }

    private void Update()
    {
        if (rig != null) asleep = rig.IsSleeping();

        //Gravity curve
        if (isFalling) {
            if (Physics.SphereCast(transform.position, 0.2f, Vector3.down, out RaycastHit hit, 0.3f)) {
                isFalling = false;
                airtime = 0;
                transform.position = hit.point + 0.25f * Vector3.up;
            } else {
                transform.position += item.gravityCurve.Evaluate(airtime) * GRAV * Time.deltaTime * Vector3.down;
                airtime += Time.deltaTime;
            }
        }
    }

    public void Gravity(bool on)
    {
        if (rig == null) isFalling = on;
        else if (on) rig.WakeUp();
        else rig.Sleep();
    }

    public void Grab()
    {
        if (isHeld) return;

        isHeld = true;
        Debug.Log($"Grabbed {gameObject.name}");
    }
}