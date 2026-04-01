using UnityEngine;

public class Item : Prop
{
    [Tooltip("Scriptable object")]
    public ItemObject item;
    [Tooltip("Rigidbody provided for physics. If not provided item will default to the gravity curve")]
    public Rigidbody rig;
    [Tooltip("Radius of spherecast when detecting ground collision")]
    public float sphereCastRadius = 0.2f;
    [Tooltip("Distance above ground to stop at when landing")]
    public float groundOffset = 0.2f;
    private bool isFalling;
    private float airtime;
    private const float GRAV = 9;
    private float snowDepth = 0;
    private const float SnowCarve = 50;
    public bool isThrown = false;

    // Start is called before the first frame update
    public override void Start()
    {
        base.Start();
        PhysicsStart();
    }

    private void Update()
    {
        if (isHeld) return;

        //Gravity curve
        if (isFalling) {
            if (Physics.SphereCast(transform.position, sphereCastRadius, Vector3.down, out RaycastHit hit, 0.1f + groundOffset * Time.deltaTime, 129)) {
                isFalling = false;
                airtime = 0;
                transform.position = hit.point + groundOffset * Vector3.up;
            } else {
                transform.position += item.gravityCurve.Evaluate(airtime) * GRAV * Time.deltaTime * Vector3.down;
                airtime += Time.deltaTime;
            }
        }

        //Stop moving
        if (rig == null) return;
        if (rig.IsSleeping()) {
            isThrown = false;
            return;
        }

        //Snow
        if (snowDepth > 0 && rig != null) {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit snow, snowDepth, 512)) {
                if (snow.collider.gameObject.TryGetComponent<SnowySurface>(out SnowySurface script)) {
                    float reduce = script.Carve(snow.triangleIndex * 3, rig.velocity.magnitude * SnowCarve * Time.deltaTime * snow.barycentricCoordinate);
                    //rig.velocity = Vector3.Lerp(rig.velocity, Vector3.zero, Time.deltaTime * reduce);
                    reduce = Time.deltaTime * reduce * SnowCarve / 10;
                    rig.AddForce(-rig.velocity * reduce, ForceMode.Impulse);
                }
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (rig.velocity.magnitude < 0.1f) return;
        if (!isThrown) return;
        if (collision.gameObject.CompareTag("Player")) {
            if (collision.gameObject.TryGetComponent<PlayerController>(out PlayerController player)) {
                player.KnockPlayer((player.transform.position - transform.position).normalized * Mathf.Clamp(item.weight, 0.001f, 5));
                Debug.Log("Bumped player");
            }
        }
    }

    private void PhysicsStart()
    {
        if (Physics.SphereCast(transform.position, sphereCastRadius, Vector3.down, out RaycastHit hit, groundOffset)) Gravity(false);
        else Gravity(true);
    }

    //Use item's preferred gravity style
    public void Gravity(bool on)
    {
        if (rig == null) isFalling = on;
        else if (on) rig.WakeUp();
        else rig.Sleep();
    }

    //Pass the snow data from player that grabbed it
    public override void SnowData(float depth)
    {
        snowDepth = depth;
    }

    //Hit item at specified point
    public void ApplyForce(Vector3 point, Vector3 force)
    {
        if (rig == null) return;
        rig.AddForceAtPosition(force, point, ForceMode.Impulse);
    }

    //Force at non specific point
    public void ApplyForce(Vector3 force)
    {
        if (rig == null) return;
        rig.AddForce(force, ForceMode.Impulse);
    }

    //Player grabs item
    public override bool Grab(Transform player)
    {
        isThrown = false;
        bool ret = base.Grab(player);
        if (!ret) return false;
        transform.localPosition = item.holdOffset;
        transform.localEulerAngles = item.holdRotation;
        transform.localScale = item.holdScale;
        if (rig != null) rig.isKinematic = true;
        return true;
    }

    //Player drops item
    public override void Drop(Vector3 pos)
    {
        isThrown = false;
        base.Drop(transform.position + pos);
        if (rig != null) rig.isKinematic = false;
        PhysicsStart();
    }

    //Player throws item with rig
    public override void Throw(Vector3 pos, Vector3 direction)
    {
        //Rigless behavior just gets dropped
        if (rig == null) {
            Drop(pos);
            return;
        }
        isThrown = true;
        base.Throw(pos, direction);
        rig.isKinematic = false;
        ApplyForce(direction);
        Gravity(true);
    }
}