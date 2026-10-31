using UnityEngine;

/// <summary>
/// Kameranın baktığı yere ışın atar, bakılan IInteractable'ı bulur,
/// [E] veya sol tık ile etkileşime girer. Eşya taşıma (elde tutma) da buradadır.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("Referanslar")]
    public Camera cam;
    [Tooltip("Taşınan eşyanın duracağı nokta (kameranın çocuğu).")]
    public Transform holdPoint;

    [Header("Ayarlar")]
    public float reach = 3f;
    [Tooltip("Küçük nesneleri (işlemci, RAM) daha kolay hedeflemek için ışının kalınlığı")]
    public float aimAssistRadius = 0.15f;
    public LayerMask interactMask = ~0;
    public float dropDistance = 0.9f;

    /// Şu an elde tutulan eşya (yoksa null).
    public Carryable HeldItem { get; private set; }

    /// Şu an itilen alışveriş arabası (yoksa null).
    public ShoppingCart PushedCart { get; private set; }

    /// Eller dolu mu? (eşya taşıyor ya da araba itiyor)
    public bool HandsBusy => HeldItem != null || PushedCart != null;

    /// Şu an bakılan etkileşimli nesne (yoksa null).
    public IInteractable Target { get; private set; }

    /// Ekranda gösterilecek ipucu (HUD okur).
    public string CurrentPrompt { get; private set; } = "";

    /// Arabadayken true: hedef aranmaz, ipucu gösterilmez (E tuşunu araba kullanır).
    [HideInInspector] public bool Suspended;

    FirstPersonController controller;
    readonly RaycastHit[] hitBuffer = new RaycastHit[32];

    void Awake()
    {
        controller = GetComponent<FirstPersonController>();
        if (cam == null) cam = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        if (Suspended || (controller != null && controller.InputLocked))
        {
            Target = null;
            CurrentPrompt = "";
            return;
        }

        FindTarget();

        // Fare kilitliyken (oyun içindeyken) tıklamalar da etkileşim sayılır.
        bool mouseActive = Cursor.lockState == CursorLockMode.Locked
                           && (controller == null || controller.CursorLockedFrame != Time.frameCount);

        bool interact = GameInput.InteractPressed || (mouseActive && GameInput.PrimaryClickPressed);
        bool drop = GameInput.DropPressed || (mouseActive && GameInput.SecondaryClickPressed);

        if (interact && Target != null && Target.CanInteract(this))
            Target.Interact(this);
        else if (drop && HeldItem != null)
            DropHeldItem();
        else if (drop && PushedCart != null)
            StopPushing();
    }

    /// İtilen arabanın kendisi (ve içindekiler) hedeflemeyi engellemesin.
    bool IsOwnCart(Collider c) => PushedCart != null && c.GetComponentInParent<ShoppingCart>() == PushedCart;

    /// Işının çarptığı en yakın nesne (itilen araba hariç).
    bool FirstHit(Ray ray, out RaycastHit best)
    {
        best = default;
        bool found = false;
        int n = Physics.RaycastNonAlloc(ray, hitBuffer, reach, interactMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var h = hitBuffer[i];
            if (IsOwnCart(h.collider)) continue;
            if (!found || h.distance < best.distance) { best = h; found = true; }
        }
        return found;
    }

    void FindTarget()
    {
        Target = null;
        var ray = new Ray(cam.transform.position, cam.transform.forward);

        // 1) Tam nişangâhın altındaki nesne
        if (FirstHit(ray, out RaycastHit hit))
        {
            var it = hit.collider.GetComponentInParent<IInteractable>();
            if (it != null && it.CanInteract(this)) Target = it;
        }

        // 2) Bulunamadıysa nişangâhın biraz çevresine bak (küçük kutular için)
        if (Target == null)
        {
            var hits = Physics.SphereCastAll(ray, aimAssistRadius, reach, interactMask, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.distance <= 0f) continue; // başlangıçta içinde olduğu şeyler (oyuncunun kendisi)
                if (IsOwnCart(h.collider)) continue;
                var it = h.collider.GetComponentInParent<IInteractable>();
                if (it != null && it.CanInteract(this) && h.distance < best)
                {
                    best = h.distance;
                    Target = it;
                }
            }
        }

        if (Target != null)
            CurrentPrompt = Target.GetPrompt(this);
        else if (HeldItem != null)
            CurrentPrompt = $"{HeldItem.DisplayName}   [G / Sağ tık] Bırak";
        else if (PushedCart != null)
            CurrentPrompt = "Alışveriş arabası   [G / Sağ tık] Arabayı bırak";
        else
            CurrentPrompt = "";
    }

    // ---------- Alışveriş arabası ----------

    public void StartPushing(ShoppingCart cart)
    {
        if (HandsBusy || cart == null) return;
        PushedCart = cart;
        cart.BeginPush(this);
        if (controller != null) controller.speedMultiplier = cart.pushSpeedMultiplier;
    }

    public void StopPushing()
    {
        if (PushedCart == null) return;
        var cart = PushedCart;
        PushedCart = null;
        cart.EndPush();
        if (controller != null) controller.speedMultiplier = 1f;
    }

    // ---------- Taşıma ----------

    public void PickUp(Carryable item)
    {
        if (HeldItem != null || item == null) return;
        item.BeginHold(holdPoint);
        HeldItem = item;
    }

    /// Eldeki eşyayı başka bir sisteme (raf, bagaj, tezgâh) devretmek için.
    public Carryable ReleaseHeldItem()
    {
        var item = HeldItem;
        HeldItem = null;
        return item;
    }

    public void DropHeldItem()
    {
        if (HeldItem == null) return;
        var item = ReleaseHeldItem();

        Vector3 forward = cam.transform.forward;
        Vector3 pos = cam.transform.position + forward * dropDistance;

        // Eşyayı duvarın içine bırakmamak için önü kontrol et.
        if (Physics.Raycast(cam.transform.position, forward, out RaycastHit hit, dropDistance, interactMask, QueryTriggerInteraction.Ignore))
            pos = hit.point - forward * 0.25f;

        Quaternion rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        item.Drop(pos, rot, Vector3.zero);
    }
}
