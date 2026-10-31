using UnityEngine;

/// <summary>
/// Birinci şahıs yürüme + fare ile bakma.
/// WASD: yürü, Shift: koş, Space: zıpla, Esc: fareyi serbest bırak.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Hareket")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 6f;
    public float gravity = -20f;
    public float jumpHeight = 0.9f;
    [Tooltip("Yerdeki kutulara çarpınca onları ne kadar itsin")]
    public float pushPower = 1.2f;

    [Header("Kamera")]
    public Transform cameraRoot;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 85f;

    /// Yürüme hızı çarpanı (örn. alışveriş arabası iterken yavaşlar).
    [HideInInspector] public float speedMultiplier = 1f;

    /// Menü açıkken (dükkân bilgisayarı, pazarlık ekranı vb.) true yapılır.
    public bool InputLocked { get; private set; }

    /// Fare en son hangi karede kilitlendi (o tıklama etkileşim sayılmasın diye).
    public int CursorLockedFrame { get; private set; } = -1;

    // Menü Esc ile kapandığı karede aynı Esc fareyi tekrar serbest bırakmasın.
    int inputLockChangedFrame = -1;

    CharacterController cc;
    float pitch;
    float verticalVelocity;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (cameraRoot == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraRoot = cam.transform;
        }
    }

    void Start() => SetCursorLocked(true);

    /// Arayüz açıldığında hareketi ve bakışı durdurur, fareyi serbest bırakır.
    public void SetInputLocked(bool locked)
    {
        InputLocked = locked;
        inputLockChangedFrame = Time.frameCount;
        SetCursorLocked(!locked);
    }

    void SetCursorLocked(bool locked)
    {
        if (locked && Cursor.lockState != CursorLockMode.Locked) CursorLockedFrame = Time.frameCount;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void Update()
    {
        // Test kolaylığı: Esc ile fareyi bırak, tıklayınca geri kilitle.
        if (!InputLocked && Time.frameCount != inputLockChangedFrame)
        {
            if (GameInput.CancelPressed) SetCursorLocked(false);
            else if (Cursor.lockState != CursorLockMode.Locked && GameInput.PrimaryClickPressed) SetCursorLocked(true);
        }

        bool canLook = !InputLocked && Cursor.lockState == CursorLockMode.Locked;
        if (canLook) Look();
        Move();
    }

    void Look()
    {
        Vector2 look = GameInput.Look * mouseSensitivity;
        transform.Rotate(0f, look.x, 0f);
        pitch = Mathf.Clamp(pitch - look.y, -maxLookAngle, maxLookAngle);
        if (cameraRoot != null) cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        bool grounded = cc.isGrounded;
        if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;

        Vector2 input = InputLocked ? Vector2.zero : GameInput.Move;
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        if (move.sqrMagnitude > 1f) move.Normalize();

        float speed = ((!InputLocked && GameInput.Run) ? runSpeed : walkSpeed) * speedMultiplier;

        if (!InputLocked && grounded && GameInput.JumpPressed)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * speed;
        velocity.y = verticalVelocity;
        cc.Move(velocity * Time.deltaTime);
    }

    // Yerdeki serbest kutulara yürüyünce hafifçe itsin.
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var rb = hit.collider.attachedRigidbody;
        if (rb == null || rb.isKinematic || hit.moveDirection.y < -0.3f) return;
        if (rb.mass > 40f || rb.GetComponent<CarController>() != null) return; // araba gibi ağır şeyler itilmez
        var cart = rb.GetComponent<ShoppingCart>();
        if (cart != null && cart.IsPushed) return; // itilen arabayı kendisi yönetir
        Vector3 push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z) * pushPower;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = push;
#else
        rb.velocity = push;
#endif
    }
}
