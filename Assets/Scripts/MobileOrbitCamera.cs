using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class MobileOrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Orbit")]
    public float distance = 12f;
    public float minDistance = 6f;
    public float maxDistance = 30f;
    public float rotationSpeed = 0.15f;
    public float minVerticalAngle = -20f;
    public float maxVerticalAngle = 75f;
    public bool allowFullVerticalOrbit = true;

    [Header("Zoom")]
    public float pinchZoomSpeed = 0.02f;
    public float scrollZoomSpeed = 6f;
    public float keyboardZoomSpeed = 10f;

    [Header("Transition")]
    public float transitionSmoothTime = 0.25f;

    [Header("Clipping")]
    public float inspectNearClipPlane = 0.001f;

    private float yaw;
    private float pitch = 10f;
    private Vector3 lastMousePosition;
    private Transform defaultTarget;
    private float defaultDistance;
    private float defaultMinDistance;
    private float defaultMaxDistance;
    private float defaultYaw;
    private float defaultPitch;
    private bool inspectMode;
    private Transform inspectBody;
    private float currentDistance;
    private float distanceVelocity;
    private Vector3 currentTargetPosition;
    private Vector3 targetPositionVelocity;
    private Camera cachedCamera;
    private float defaultNearClipPlane;
    private Quaternion orbitRotation;
    private Quaternion currentOrbitRotation;
    private Quaternion inspectStartRotation;
    private float inspectStartDistance;

    private void Start()
    {
        if (target == null)
        {
            return;
        }

        cachedCamera = GetComponent<Camera>();

        if (cachedCamera != null)
        {
            defaultNearClipPlane = cachedCamera.nearClipPlane;
        }

        Vector3 offset = transform.position - target.position;
        distance = Mathf.Clamp(offset.magnitude, minDistance, maxDistance);

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = NormalizeAngle(angles.x);
        pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);

        defaultTarget = target;
        defaultDistance = distance;
        defaultMinDistance = minDistance;
        defaultMaxDistance = maxDistance;
        defaultYaw = yaw;
        defaultPitch = pitch;
        currentDistance = distance;
        currentTargetPosition = target.position;
        orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        currentOrbitRotation = orbitRotation;

        UpdateCameraPosition();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        HandleRotationInput();
        HandleZoomInput();
        UpdateCameraPosition();
    }

    private void HandleRotationInput()
    {
        if (TryHandleTouchRotation())
        {
            ClampPitchIfNeeded();
            return;
        }

        HandleMouseRotation();

        ClampPitchIfNeeded();
    }

    private void HandleZoomInput()
    {
        if (TryHandleTouchZoom())
        {
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            return;
        }

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            distance -= scroll * scrollZoomSpeed * 0.01f;
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.isPressed)
            {
                distance -= keyboardZoomSpeed * Time.unscaledDeltaTime;
            }

            if (Keyboard.current.eKey.isPressed)
            {
                distance += keyboardZoomSpeed * Time.unscaledDeltaTime;
            }
        }

        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    private void UpdateCameraPosition()
    {
        float deltaTime = Time.unscaledDeltaTime;

        currentDistance = Mathf.SmoothDamp(currentDistance, distance, ref distanceVelocity, transitionSmoothTime, Mathf.Infinity, deltaTime);
        currentTargetPosition = Vector3.SmoothDamp(
            currentTargetPosition,
            target.position,
            ref targetPositionVelocity,
            transitionSmoothTime,
            Mathf.Infinity,
            deltaTime);

        float rotationLerp = transitionSmoothTime <= 0.0001f
            ? 1f
            : 1f - Mathf.Exp(-deltaTime / transitionSmoothTime);
        currentOrbitRotation = Quaternion.Slerp(currentOrbitRotation, orbitRotation, rotationLerp);

        Vector3 offset = currentOrbitRotation * new Vector3(0f, 0f, -currentDistance);

        transform.position = currentTargetPosition + offset;
        transform.rotation = Quaternion.LookRotation((currentTargetPosition - transform.position).normalized, currentOrbitRotation * Vector3.up);
    }

    private void HandleMouseRotation()
    {
        if (Mouse.current == null)
        {
            return;
        }

        bool leftPressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
        bool rightPressedThisFrame = Mouse.current.rightButton.wasPressedThisFrame;
        bool isDragging = Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed;

        if (leftPressedThisFrame || rightPressedThisFrame)
        {
            lastMousePosition = Mouse.current.position.ReadValue();
            return;
        }

        if (!isDragging || IsPointerOverUi())
        {
            return;
        }

        Vector3 currentMousePosition = Mouse.current.position.ReadValue();
        Vector3 delta = currentMousePosition - lastMousePosition;
        lastMousePosition = currentMousePosition;

        ApplyRotationDelta(delta);
    }

    private bool TryHandleTouchRotation()
    {
        if (Touchscreen.current == null)
        {
            return false;
        }

        int activeTouchCount = 0;
        TouchControl activeTouch = null;

        foreach (TouchControl touch in Touchscreen.current.touches)
        {
            if (!touch.press.isPressed)
            {
                continue;
            }

            activeTouchCount++;

            if (activeTouch == null)
            {
                activeTouch = touch;
            }
        }

        if (activeTouchCount != 1 || activeTouch == null)
        {
            return false;
        }

        int touchId = activeTouch.touchId.ReadValue();

        if (IsPointerOverUi(touchId))
        {
            return true;
        }

        Vector2 delta = activeTouch.delta.ReadValue();
        ApplyRotationDelta(delta);
        return true;
    }

    private bool TryHandleTouchZoom()
    {
        if (Touchscreen.current == null)
        {
            return false;
        }

        TouchControl firstTouch = null;
        TouchControl secondTouch = null;

        foreach (TouchControl touch in Touchscreen.current.touches)
        {
            if (!touch.press.isPressed)
            {
                continue;
            }

            if (firstTouch == null)
            {
                firstTouch = touch;
            }
            else
            {
                secondTouch = touch;
                break;
            }
        }

        if (firstTouch == null || secondTouch == null)
        {
            return false;
        }

        int firstTouchId = firstTouch.touchId.ReadValue();
        int secondTouchId = secondTouch.touchId.ReadValue();

        if (IsPointerOverUi(firstTouchId) || IsPointerOverUi(secondTouchId))
        {
            return true;
        }

        Vector2 firstPosition = firstTouch.position.ReadValue();
        Vector2 secondPosition = secondTouch.position.ReadValue();
        Vector2 firstPrevious = firstPosition - firstTouch.delta.ReadValue();
        Vector2 secondPrevious = secondPosition - secondTouch.delta.ReadValue();

        float previousDistance = Vector2.Distance(firstPrevious, secondPrevious);
        float currentDistance = Vector2.Distance(firstPosition, secondPosition);
        float pinchDelta = currentDistance - previousDistance;

        distance -= pinchDelta * pinchZoomSpeed;
        return true;
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private static bool IsPointerOverUi(int fingerId)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }

    public void FocusOnTarget(Transform newTarget, float focusDistance, float focusMinDistance, float focusMaxDistance)
    {
        FocusOnTarget(newTarget, focusDistance, focusMinDistance, focusMaxDistance, transform.rotation);
    }

    public void FocusOnTarget(
        Transform newTarget,
        float focusDistance,
        float focusMinDistance,
        float focusMaxDistance,
        Quaternion startViewRotation)
    {
        if (newTarget == null)
        {
            return;
        }

        target = newTarget;
        inspectBody = newTarget;
        inspectMode = true;
        minDistance = focusMinDistance;
        maxDistance = focusMaxDistance;
        distance = Mathf.Clamp(focusDistance, minDistance, maxDistance);
        orbitRotation = startViewRotation;
        inspectStartRotation = orbitRotation;
        inspectStartDistance = distance;

        if (cachedCamera != null)
        {
            cachedCamera.nearClipPlane = inspectNearClipPlane;
        }

        UpdateCameraPosition();
    }

    public void ResetFocusedView()
    {
        if (!inspectMode)
        {
            return;
        }

        orbitRotation = inspectStartRotation;
        distance = Mathf.Clamp(inspectStartDistance, minDistance, maxDistance);
        UpdateCameraPosition();
    }

    public void RestoreDefaultView()
    {
        inspectMode = false;
        inspectBody = null;
        target = defaultTarget;
        distance = defaultDistance;
        minDistance = defaultMinDistance;
        maxDistance = defaultMaxDistance;
        yaw = defaultYaw;
        pitch = defaultPitch;
        orbitRotation = Quaternion.Euler(defaultPitch, defaultYaw, 0f);

        if (cachedCamera != null)
        {
            cachedCamera.nearClipPlane = defaultNearClipPlane;
        }

        UpdateCameraPosition();
    }

    private void ApplyRotationDelta(Vector2 delta)
    {
        float yawDelta = delta.x * rotationSpeed;
        float pitchDelta = -delta.y * rotationSpeed;

        Vector3 worldUp = (currentOrbitRotation * Vector3.up).normalized;
        Vector3 rightAxis = (currentOrbitRotation * Vector3.right).normalized;

        orbitRotation = Quaternion.AngleAxis(yawDelta, worldUp) * orbitRotation;
        orbitRotation = Quaternion.AngleAxis(pitchDelta, rightAxis) * orbitRotation;
    }

    private void ClampPitchIfNeeded()
    {
        if (allowFullVerticalOrbit)
        {
            return;
        }

        pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
    }
}
