using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Camera")]
    public Transform head;
    private const float HeadHeight = 0.625f;

    [Header("Controls")]
    public bool paused;
    private const float jumpStartEval = 0.5f;
    private const float lookSpeed = 0.1f;
    private const float moveSpeed = 3.1f;
    private const float sprintSpeed = 1.8f;
    private const float sneakSpeed = 0.5f;
    private const float playerColliderRadius = 0.4f;
    private const float slopeLimit = 0.5f; //60 degrees
    private bool hasJumped;
    private bool risingJump;
    private bool wasLaunched;
    private float launchStunTime;
    private float jumpInputBuffer;
    private const float maxJumpBuffer = 0.2f;
    private bool isSprinting;
    private bool isSneaking;
    private float airtime;
    private bool moving;
    private Vector2 movementInput;
    private Vector3 movementDir;
    private Vector3 launchVector;
    private Vector3 slopeDir;
    private Vector3 surfaceNormals = default;
    private RaycastHit wallCollision;
    public LayerMask groundLayers;
    private const float GravitationalForce = 9.9f;
    public AnimationCurve gravityCurve;

    [Header("Inventory")]
    public Transform selectionShellObject;
    public MeshFilter selectionShellMesh;
    private bool isOutlined;
    public GameObject interactWith;
    public byte heldItemIndex;
    public Item[] inventory = new Item[5];
    public LayerMask interactLayers;
    public Transform interactIcon;

    public void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Update()
    {
        //Pause cant move
        if (paused) return;

        //Looking at raycast
        if (Physics.SphereCast(head.position, 0.05f, head.forward, out RaycastHit interact, 2.95f, interactLayers)) {
            if (!interactIcon.gameObject.activeSelf) interactIcon.gameObject.SetActive(true);
            if (interactWith == null) interactWith = interact.collider.gameObject;
            if (!isOutlined) {
                if (interact.collider.gameObject.TryGetComponent<MeshFilter>(out MeshFilter m)) {
                    isOutlined = true;
                    selectionShellMesh.mesh = m.mesh;
                    selectionShellObject.SetPositionAndRotation(interact.collider.gameObject.transform.position, interact.collider.gameObject.transform.rotation);
                    selectionShellObject.localScale = interact.collider.gameObject.transform.localScale;
                }
            }
            interactIcon.position = interact.point;
        } else {
            if (interactIcon.gameObject.activeSelf) interactIcon.gameObject.SetActive(false);
            if (interactWith != null) interactWith = null;
            if (isOutlined) {
                selectionShellMesh.mesh = null;
                isOutlined = false;
            }
        }

        //Debug launching
        if (Input.GetMouseButtonDown(1)) LaunchPlayer();

        //Launch stun
        if (launchStunTime > 0) launchStunTime -= Time.deltaTime;

        //Rising jump logic
        if (risingJump) {
            Vector3 up = GravitationalForce * gravityCurve.Evaluate(airtime) * Time.deltaTime * Vector3.up;
            if (Physics.SphereCast(transform.position, playerColliderRadius, Vector3.up, out RaycastHit roof, 1 + playerColliderRadius, groundLayers)) {
                //if (launchVector.magnitude > 0.1f) up += CollisionCheck(Vector3.ProjectOnPlane(movementDir.normalized, roof.normal) * airtime);
                up += CollisionCheck(Vector3.ProjectOnPlane(Vector3.up, roof.normal) * airtime);
                airtime *= 0.8f;
            }
            transform.position += up;
            airtime -= Time.deltaTime;
            if (airtime <= 0) {
                risingJump = false;
                airtime = 0.01f;
            }
        } else {
            //Gravity logic
            float mov = GravitationalForce * Mathf.Pow(gravityCurve.Evaluate(airtime), 2) * Time.deltaTime;
            if (Physics.SphereCast(transform.position, playerColliderRadius, Vector3.down, out RaycastHit floor, Mathf.Max(mov, 1 - playerColliderRadius), groundLayers)) {
                float dis = Vector3.Distance(floor.point, transform.position - (Vector3.up * Mathf.Max(mov, 1 - playerColliderRadius)));
                surfaceNormals = floor.normal;
                /* Sliding down slopes while standing still doesnt make much sense
                if (surfaceNormals.y < 1) {
                    //Slide down a surface
                    slopeDir = Time.deltaTime * Vector3.ProjectOnPlane(Vector3.down, surfaceNormals);
                    if (!moving && slopeDir.y < 0) transform.position += (1 - surfaceNormals.y) * Friction() * slopeDir;
                } else slopeDir = default;
                */
                if (surfaceNormals.y > slopeLimit) {
                    //Surface you can stand on
                    transform.position = new Vector3(transform.position.x, floor.point.y + Mathf.Clamp01(transform.position.y - floor.point.y) + Mathf.Clamp(playerColliderRadius - 0.01f - dis, 0, playerColliderRadius), transform.position.z);
                    hasJumped = false;
                    if (launchStunTime <= 0) wasLaunched = false;
                    airtime = 0;
                    jumpInputBuffer = Mathf.Clamp(jumpInputBuffer + Time.deltaTime, 0, maxJumpBuffer);
                } else {
                    //Normals of the surface are too steep: Start sliding & cant jump off it
                    if (jumpInputBuffer > 0) jumpInputBuffer -= Time.deltaTime * (isSprinting ? sprintSpeed : 1);
                    else {
                        jumpInputBuffer = 0;
                        hasJumped = true;
                        slopeDir = Time.deltaTime * Vector3.ProjectOnPlane(Vector3.down, surfaceNormals);
                    }
                    airtime = Mathf.Clamp(airtime + Time.deltaTime, 0.001f, 3);
                    transform.position += (2.5f + gravityCurve.Evaluate(airtime) - surfaceNormals.y) * Friction(floor.collider.tag) * slopeDir;
                }
            } else {
                if (jumpInputBuffer > 0) jumpInputBuffer -= Time.deltaTime * (isSprinting ? sprintSpeed : 1);
                else {
                    hasJumped = true;
                    jumpInputBuffer = 0;
                    slopeDir = default;
                }
                transform.position += mov * Vector3.down;
                airtime = Mathf.Clamp(airtime + Time.deltaTime, 0.001f, 3);
            }
        }

        //Movement logic
        if (moving || wasLaunched) {
            float moveMulti = moveSpeed * Time.deltaTime;
            moveMulti *= (isSprinting ? sprintSpeed : isSneaking ? sneakSpeed : 1);
            movementDir = transform.forward * (movementInput.y * moveMulti) + transform.right * (movementInput.x * moveMulti);
            if (wasLaunched) movementDir = launchVector;
            transform.position += CollisionCheck(movementDir, wasLaunched);
        }

        //Crouch head move
        if (isSneaking) {
            if (head.localPosition.y > 0.05f) head.localPosition = Vector3.Lerp(head.localPosition, Vector3.zero, Time.deltaTime * 30);
            else if (head.localPosition != Vector3.zero) head.localPosition = Vector3.zero;
        } else {
            if (head.localPosition.y < HeadHeight - 0.05f) head.localPosition = Vector3.Lerp(head.localPosition, Vector3.up * HeadHeight, Time.deltaTime * 30);
            else if (head.localPosition != Vector3.up * HeadHeight) head.localPosition = Vector3.up * HeadHeight;
        }
    }

    //Return multiplicitive value for how much a player slides on specific tagged surfaces
    private float Friction(string tag = default)
    {
        return tag switch {
            "" => 1,
            _ => 1.5f,
        };
    }

    //The multi raycast collision check
    private Vector3 CollisionCheck(Vector3 inVector, bool checkA = false)
    {
        if (Physics.SphereCast(transform.position + Vector3.down * 0.3f, playerColliderRadius, inVector, out wallCollision, Mathf.Max(inVector.magnitude - playerColliderRadius, 0.1f), groundLayers)) Debug.DrawLine(wallCollision.point, transform.position, Color.blue);
        else if (Physics.SphereCast(transform.position + Vector3.up * 0.3f, playerColliderRadius, inVector, out wallCollision, Mathf.Max(inVector.magnitude - playerColliderRadius, 0.1f), groundLayers)) Debug.DrawLine(wallCollision.point, transform.position, Color.red);
        else if (checkA) return inVector;
        else return Vector3.ProjectOnPlane(inVector, surfaceNormals) + Friction() * Mathf.Pow(1.5f - surfaceNormals.y, 2) * slopeDir;
        //When collide, raycast again with new projection before moving
        if (wallCollision.normal.y <= slopeLimit) wallCollision.normal = new Vector3(wallCollision.normal.x, 0, wallCollision.normal.z);
        Vector3 newMov = Vector3.ProjectOnPlane(inVector, wallCollision.normal);
        if (Physics.SphereCast(transform.position + Vector3.down * 0.3f, playerColliderRadius, newMov, out RaycastHit wall, Mathf.Max(newMov.magnitude - playerColliderRadius, 0.1f), groundLayers)) return default;
        else if (Physics.SphereCast(transform.position + Vector3.up * 0.3f, playerColliderRadius, newMov, out RaycastHit wall2, Mathf.Max(newMov.magnitude - playerColliderRadius, 0.1f), groundLayers)) return default;
        else return newMov;
    }

    //Launch player in given direction
    public void LaunchPlayer(Vector3 direction = default)
    {
        if (direction == default) direction = Random.onUnitSphere * 0.1f;
        Debug.DrawRay(transform.position, direction, Color.cyan, 1.5f);
        hasJumped = true;
        risingJump = true;
        wasLaunched = true;
        launchVector = direction;
        launchVector.y = Mathf.Max(launchVector.y, 0.1f);
        launchStunTime = Mathf.Clamp(launchVector.magnitude, 0.8f, 1.8f);
        airtime = launchVector.y;
    }

    #region Controls
    //Set pause to given state
    public void Pause(bool state)
    {
        paused = state;
        Cursor.lockState = (CursorLockMode)(state ? 0 : 1);
    }

    //Look rotate body and head
    public void CameraMovement(InputAction.CallbackContext ctx)
    {
        transform.Rotate(ctx.ReadValue<Vector2>().x * lookSpeed * Vector3.up);
        head.localEulerAngles = new Vector3(head.localEulerAngles.x - ctx.ReadValue<Vector2>().y * lookSpeed, 0, 0);
    }

    //Movement input
    public void Movement(InputAction.CallbackContext ctx)
    {
        if (ctx.started) moving = true;
        movementInput = ctx.ReadValue<Vector2>();
        if (ctx.canceled) moving = false;
    }

    //Jump input
    public void Jump(InputAction.CallbackContext ctx)
    {
        if (!hasJumped) {
            risingJump = true;
            hasJumped = true;
            airtime = jumpStartEval;
        }
    }

    //Run input
    public void Sprint(InputAction.CallbackContext ctx)
    {
        isSneaking = false;
        if (ctx.started) isSprinting = true;
        if (ctx.canceled) isSprinting = false;
    }
    
    //Sneak input
    public void Sneak(InputAction.CallbackContext ctx)
    {
        isSprinting = false;
        if (ctx.started) isSneaking = true;
        if (ctx.canceled) isSneaking = false;
    }

    //Grab input
    public void Grab(InputAction.CallbackContext ctx)
    {
        if (ctx.started) {
            if (interactWith != null) {
                if (interactWith.TryGetComponent<Item>(out Item script)) {
                    script.Grab();
                }
            }
        }
    }
    #endregion
}