using UnityEngine;

/// <summary>
/// Basit (arcade) araba sürüşü: her tekerlek için aşağı ışın atan yaylı süspansiyon, önden çekiş,
/// yanlara kaymayı önleyen tutuş, el freni. Arabaya [E] ile binilir / inilir.
/// Biniş sırasında oyuncu nesnesi sürücü gözü noktasına bağlanır, yürüme kapanır, fare ile etrafa bakılır.
/// Bagaj yuvalarındaki kutular yuvaya bağlı oldukları için araba ile birlikte gider.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour, IInteractable
{
    [System.Serializable]
    public class Wheel
    {
        public Transform pivot;     // göbek (süspansiyon + direksiyon)
        public Transform spinner;   // dönen kısım (lastik + jant)
        public bool front;
        [HideInInspector] public Vector3 restLocal;
        [HideInInspector] public float lastLength;
        [HideInInspector] public float spin;
        [HideInInspector] public bool grounded;
    }

    [Header("Parçalar (sahne kurucu atar)")]
    public Wheel[] wheels;
    public Transform steeringWheel;
    public Vector3 steeringAxis = Vector3.back;
    public Transform driverEye;
    public PlacementSlot[] trunkSlots;

    [Header("Motor / fren")]
    public float engineForce = 6500f;
    public float maxSpeed = 27f;          // ~97 km/s
    public float maxReverseSpeed = 6f;
    public float brakeForce = 11000f;
    public float rollingResistance = 180f;
    public float airDrag = 0.42f;

    [Header("Direksiyon")]
    public float maxSteerAngle = 33f;
    public float steerSpeed = 110f;       // derece/saniye
    public float steeringWheelRatio = 11f;

    [Header("Süspansiyon")]
    public float wheelRadius = 0.316f;
    public float rayTop = 0.25f;          // ışın göbeğin bu kadar üstünden başlar
    public float springTravel = 0.08f;    // statik yükte çökme
    public float springStiffness = 38000f;
    public float damping = 2600f;

    [Header("Tutuş")]
    public float lateralGrip = 12f;
    public float frictionCoef = 1.15f;
    public float handbrakeRearGrip = 0.25f;

    /// Şu an sürülen araba (yoksa null). HUD okur.
    public static CarController Current { get; private set; }
    public float SpeedKmh => Vector3.Dot(rb.linearVelocityCompat(), transform.forward) * 3.6f;

    Rigidbody rb;
    float steer;
    float yaw, pitch;
    int enteredFrame = -1;

    PlayerInteractor driver;
    FirstPersonController driverFpc;
    CharacterController driverCc;
    Transform driverCam;
    Vector3 camLocalPos;
    Quaternion camLocalRot;

    readonly RaycastHit[] hits = new RaycastHit[8];
    Collider[] ownColliders;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 1250f;
        rb.centerOfMass = new Vector3(0f, 0.42f, 0.15f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.SetCarDamping();

        foreach (var w in wheels)
        {
            w.restLocal = w.pivot.localPosition;
            w.lastLength = rayTop;
        }
        ownColliders = GetComponentsInChildren<Collider>(true);

        if (trunkSlots != null)
            foreach (var s in trunkSlots)
            {
                s.ItemPlaced += (slot, item) => IgnoreItem(item, true);
                s.ItemRemoved += (slot, item) => IgnoreItem(item, false);
            }

        // Park hâlinde başlar: kimse süremezken hiçbir şey itemez, devirmez
        rb.isKinematic = true;
    }

    float parkTimer, flipTimer;

    /// Ters dönmüş / yan yatmış arabayı yönünü koruyarak dik hâle getirir.
    void Upright()
    {
        var fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(transform.up, Vector3.up);
        transform.SetPositionAndRotation(transform.position + Vector3.up * 0.8f, Quaternion.LookRotation(fwd.normalized, Vector3.up));
        if (!rb.isKinematic)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;
        }
    }

    /// Bagajdaki kutular arabanın kendi çarpıştırıcılarını itmesin (titreme olmasın).
    void IgnoreItem(Carryable item, bool ignore)
    {
        if (item == null) return;
        foreach (var ic in item.GetComponentsInChildren<Collider>(true))
            foreach (var oc in ownColliders)
                if (oc != null && !oc.isTrigger) Physics.IgnoreCollision(ic, oc, ignore);
    }

    // ---------- IInteractable ----------

    public bool CanInteract(PlayerInteractor p) => Current == null && !p.HandsBusy;

    public string GetPrompt(PlayerInteractor p) => "[E] Arabaya bin";

    public void Interact(PlayerInteractor p) => Enter(p);

    void Enter(PlayerInteractor p)
    {
        driver = p;
        driverFpc = p.GetComponent<FirstPersonController>();
        driverCc = p.GetComponent<CharacterController>();
        driverCam = p.cam.transform;
        camLocalPos = driverCam.localPosition;
        camLocalRot = driverCam.localRotation;

        if (driverCc != null) driverCc.enabled = false;
        if (driverFpc != null) driverFpc.enabled = false;
        p.Suspended = true;

        // Oyuncu sürücü gözüne bağlanır: kamera tam göz noktasına gelsin
        p.transform.SetParent(driverEye, false);
        p.transform.localRotation = Quaternion.identity;
        p.transform.localPosition = -camLocalPos;
        driverCam.localRotation = Quaternion.identity;
        yaw = 0f; pitch = 0f;

        if (transform.up.y < 0.5f) Upright();
        rb.isKinematic = false;
        parkTimer = 0f;
        flipTimer = 0f;

        Current = this;
        enteredFrame = Time.frameCount;
    }

    void Exit()
    {
        if (!FindExitSpot(out var spot))
        {
            GameHUD.ShowMessage("Burada inemezsin, kapının yanı dolu.");
            return;
        }

        var p = driver;
        p.transform.SetParent(null, true);
        p.transform.SetPositionAndRotation(spot, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        driverCam.localPosition = camLocalPos;
        driverCam.localRotation = camLocalRot;

        if (driverCc != null) driverCc.enabled = true;
        if (driverFpc != null) driverFpc.enabled = true;
        p.Suspended = false;

        driver = null;
        Current = null;
    }

    /// Önce sürücü (sol) kapısının yanı, sonra sağ, sonra arka ve ön.
    bool FindExitSpot(out Vector3 spot)
    {
        var candidates = new[] { new Vector3(-1.35f, 0f, 0.1f), new Vector3(1.35f, 0f, 0.1f), new Vector3(0f, 0f, -3.1f), new Vector3(0f, 0f, 3.1f) };
        foreach (var c in candidates)
        {
            var p = transform.TransformPoint(c);
            if (!Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var g, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (g.collider.attachedRigidbody == rb) continue;
            var feet = g.point + Vector3.up * 0.05f;
            bool blocked = Physics.CheckCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.45f, 0.3f, ~0, QueryTriggerInteraction.Ignore);
            if (!blocked) { spot = feet; return true; }
        }
        spot = default;
        return false;
    }

    // ---------- Girdi + görsel ----------

    void Update()
    {
        if (Current == this && driver != null)
        {
            // İnmek için E (bindiğimiz karedeki aynı basış sayılmasın)
            if (Time.frameCount != enteredFrame && GameInput.InteractPressed)
            {
                if (Mathf.Abs(SpeedKmh) < 6f) Exit();
                else GameHUD.ShowMessage("İnmek için önce dur.");
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var look = GameInput.Look * 2f;
                yaw = Mathf.Clamp(yaw + look.x, -130f, 130f);
                pitch = Mathf.Clamp(pitch - look.y, -55f, 40f);
                if (driverCam != null) driverCam.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            if (GameInput.CancelPressed) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            else if (Cursor.lockState != CursorLockMode.Locked && GameInput.PrimaryClickPressed) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

            // Takla atıp yan / ters kaldıysa 1.5 sn sonra kendiliğinden düzelir
            if (transform.up.y < 0.3f && rb.linearVelocityCompat().magnitude < 2f)
            {
                flipTimer += Time.deltaTime;
                if (flipTimer > 1.5f) { Upright(); flipTimer = 0f; GameHUD.ShowMessage("Araba düzeltildi."); }
            }
            else flipTimer = 0f;
        }

        float target = (Current == this ? GameInput.Move.x : 0f) * maxSteerAngle * Mathf.Lerp(1f, 0.35f, Mathf.Abs(SpeedKmh) / 3.6f / maxSpeed);
        steer = Mathf.MoveTowards(steer, target, steerSpeed * Time.deltaTime);

        // Tekerleklerin görüntüsü: süspansiyon, direksiyon açısı, dönüş
        float vLong = Vector3.Dot(rb.linearVelocityCompat(), transform.forward);
        foreach (var w in wheels)
        {
            w.pivot.localPosition = w.restLocal + Vector3.up * (rayTop - w.lastLength);
            w.pivot.localRotation = w.front ? Quaternion.Euler(0f, steer, 0f) : Quaternion.identity;
            if (w.grounded) w.spin += vLong / wheelRadius * Mathf.Rad2Deg * Time.deltaTime;
            if (w.spinner != null) w.spinner.localRotation = Quaternion.Euler(w.spin % 360f, 0f, 0f);
        }
        if (steeringWheel != null) steeringWheel.localRotation = Quaternion.AngleAxis(steer * steeringWheelRatio, steeringAxis);
    }

    // ---------- Fizik ----------

    void FixedUpdate()
    {
        if (rb.isKinematic) return; // park hâlinde
        bool driving = Current == this;
        Vector2 input = driving ? GameInput.Move : Vector2.zero;
        bool handbrake = !driving || GameInput.HandbrakeHeld;

        Vector3 vel = rb.linearVelocityCompat();
        float vLong = Vector3.Dot(vel, transform.forward);
        float dt = Time.fixedDeltaTime;

        // Gaz / fren / geri vites
        float throttle = 0f, brake = 0f;
        if (input.y > 0.1f)
        {
            if (vLong < -0.5f) brake = 1f; else throttle = 1f;
        }
        else if (input.y < -0.1f)
        {
            if (vLong > 0.5f) brake = 1f; else throttle = -1f;
        }
        if (!driving) brake = 1f;

        int drivenGrounded = 0;
        foreach (var w in wheels) if (w.front && w.grounded) drivenGrounded++;

        foreach (var w in wheels)
        {
            Vector3 origin = transform.TransformPoint(w.restLocal + Vector3.up * rayTop);
            Vector3 down = -transform.up;
            float maxLen = rayTop + springTravel + wheelRadius + 0.05f;

            w.grounded = CastGround(origin, down, maxLen, out var hit);
            if (!w.grounded)
            {
                w.lastLength = Mathf.MoveTowards(w.lastLength, rayTop + springTravel, dt * 2f);
                continue;
            }

            float length = Mathf.Max(hit.distance - wheelRadius, 0f);
            float natural = rayTop + springTravel;
            float compression = natural - length;
            float vComp = (w.lastLength - length) / dt;
            w.lastLength = length;

            float springF = springStiffness * compression + damping * vComp;
            if (length < 0.04f) springF += (0.04f - length) * springStiffness * 4f; // tampon lastik
            springF = Mathf.Max(springF, 0f);

            Vector3 contact = hit.point;
            rb.AddForceAtPosition(transform.up * springF, contact);

            // Tekerleğin yön vektörleri (ön tekerler direksiyonla döner)
            Quaternion steerRot = w.front ? Quaternion.AngleAxis(steer, transform.up) : Quaternion.identity;
            Vector3 fwd = Vector3.ProjectOnPlane(steerRot * transform.forward, hit.normal).normalized;
            Vector3 right = Vector3.Cross(hit.normal, fwd).normalized;

            Vector3 pv = rb.GetPointVelocity(contact);
            float vLat = Vector3.Dot(pv, right);
            float vFwd = Vector3.Dot(pv, fwd);

            // Yana tutuş
            float grip = lateralGrip * (handbrake && !w.front && driving ? handbrakeRearGrip : 1f);
            float latF = -vLat * grip * rb.mass / wheels.Length;
            float maxF = frictionCoef * springF;
            latF = Mathf.Clamp(latF, -maxF, maxF);

            // İleri: motor (ön çekiş), fren, yuvarlanma direnci
            float longF = 0f;
            if (w.front && drivenGrounded > 0 && throttle != 0f)
            {
                float limit = throttle > 0f ? maxSpeed : maxReverseSpeed;
                float taper = Mathf.Clamp01(1f - Mathf.Abs(vLong) / limit);
                longF += throttle * engineForce * taper / drivenGrounded;
            }
            float stopF = 0f;
            if (brake > 0f) stopF += brakeForce * brake / wheels.Length;
            if (handbrake && !w.front) stopF += brakeForce * 0.5f / 2f;
            stopF += rollingResistance / wheels.Length;
            // Durmuş araçta fren titreşim yapmasın: hızı sıfırlayacak kadar kuvvet uygula
            float stopCap = Mathf.Abs(vFwd) * rb.mass / wheels.Length / dt;
            longF -= Mathf.Sign(vFwd) * Mathf.Min(stopF, stopCap);
            longF = Mathf.Clamp(longF, -maxF * 1.2f, maxF * 1.2f);

            rb.AddForceAtPosition(right * latF + fwd * longF, contact);
        }

        // Hava direnci
        rb.AddForce(-vel * vel.magnitude * airDrag);

        // Sürücü indi ve araba durdu: dört teker yerdeyse park et (kinematik)
        bool allGrounded = true;
        foreach (var w in wheels) allGrounded &= w.grounded;
        if (!driving && allGrounded && vel.magnitude < 0.3f && rb.angularVelocity.magnitude < 0.2f)
        {
            parkTimer += dt;
            if (parkTimer > 0.6f) rb.isKinematic = true;
        }
        else parkTimer = 0f;
    }

    bool CastGround(Vector3 origin, Vector3 dir, float maxLen, out RaycastHit best)
    {
        best = default;
        int n = Physics.RaycastNonAlloc(origin, dir, hits, maxLen, ~0, QueryTriggerInteraction.Ignore);
        float bestD = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.collider.attachedRigidbody == rb) continue;      // kendi gövdesi
            var ar = h.collider.attachedRigidbody;
            if (ar != null && !ar.isKinematic && ar.mass < 50f) continue; // yerdeki kutular tekerleği kaldırmasın
            if (h.distance < bestD) { bestD = h.distance; best = h; found = true; }
        }
        return found;
    }
}

/// Unity 6 / eski sürüm uyumu için küçük yardımcılar.
static class RigidbodyCompat
{
    public static Vector3 linearVelocityCompat(this Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    public static void SetCarDamping(this Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        rb.angularDamping = 0.6f;
        rb.linearDamping = 0f;
#else
        rb.angularDrag = 0.6f;
        rb.drag = 0f;
#endif
    }
}
