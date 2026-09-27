using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-down map camera (node-prefabs-camera-plan.txt, section 5).
///
/// Uses the NEW Input System package (Keyboard/Mouse), so it works with
/// Player Settings -> Active Input Handling = "Input System Package (New)".
/// (The legacy UnityEngine.Input class throws InvalidOperationException
/// when that setting is active.)
///
/// Controls (all toggleable + tunable in the Inspector):
///   PAN    WASD / arrow keys
///   PAN    hold LEFT mouse and drag
///   PAN    hold RIGHT mouse and drag
///   PAN    screen edge scroll
///   ZOOM   mouse wheel - zooms toward the cursor, clamped to Zoom Min/Max
///   ANGLE  hold the SCROLL WHEEL (middle mouse) and move the mouse up/down
///   RESET  R - re-center the camera on the map
///
/// Attach to any GameObject - it drives the main camera. GameView auto-uses
/// it when present (SetupCameraController), so nothing needs wiring.
///
/// UI-safe: while an IMGUI control has the mouse (slider drag, button press,
/// ...) the camera ignores mouse input, so interacting with the panels never
/// moves the map.
/// </summary>
[AddComponentMenu("Recource/Camera Controller")]
public class CameraController : MonoBehaviour
{
    [Header("Pan (units per second)")]
    [Tooltip("WASD / arrow keys / screen edge scroll speed.")]
    public float panSpeed = 20f;
    [Tooltip("Mouse drag: world units moved per pixel of mouse travel.")]
    public float dragSpeed = 0.05f;
    public bool enableWASD = true;
    public bool enableLeftDrag = true;
    public bool enableRightDrag = true;
    public bool enableEdgeScroll = true;
    [Tooltip("Pixels from the screen edge that start edge scrolling.")]
    public float edgeMargin = 28f;
    [Tooltip("Extra speed multiplier while edge scrolling.")]
    public float edgeScrollBoost = 1.6f;

    [Header("Zoom (camera distance)")]
    public bool enableWheelZoom = true;
    [Tooltip("Closest the camera can zoom in (world units).")]
    [Range(1f, 200f)]
    public float zoomMin = 10f;
    [Tooltip("Farthest the camera can zoom out (world units).")]
    [Range(1f, 400f)]
    public float zoomMax = 60f;
    [Tooltip("Zoom strength per wheel notch (higher = stronger).")]
    [Range(0.1f, 5f)]
    public float wheelZoomStrength = 1.6f;
    [Tooltip("Keep the point under the mouse cursor fixed while zooming.")]
    public bool zoomToCursor = true;

    [Header("Camera angle (hold scroll wheel + move mouse up/down)")]
    public bool enableAngleDrag = true;
    [Tooltip("Lowest camera angle (degrees). 20 = low, 85 = almost straight down.")]
    [Range(10f, 89f)]
    public float angleMin = 20f;
    [Range(10f, 89f)]
    public float angleMax = 85f;
    [Tooltip("Starting camera angle (degrees).")]
    [Range(10f, 89f)]
    public float angleStart = 50f;
    [Tooltip("Angle change (degrees) per pixel of mouse travel while dragging.")]
    [Range(0.05f, 1f)]
    public float angleDragSpeed = 0.25f;

    [Header("Smoothing & map bounds")]
    [Tooltip("Camera damping - higher is snappier, 0 = instant.")]
    public float smoothing = 12f;
    [Tooltip("Keep the camera inside the map extent (set by GameView).")]
    public bool clampToMap = true;
    [Tooltip("Extra padding (world units) allowed outside the map edges.")]
    public float boundsMargin = 10f;
    [Tooltip("Press R to re-center the camera on the map.")]
    public bool enableResetKey = true;

    Camera cam;

    // camera model: look at `target` from `distance` away at `angleDeg` elevation
    Vector3 target;
    float distance;
    float angleDeg;
    Vector2 bMin, bMax; // map extent: x = world X, y = world Z
    bool boundsSet;

    // smoothed values actually used to place the camera
    Vector3 sTarget;
    float sDistance;
    float sAngle;

    Vector2 lastMouse;
    bool mouseTracked;

    /// <summary>The active controller in the scene (for other systems, e.g. the node UI).</summary>
    public static CameraController Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Find an existing CameraController, or create one on the main camera.
    /// GameView calls this in Start so the component "just works".
    /// </summary>
    public static CameraController FindOrCreate()
    {
        var all = FindObjectsOfType<CameraController>();
        if (all != null && all.Length > 0) return all[0];
        return EnsureMainCamera().AddComponent<CameraController>();
    }

    static GameObject EnsureMainCamera()
    {
        var main = Camera.main;
        if (main != null) return main.gameObject;
        var all = FindObjectsOfType<Camera>();
        if (all != null && all.Length > 0) return all[0].gameObject;
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var c = go.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.10f, 0.11f, 0.13f);
        return go;
    }

    /// <summary>
    /// Give the map extent (world XZ, from the node positions) so pan/zoom
    /// stay inside the map. The first call also centers the camera on the map.
    /// </summary>
    public void SetMapBounds(Vector2 min, Vector2 max)
    {
        bMin = min;
        bMax = max;
        boundsSet = true;
        if (sDistance <= 0f)
            target = new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f);
        ClampTarget();
    }

    /// <summary>Move the camera so it looks at a world point (e.g. focus a clicked node).</summary>
    public void FocusOn(Vector3 worldPoint)
    {
        target = new Vector3(worldPoint.x, 0f, worldPoint.z);
        ClampTarget();
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }
        if (distance <= 0f)
            distance = Mathf.Clamp((zoomMin + zoomMax) * 0.5f, zoomMin, zoomMax);
        if (angleDeg <= 0f)
            angleDeg = Mathf.Clamp(angleStart, angleMin, angleMax);
        if (sDistance <= 0f)
        {
            sTarget = target;
            sDistance = distance;
            sAngle = angleDeg;
        }

        float dt = Time.deltaTime;

        // UI-safe: never fight an IMGUI control for the mouse (plan section 5)
        bool uiBusy = (GUIUtility.hotControl != 0);

        if (!uiBusy)
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            // ---- WASD / arrow keys (new Input System) ----
            if (enableWASD && keyboard != null)
            {
                bool up = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
                bool down = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
                bool left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
                bool right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
                float h = (right ? 1f : 0f) - (left ? 1f : 0f);
                float v = (up ? 1f : 0f) - (down ? 1f : 0f);
                if (h != 0f || v != 0f)
                    target += CamRight() * (h * panSpeed * dt) + CamUp() * (v * panSpeed * dt);
            }

            if (mouse != null)
            {
                // ---- mouse buttons: drag pan + middle-mouse angle ----
                Vector2 m = mouse.position.value;
                bool leftDown = mouse.leftButton.isPressed;
                bool rightDown = mouse.rightButton.isPressed;
                bool midDown = mouse.middleButton.isPressed;
                bool anyHeld = leftDown || rightDown || midDown;
                if (anyHeld)
                {
                    if (mouseTracked)
                    {
                        float dx = m.x - lastMouse.x;
                        float dy = m.y - lastMouse.y;

                        bool panDrag = (enableLeftDrag && leftDown) ||
                                       (enableRightDrag && rightDown);
                        if (panDrag)
                        {
                            // horizontal: map follows the mouse (grab)
                            // vertical: camera follows the mouse (drag down = map moves up)
                            target += (-CamRight() * dx - CamUp() * dy) * dragSpeed;
                        }

                        // hold the scroll wheel (middle mouse) + move up/down = camera angle
                        if (enableAngleDrag && midDown)
                            angleDeg = Mathf.Clamp(angleDeg + dy * angleDragSpeed, angleMin, angleMax);
                    }
                    lastMouse = m;
                    mouseTracked = true;
                }
                else
                {
                    mouseTracked = false;
                }

                // ---- screen edge scroll ----
                if (enableEdgeScroll)
                {
                    float ex = 0f, ey = 0f;
                    if (m.x > Screen.width - edgeMargin)
                        ex = (m.x - (Screen.width - edgeMargin)) / Mathf.Max(1f, edgeMargin);
                    else if (m.x < edgeMargin)
                        ex = -(m.x / Mathf.Max(1f, edgeMargin));
                    if (m.y > Screen.height - edgeMargin)
                        ey = (m.y - (Screen.height - edgeMargin)) / Mathf.Max(1f, edgeMargin);
                    else if (m.y < edgeMargin)
                        ey = -(m.y / Mathf.Max(1f, edgeMargin));
                    if (ex != 0f || ey != 0f)
                        target += (CamRight() * ex + CamUp() * ey) * (panSpeed * edgeScrollBoost * dt);
                }

                // ---- mouse wheel zoom (toward the cursor) ----
                // paused while the scroll wheel itself is held down (= angle mode)
                if (enableWheelZoom && !midDown)
                {
                    float w = mouse.scroll.value.y;
                    if (Mathf.Abs(w) > 0.0001f)
                    {
                        float oldD = distance;
                        distance = Mathf.Clamp(distance * (1f - w * wheelZoomStrength), zoomMin, zoomMax);
                        if (zoomToCursor)
                        {
                            Vector3? pivot = WorldPointUnderCursor();
                            if (pivot.HasValue)
                                target = pivot.Value + (target - pivot.Value) * (distance / oldD);
                        }
                    }
                }
            }
            else
            {
                mouseTracked = false;
            }

            // ---- R = reset view ----
            if (enableResetKey && keyboard != null && keyboard.rKey.wasPressedThisFrame && boundsSet)
            {
                target = new Vector3((bMin.x + bMax.x) * 0.5f, 0f, (bMin.y + bMax.y) * 0.5f);
                distance = Mathf.Clamp((zoomMin + zoomMax) * 0.5f, zoomMin, zoomMax);
                angleDeg = Mathf.Clamp(angleStart, angleMin, angleMax);
            }
        }

        ClampTarget();

        // ---- smooth toward the goal, then place the camera ----
        float k = (smoothing <= 0f) ? 1f : 1f - Mathf.Exp(-smoothing * dt);
        sTarget = Vector3.Lerp(sTarget, target, k);
        sDistance = Mathf.Lerp(sDistance, distance, k);
        sAngle = Mathf.Lerp(sAngle, angleDeg, k);

        float a = sAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(0f, Mathf.Sin(a), -Mathf.Cos(a)) * sDistance;
        cam.transform.position = sTarget + offset;
        cam.transform.LookAt(sTarget);
    }

    /// <summary>World direction that appears as "screen right" (projected onto the map plane).</summary>
    Vector3 CamRight()
    {
        Vector3 r = cam.transform.right;
        r.y = 0f;
        return (r.sqrMagnitude > 1e-8f) ? r.normalized : new Vector3(-1f, 0f, 0f);
    }

    /// <summary>World direction that appears as "screen up" (projected onto the map plane).</summary>
    Vector3 CamUp()
    {
        Vector3 u = cam.transform.up;
        u.y = 0f;
        return (u.sqrMagnitude > 1e-8f) ? u.normalized : new Vector3(0f, 0f, 1f);
    }

    void ClampTarget()
    {
        if (!clampToMap || !boundsSet) return;
        target.x = Mathf.Clamp(target.x, bMin.x - boundsMargin, bMax.x + boundsMargin);
        target.z = Mathf.Clamp(target.z, bMin.y - boundsMargin, bMax.y + boundsMargin);
        target.y = 0f;
    }

    /// <summary>The point on the map plane (y = 0) under the mouse cursor, if any.</summary>
    Vector3? WorldPointUnderCursor()
    {
        var mouse = Mouse.current;
        if (mouse == null) return null;
        Ray ray = cam.ScreenPointToRay(mouse.position.value);
        if (Mathf.Abs(ray.direction.y) < 1e-5f) return null;
        float t = -cam.transform.position.y / ray.direction.y;
        if (t < 0f) return null;
        return cam.transform.position + ray.direction * t;
    }
}
