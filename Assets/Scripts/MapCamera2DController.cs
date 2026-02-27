using UnityEngine;
using UnityEngine.InputSystem;

public class MapCamera2DController : MonoBehaviour
{
    public Camera cam;
    public float panSpeed = 1.0f;          // multiplier
    public float zoomSpeed = 0.15f;        // orthographic zoom factor
    public float minOrthoSize = 10f;
    public float maxOrthoSize = 500000f;

    [Header("Mouse")]
    public int panMouseButton = 1; // 0=LMB, 1=RMB, 2=MMB
    public bool requirePanButton = true;

    private Vector2 _lastMouse;
    private bool _panning;

    void Reset()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        if (cam == null) return;
        var mouse = Mouse.current;
        if (mouse == null) return;

        // Zoom with scroll
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float factor = 1f - Mathf.Sign(scroll) * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor, minOrthoSize, maxOrthoSize);
        }

        // Pan with mouse drag
        bool panPressed = !requirePanButton || IsButtonPressed(mouse, panMouseButton);

        if (panPressed && !_panning)
        {
            _panning = true;
            _lastMouse = mouse.position.ReadValue();
        }
        else if (!panPressed && _panning)
        {
            _panning = false;
        }

        if (_panning)
        {
            Vector2 cur = mouse.position.ReadValue();
            Vector2 delta = cur - _lastMouse;
            _lastMouse = cur;

            // Move camera in its local X/Z plane (top-down: X right, Z forward)
            // Scale pan based on ortho size so it feels consistent
            float scale = cam.orthographicSize * 0.002f * panSpeed;
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) scale *= 3f;

            Vector3 move = new Vector3(-delta.x * scale, 0f, -delta.y * scale);
            cam.transform.position += move;
        }
    }

    static bool IsButtonPressed(Mouse mouse, int button)
    {
        return button switch
        {
            0 => mouse.leftButton.isPressed,
            1 => mouse.rightButton.isPressed,
            2 => mouse.middleButton.isPressed,
            _ => mouse.rightButton.isPressed
        };
    }
}