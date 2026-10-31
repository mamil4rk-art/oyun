using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Toptancıdaki alışveriş arabası. Boş elle [E] ile tutup itilir, [G] / sağ tık ile bırakılır.
/// İçine kutu konur: elindeki kutuyla arabaya bakıp [E], ya da araba iterken rafa bakıp [E].
/// Kutular sepete boyutlarına göre sıra sıra, gerekirse kat kat dizilir.
/// Sepetteki kutular yuva (PlacementSlot) çocuklarındadır; kasa ödenmemiş kutuları buradan okur.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ShoppingCart : MonoBehaviour, IInteractable
{
    public static readonly List<ShoppingCart> All = new List<ShoppingCart>();

    [Header("İtme")]
    [Tooltip("Araba merkezinin oyuncunun ne kadar önünde duracağı")]
    public float holdDistance = 1.15f;
    public float followGain = 12f;
    public float maxSpeed = 7f;
    public float turnSpeed = 540f;
    [Tooltip("Araba bir yere takılıp oyuncudan bu kadar uzak kalırsa bırakılır")]
    public float breakDistance = 2.2f;
    [Tooltip("Araba iterken yürüme hızı çarpanı")]
    public float pushSpeedMultiplier = 0.8f;

    [Header("Sepet (arabaya göre yerel)")]
    public float basketFloorY = 0.46f;
    public Vector2 basketSize = new Vector2(0.52f, 0.8f); // x genişlik, y derinlik (z)
    public float basketCenterZ = 0.02f;

    public PlayerInteractor Pusher { get; private set; }
    public bool IsPushed => Pusher != null;

    Rigidbody rb;
    PlacementSlot[] slots;
    Collider[] solidColliders;
    readonly List<PlacementSlot> order = new List<PlacementSlot>(); // konulma sırası

    static PhysicsMaterial slippery;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        SetDamping(4f);

        // Tekerlekler sürtünmesiz kaysın; durmayı sönümleme (damping) sağlar.
        if (slippery == null)
            slippery = new PhysicsMaterial("ArabaKaygan")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
            };

        var solids = new List<Collider>();
        foreach (var c in GetComponentsInChildren<Collider>(true))
        {
            if (c.isTrigger || c.attachedRigidbody != rb) continue;
            c.sharedMaterial = slippery;
            solids.Add(c);
        }
        solidColliders = solids.ToArray();

        slots = GetComponentsInChildren<PlacementSlot>(true);
        foreach (var s in slots)
        {
            s.ItemPlaced += OnItemPlaced;
            s.ItemRemoved += OnItemRemoved;
        }
        Relayout();
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // ---------- İçerik ----------

    public int Capacity => slots.Length;

    public int Count
    {
        get
        {
            int n = 0;
            foreach (var s in slots) if (!s.IsEmpty) n++;
            return n;
        }
    }

    public bool HasRoom => FirstEmpty() != null;

    public IEnumerable<ItemBox> Boxes
    {
        get
        {
            foreach (var s in order)
                if (s.Occupant is ItemBox b) yield return b;
        }
    }

    public int UnpaidCount
    {
        get
        {
            int n = 0;
            foreach (var b in Boxes) if (!b.IsPaid) n++;
            return n;
        }
    }

    public int UnpaidTotal
    {
        get
        {
            int t = 0;
            foreach (var b in Boxes) if (!b.IsPaid && b.Item != null) t += b.Item.wholesalePrice;
            return t;
        }
    }

    PlacementSlot FirstEmpty()
    {
        foreach (var s in slots) if (s.IsEmpty) return s;
        return null;
    }

    /// Eşyayı ilk boş yere koyar. Yer yoksa false.
    public bool TryAdd(Carryable item)
    {
        var s = FirstEmpty();
        return s != null && s.TryPlace(item);
    }

    void OnItemPlaced(PlacementSlot slot, Carryable item)
    {
        order.Add(slot);
        IgnoreCollisions(item, true); // sepetin tel duvarlarıyla itişmesin
        Relayout();
    }

    void OnItemRemoved(PlacementSlot slot, Carryable item)
    {
        order.Remove(slot);
        IgnoreCollisions(item, false);
        Relayout();
    }

    void IgnoreCollisions(Carryable item, bool ignore)
    {
        foreach (var a in item.GetComponentsInChildren<Collider>())
        {
            if (!a.enabled) continue;
            foreach (var b in solidColliders)
                if (b != null && b.enabled) Physics.IgnoreCollision(a, b, ignore);
        }
    }

    /// Kutuları boyutlarına göre sepete dizer: önce yan yana, sonra arkaya doğru, dolunca üst kata.
    void Relayout()
    {
        float x = 0f, z = 0f, y = 0f, rowDepth = 0f, layerH = 0f;
        foreach (var s in order)
        {
            Vector3 size = SizeOf(s.Occupant);
            if (x > 0f && x + size.x > basketSize.x) { x = 0f; z += rowDepth; rowDepth = 0f; }
            if (z > 0f && z + size.z > basketSize.y) { x = 0f; z = 0f; rowDepth = 0f; y += layerH; layerH = 0f; }

            s.transform.localPosition = new Vector3(
                -basketSize.x / 2f + x + size.x / 2f,
                basketFloorY + y,
                basketCenterZ - basketSize.y / 2f + z + size.z / 2f);
            s.transform.localRotation = Quaternion.identity;

            x += size.x + 0.01f;
            rowDepth = Mathf.Max(rowDepth, size.z + 0.01f);
            layerH = Mathf.Max(layerH, size.y + 0.005f);
        }

        // Boş yuvalar sepetin ortasında bekler (elindeki kutuyla sepete bakınca hedef olsunlar).
        foreach (var s in slots)
            if (s.IsEmpty) s.transform.localPosition = new Vector3(0f, basketFloorY, basketCenterZ);
    }

    static Vector3 SizeOf(Carryable c)
        => c is ItemBox b && b.Item != null ? b.Item.boxSize : new Vector3(0.3f, 0.3f, 0.3f);

    // ---------- İtme ----------

    public void BeginPush(PlayerInteractor p)
    {
        Pusher = p;
        SetDamping(0f);
    }

    public void EndPush()
    {
        Pusher = null;
        SetDamping(4f);
        SetVelocity(Vector3.zero);
    }

    void FixedUpdate()
    {
        if (Pusher == null) return;

        Transform t = Pusher.transform;
        Vector3 fwd = t.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) return;
        fwd.Normalize();

        Vector3 target = t.position + fwd * holdDistance;
        Vector3 delta = target - rb.position;
        delta.y = 0f;

        if (delta.magnitude > breakDistance)
        {
            Pusher.StopPushing();
            GameHUD.ShowMessage("Araba takıldı, bıraktın.");
            return;
        }

        Vector3 v = Vector3.ClampMagnitude(delta * followGain, maxSpeed);
        v.y = GetVelocity().y;
        SetVelocity(v);
        rb.angularVelocity = Vector3.zero;

        Quaternion targetRot = Quaternion.LookRotation(fwd, Vector3.up);
        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, turnSpeed * Time.fixedDeltaTime));
    }

    // ---------- IInteractable ----------

    public bool CanInteract(PlayerInteractor p)
    {
        if (p.PushedCart != null) return false;
        if (p.HeldItem != null) return HasRoom;
        return true;
    }

    public string GetPrompt(PlayerInteractor p)
    {
        if (p.HeldItem != null) return $"[E] {p.HeldItem.DisplayName} → arabaya koy";
        return Count > 0 ? $"[E] Arabayı it  ({Count} ürün)" : "[E] Arabayı it";
    }

    public void Interact(PlayerInteractor p)
    {
        if (p.HeldItem != null)
        {
            var item = p.ReleaseHeldItem();
            if (!TryAdd(item)) p.PickUp(item);
            return;
        }
        p.StartPushing(this);
    }

    // ---------- Sürüm uyumu ----------

    void SetDamping(float d)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = d;
#else
        rb.drag = d;
#endif
    }

    Vector3 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    void SetVelocity(Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }
}
