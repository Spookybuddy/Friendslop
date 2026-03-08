using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    [Header("Camera")]
    public Transform head;
    public Camera mainCam;
    private const float HeadHeight = 0.625f;
    public ParticleSystem[] weathersList;
    public Material weathersMaterial;
    private float weatherOffset = 0;

    [Header("Prefs")]
    public GameObject pausedScreen;
    public TMP_Dropdown resolutionDropdown;
    private Resolution[] resolutions;
    private Resolution saved;

    public TMP_Dropdown fpsDropdown;
    private readonly sbyte[] framerates = new sbyte[5] { -1, 120, 90, 60, 30};

    public Toggle vsyncToggle;
    private byte useVsync = 1;

    public UISetting fov;
    public UISetting masterVolume;
    public UISetting voiceVolume;

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
    public string lastSurface = "Default";
    public LayerMask groundLayers;
    private const float GravitationalForce = 9.9f;
    public AnimationCurve gravityCurve;
    [HideInInspector]
    public float snowDepth = 0;

    [Header("Inventory")]
    public Transform holdPosition;
    public int playerStrength = 10;
    public Transform selectionShellObject;
    public MeshFilter selectionShellMesh;
    public GameObject interactWith;
    public byte heldItemIndex;
    public Item[] inventory = new Item[5];
    private bool dropping = false;
    private float throwTimer = 0;
    public float buildupRate = 2;
    public float throwThreshold = 0.25f;
    public LayerMask interactLayers;
    public Transform interactIcon;

    public void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        //Load up fps settings
        int fps = ReadPref("Framerate");
        fpsDropdown.value = (fps < 0 ? 0 : fps);

        //Load up vsync settings
        int v = ReadPref("Vsync");
        if (v != 0) Sync(1);
        else {
            vsyncToggle.isOn = false;
            Sync(0);
        }

        //Load up fov settings
        fov.value = ReadPref("FOV");
        if (fov.value < 0) fov.value = 60;
        mainCam.fieldOfView = fov.value;
        SetSlider(fov, "FOV");

        //Load up resolution settings
        resolutions = Screen.resolutions;
        List<string> list = new List<string>();
        for (int i = 0; i < resolutions.Length; i++) list.Insert(0, resolutions[i].ToString());
        resolutionDropdown.AddOptions(list);
        double hertz = ReadResolution();
        //Debug.Log($"<color=#009999>{Display.main.systemWidth} x {Display.main.systemHeight} @ {Screen.currentResolution.refreshRateRatio}Hz</color>");
        for (int i = 0; i < resolutions.Length; i++) {
            if (resolutions[i].Equals(saved) || (resolutions[i].width.Equals(saved.width) && resolutions[i].height.Equals(saved.height) && resolutions[i].refreshRateRatio.value.Equals(hertz))) {
                resolutionDropdown.value = resolutions.Length - 1 - i;
                break;
            }
        }
        
        //Load up volume settings
        masterVolume.value = ReadPref("Master");
        if (masterVolume.value < 0) masterVolume.value = 50;
        SetSlider(masterVolume, "Master");
        voiceVolume.value = ReadPref("Voices");
        if (voiceVolume.value < 0) voiceVolume.value = 50;
        SetSlider(voiceVolume, "Voices");
    }

    public void Update()
    {
        //Pause cant move
        if (paused) return;

        //Looking at raycast
        if (Physics.SphereCast(head.position, 0.05f, head.forward, out RaycastHit interact, 2.95f, interactLayers)) {
            if (!interactIcon.gameObject.activeSelf) interactIcon.gameObject.SetActive(true);
            if (interactWith == null || interactWith != interact.collider.gameObject) interactWith = interact.collider.gameObject;
            if (interact.collider.gameObject.TryGetComponent<MeshFilter>(out MeshFilter m)) {
                if (selectionShellMesh.mesh != m.mesh) {
                    selectionShellMesh.mesh = m.mesh;
                    selectionShellObject.SetParent(interact.collider.transform, false);
                }
            }
            interactIcon.position = interact.point;
        } else {
            if (interactIcon.gameObject.activeSelf) interactIcon.gameObject.SetActive(false);
            if (interactWith != null) interactWith = null;
            if (selectionShellMesh.mesh != null) selectionShellMesh.mesh = null;
        }

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

                    //Get tag
                    lastSurface = floor.collider.tag;
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

        //Throwing
        if (dropping) throwTimer = Mathf.Clamp(throwTimer + Time.deltaTime * buildupRate, 0, buildupRate * 2);

        //Movement logic
        if (moving || wasLaunched) {
            float moveMulti = moveSpeed * Time.deltaTime;
            moveMulti *= (isSprinting ? sprintSpeed : isSneaking ? sneakSpeed : 1);
            moveMulti *= (dropping ? 0.75f : 1);

            //Snow
            if (snowDepth > 0) {
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit snow, snowDepth, 512)) {
                    if (snow.collider.gameObject.TryGetComponent<SnowySurface>(out SnowySurface script)) {
                        moveMulti *= Mathf.Clamp(1 - script.Carve(snow.triangleIndex * 3, 90 * Time.deltaTime * snow.barycentricCoordinate), 0.5f, 0.9f) + 0.1f;
                    }
                }
            }

            movementDir = transform.forward * (movementInput.y * moveMulti) + transform.right * (movementInput.x * moveMulti);
            if (wasLaunched) movementDir = launchVector;
            movementDir = CollisionCheck(movementDir, wasLaunched);
            //transform.position += CollisionCheck(movementDir, wasLaunched);
            float dot = Vector3.Dot(transform.right, movementDir.normalized);
            if (dot > 0.1f || dot < -0.1f) ScrollWeather(dot * movementDir.magnitude * moveSpeed * 2.5f);
            transform.position += movementDir;
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
            "Snowy" => 2,
            _ => 1.5f,
        };
    }

    //The multi raycast collision check
    private Vector3 CollisionCheck(Vector3 inVector, bool checkA = false)
    {
        if (Physics.SphereCast(transform.position + Vector3.down * 0.3f, playerColliderRadius, inVector, out wallCollision, Mathf.Max(inVector.magnitude - playerColliderRadius, 0.1f), groundLayers)) Debug.DrawLine(wallCollision.point, transform.position, Color.blue);
        else if (Physics.SphereCast(transform.position + Vector3.up * 0.3f, playerColliderRadius, inVector, out wallCollision, Mathf.Max(inVector.magnitude - playerColliderRadius, 0.1f), groundLayers)) Debug.DrawLine(wallCollision.point, transform.position, Color.red);
        else if (checkA) return inVector;
        //else return Vector3.ProjectOnPlane(inVector, surfaceNormals) + Friction() * Mathf.Pow(1.5f - surfaceNormals.y, 2) * slopeDir;
        else return Vector3.ProjectOnPlane(inVector, surfaceNormals) + Mathf.Pow(1.5f - surfaceNormals.y, 2) * slopeDir;

        PropCollisionCheck(wallCollision, inVector);

        //When collide, raycast again with new projection before moving
        if (wallCollision.normal.y <= slopeLimit) wallCollision.normal = new Vector3(wallCollision.normal.x, 0, wallCollision.normal.z);
        Vector3 newMov = Vector3.ProjectOnPlane(inVector, wallCollision.normal);
        if (Physics.SphereCast(transform.position + Vector3.down * 0.3f, playerColliderRadius, newMov, out RaycastHit wall, Mathf.Max(newMov.magnitude - playerColliderRadius, 0.1f), groundLayers)) return PropCollisionCheck(wall, newMov);
        else if (Physics.SphereCast(transform.position + Vector3.up * 0.3f, playerColliderRadius, newMov, out RaycastHit wall2, Mathf.Max(newMov.magnitude - playerColliderRadius, 0.1f), groundLayers)) return PropCollisionCheck(wall2, newMov);
        else return newMov;
    }

    //Apply movement into any props
    private Vector3 PropCollisionCheck(RaycastHit hit, Vector3 inVector)
    {
        //Prop interaction
        if (hit.collider.gameObject.TryGetComponent<Item>(out Item kicked)) {
            if (kicked.item.weight < playerStrength) {
                kicked.ApplyForce(wallCollision.point, inVector * playerStrength);
                return inVector * (1 - kicked.item.weight / (float)playerStrength);
            } else return default;
        }
        return default;
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

    //Hide and activate the model of the held item
    private bool UpdateItemHeld(byte index)
    {
        if (inventory[heldItemIndex] != null) inventory[heldItemIndex].gameObject.SetActive(false);
        heldItemIndex = index;
        if (inventory[heldItemIndex] != null) inventory[heldItemIndex].gameObject.SetActive(true);
        CancelDrop();
        return true;
    }

    //Changed item while charging drop resets drop time
    private void CancelDrop()
    {
        dropping = false;
        throwTimer = 0;
    }

    //Get/Set weather to/from manager
    public void Weather(Weathers weather)
    {
        for (byte b = 1; b <= weathersList.Length; b++) weathersList[b - 1].gameObject.SetActive(b == (byte)weather);
        string nam = $"_WEATHER_{weather.ToString().ToUpper()}";
        //Debug.Log(nam);
        weathersMaterial.EnableKeyword(nam);
    }

    //Scroll the weather shader to give the illusion of it being in world space
    private void ScrollWeather(float add)
    {
        weatherOffset += add;
        weathersMaterial.SetFloat("_XOffset", weatherOffset);
    }

    //Update snow check length based on chunks' max depth
    public void SetSnowDepth(float depth)
    {
        if (depth <= 0) snowDepth = 0;
        else snowDepth = depth + 0.1f + playerColliderRadius;
    }

    #region Settings
    //Change the resolutions
    public void ChangeResolution()
    {
        int id = resolutions.Length - 1 - resolutionDropdown.value;
        Debug.Log(resolutions[id]);
        Screen.SetResolution(resolutions[id].width, resolutions[id].height, Screen.fullScreenMode, resolutions[id].refreshRateRatio);
        SetResolution(resolutions[id]);
    }

    //Set framerate
    public void ChangeFrameRate()
    {
        Frames(fpsDropdown.value);
    }

    //Save the set frames
    private void Frames(int index)
    {
        fpsDropdown.value = index;
        Application.targetFrameRate = framerates[index];
        SetPref("Framerate", index);
    }

    //Toggle vsync
    public void ToggleVsync()
    {
        useVsync = (byte)((useVsync + 1) % 2);
        Sync(useVsync);
    }

    //Set the vsync
    private void Sync(byte sync)
    {
        useVsync = sync;
        QualitySettings.vSyncCount = sync;
        SetPref("Vsync", sync);
    }

    //Set fov
    public void ChangeFOV()
    {
        fov.value = Mathf.RoundToInt(fov.slider.value);
        mainCam.fieldOfView = fov.value;
        SetSlider(fov, "FOV");
    }

    //Set all volume
    public void ChangeVolume()
    {
        masterVolume.value = Mathf.RoundToInt(masterVolume.slider.value);
        SetSlider(masterVolume, "Master");
    }

    //Set voice volume
    public void ChangeVoices()
    {
        voiceVolume.value = Mathf.RoundToInt(voiceVolume.slider.value);
        SetSlider(voiceVolume, "Voices");
    }

    //update a slider
    private void SetSlider(UISetting settings, string key)
    {
        settings.slider.value = settings.value;
        settings.text.text = $"{key}: {settings.value}";
        SetPref(key, settings.value);
    }

    //Returns value of key
    private int ReadPref(string key)
    {
        if (PlayerPrefs.HasKey(key)) return PlayerPrefs.GetInt(key);
        return -1;
    }

    //Save value of key
    private void SetPref(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
    }

    private double ReadResolution()
    {
        if (PlayerPrefs.HasKey("Resolution")) {
            string[] values = PlayerPrefs.GetString("Resolution").Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            saved = new Resolution();
            saved.width = int.Parse(values[0]);
            saved.height = int.Parse(values[1]);
            return double.Parse(values[2]);
        } else saved = Screen.currentResolution;
        return -1;
    }

    //Saves resolution as WIDTH, HEIGHT, HZ
    private void SetResolution(Resolution current)
    {
        PlayerPrefs.SetString("Resolution", $"{current.width}, {current.height}, {current.refreshRateRatio.value}");
    }
    #endregion

    #region Controls
    //Set pause to given state
    public void Pause(bool state)
    {
        paused = !paused;
        pausedScreen.SetActive(paused);
        //paused = state;
        Cursor.lockState = (CursorLockMode)(paused ? 0 : 1);
    }

    //Look rotate body and head
    public void CameraMovement(InputAction.CallbackContext ctx)
    {
        if (paused) return;
        float x = ctx.ReadValue<Vector2>().x * lookSpeed;
        transform.Rotate(x * Vector3.up);
        ScrollWeather(x);
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
                //Check if player can actually hold an item
                bool grab = false;
                if (inventory[heldItemIndex] != null) {
                    for (byte i = 0; i < inventory.Length; i++) {
                        if (inventory[(heldItemIndex + i + inventory.Length) % inventory.Length] != null) continue;
                        grab = UpdateItemHeld((byte)((heldItemIndex + i + inventory.Length) % inventory.Length));
                        break;
                    }
                } else grab = true;

                //Grab item
                if (grab) {
                    if (interactWith.TryGetComponent<Item>(out Item script)) {
                        inventory[heldItemIndex] = script;
                        script.Grab(holdPosition);
                    }
                }
            }
        }
    }

    //Drop input
    public void Drop(InputAction.CallbackContext ctx)
    {
        //If holding item, start building throw charge to be applied on release of button
        if (ctx.started && inventory[heldItemIndex] != null) dropping = true;
        if (ctx.canceled && dropping && inventory[heldItemIndex] != null) {
            inventory[heldItemIndex].SnowData(snowDepth - playerColliderRadius);
            if (throwTimer > throwThreshold) inventory[heldItemIndex].Throw(transform.forward, playerStrength * throwTimer * head.forward);
            else inventory[heldItemIndex].Drop(transform.forward);
            inventory[heldItemIndex] = null;
            CancelDrop();
        }
    }

    //Scroll inventory
    public void Scroll(InputAction.CallbackContext ctx)
    {
        sbyte scroll = (sbyte)(Mathf.Clamp(ctx.ReadValue<Vector2>().y, -1, 1));
        UpdateItemHeld((byte)((scroll + inventory.Length + heldItemIndex) % inventory.Length));
    }

    //Hotkey
    public bool Hotkey(InputAction.CallbackContext ctx)
    {
        Vector3 input = ctx.ReadValue<Vector3>();
        if (input.y > 0) return UpdateItemHeld(0);
        if (input.y < 0) return UpdateItemHeld(1);
        if (input.x > 0) return UpdateItemHeld(2);
        if (input.x < 0) return UpdateItemHeld(3);
        if (input.z > 0) return UpdateItemHeld(4);
        //if (input.z < 0) return UpdateItemHeld(5);
        return false;
    }
    #endregion
}

[System.Serializable]
public struct UISetting
{
    public Slider slider;
    public TextMeshProUGUI text;
    public int value;
}