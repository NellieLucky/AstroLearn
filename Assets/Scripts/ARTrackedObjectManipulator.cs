using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

[DisallowMultipleComponent]
public class ARTrackedObjectManipulator : MonoBehaviour
{
    [SerializeField] private float pinchScaleSpeed = 0.01f;
    [SerializeField] private float mouseZoomSpeed = 0.0015f;
    [SerializeField] private float keyboardZoomSpeed = 0.9f;
    [SerializeField] private float minScaleMultiplier = 0.6f;
    [SerializeField] private float maxScaleMultiplier = 2.5f;
    [SerializeField] private bool autoRotateWhenNoNativeRotation = true;
    [SerializeField] private float autoRotateSpeed = 18f;
    [SerializeField] private bool allowMouseControls = true;

    private Vector3 initialLocalScale;
    private Quaternion initialLocalRotation;
    private Transform rotationTarget;
    private Quaternion initialRotationTargetLocalRotation;
    private float currentScaleMultiplier = 1f;
    private float autoRotationYaw;
    private bool hasNativeRotationInHierarchy;

    private void Awake()
    {
        CacheInitialTransform();
        CacheNativeRotationState();
        CacheRotationTarget();
    }

    private void Update()
    {
        HandleScaleInput();
        ApplyAutoRotation();
    }

    public void ResetToInitialPose()
    {
        CacheInitialTransform();
        CacheNativeRotationState();
        CacheRotationTarget();
        currentScaleMultiplier = 1f;
        autoRotationYaw = 0f;
        transform.localRotation = initialLocalRotation;
        transform.localScale = initialLocalScale;

        if (rotationTarget != null)
        {
            rotationTarget.localRotation = initialRotationTargetLocalRotation;
        }
    }

    private void CacheInitialTransform()
    {
        if (initialLocalScale == Vector3.zero)
        {
            initialLocalScale = transform.localScale;
        }

        if (initialLocalRotation == default)
        {
            initialLocalRotation = transform.localRotation;
        }
    }

    private void CacheNativeRotationState()
    {
        PlanetRotation[] nativeRotations = GetComponentsInChildren<PlanetRotation>(true);
        hasNativeRotationInHierarchy = nativeRotations != null && nativeRotations.Length > 0;
    }

    private void CacheRotationTarget()
    {
        if (rotationTarget != null)
        {
            return;
        }

        rotationTarget = ResolveRotationTarget();
        initialRotationTargetLocalRotation = rotationTarget != null
            ? rotationTarget.localRotation
            : initialLocalRotation;
    }

    private void HandleScaleInput()
    {
        if (TryHandleTouchScale())
        {
            return;
        }

        if (HandleKeyboardZoom())
        {
            return;
        }

        if (!allowMouseControls)
        {
            return;
        }

        HandleMouseZoom();
    }

    private void ApplyAutoRotation()
    {
        if (!autoRotateWhenNoNativeRotation || hasNativeRotationInHierarchy || rotationTarget == null)
        {
            return;
        }

        autoRotationYaw += autoRotateSpeed * Time.unscaledDeltaTime;
        rotationTarget.localRotation = initialRotationTargetLocalRotation * Quaternion.Euler(0f, autoRotationYaw, 0f);
    }

    private bool TryHandleTouchScale()
    {
#if ENABLE_INPUT_SYSTEM
        if (TryHandleInputSystemTouchScale())
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (TryHandleLegacyTouchScale())
        {
            return true;
        }
#endif

        return false;
    }

#if ENABLE_INPUT_SYSTEM
    private bool TryHandleInputSystemTouchScale()
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

        ApplyScaleDelta(pinchDelta * pinchScaleSpeed);
        return true;
    }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
    private bool TryHandleLegacyTouchScale()
    {
        if (Input.touchCount < 2)
        {
            return false;
        }

        Touch firstTouch = Input.GetTouch(0);
        Touch secondTouch = Input.GetTouch(1);

        if (IsPointerOverUi(firstTouch.fingerId) || IsPointerOverUi(secondTouch.fingerId))
        {
            return true;
        }

        Vector2 firstPrevious = firstTouch.position - firstTouch.deltaPosition;
        Vector2 secondPrevious = secondTouch.position - secondTouch.deltaPosition;

        float previousDistance = Vector2.Distance(firstPrevious, secondPrevious);
        float currentDistance = Vector2.Distance(firstTouch.position, secondTouch.position);
        float pinchDelta = currentDistance - previousDistance;

        ApplyScaleDelta(pinchDelta * pinchScaleSpeed);
        return true;
    }
#endif

    private void HandleMouseZoom()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            if (IsPointerOverUi())
            {
                return;
            }

            float scrollDelta = Mouse.current.scroll.ReadValue().y;
            if (!Mathf.Approximately(scrollDelta, 0f))
            {
                ApplyScaleDelta(scrollDelta * mouseZoomSpeed);
            }

            return;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (IsPointerOverUi())
        {
            return;
        }

        float scrollDelta = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(scrollDelta, 0f))
        {
            ApplyScaleDelta(scrollDelta * mouseZoomSpeed * 100f);
        }
#endif
    }

    private bool HandleKeyboardZoom()
    {
        float zoomDirection = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.isPressed)
            {
                zoomDirection += 1f;
            }

            if (Keyboard.current.qKey.isPressed)
            {
                zoomDirection -= 1f;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.E))
        {
            zoomDirection += 1f;
        }

        if (Input.GetKey(KeyCode.Q))
        {
            zoomDirection -= 1f;
        }
#endif

        if (Mathf.Approximately(zoomDirection, 0f))
        {
            return false;
        }

        ApplyScaleDelta(zoomDirection * keyboardZoomSpeed * Time.unscaledDeltaTime);
        return true;
    }

    private void ApplyScaleDelta(float delta)
    {
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        currentScaleMultiplier = Mathf.Clamp(currentScaleMultiplier + delta, minScaleMultiplier, maxScaleMultiplier);
        transform.localScale = initialLocalScale * currentScaleMultiplier;
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private static bool IsPointerOverUi(int fingerId)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    private Transform ResolveRotationTarget()
    {
        if (transform.childCount == 1)
        {
            return transform.GetChild(0);
        }

        Renderer firstRenderer = GetComponentInChildren<Renderer>(true);
        if (firstRenderer != null)
        {
            Transform candidate = firstRenderer.transform;
            while (candidate != null && candidate.parent != null && candidate.parent != transform)
            {
                candidate = candidate.parent;
            }

            if (candidate != null)
            {
                return candidate;
            }
        }

        return transform;
    }
}
