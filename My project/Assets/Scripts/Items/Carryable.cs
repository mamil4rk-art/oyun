using UnityEngine;

/// <summary>
/// Elle alınıp taşınabilen her eşyanın temel sınıfı.
/// Üç durumu olabilir: yerde serbest (fizik açık), elde, ya da bir yuvada (raf/bagaj/tezgâh).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Carryable : MonoBehaviour, IInteractable, IHighlightable
{
    [SerializeField] protected string displayName = "Eşya";
    public virtual string DisplayName => displayName;

    public bool IsHeld { get; private set; }
    public PlacementSlot CurrentSlot { get; private set; }

    /// Pivot noktası ile eşyanın tabanı arasındaki mesafe (yuvaya düz oturtmak için).
    public float PivotHeight { get; private set; }

    Rigidbody rb;
    Collider[] colliders;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        PivotHeight = CalculatePivotHeight();
    }

    float CalculatePivotHeight()
    {
        if (colliders == null || colliders.Length == 0) return 0f;
        Bounds b = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++) b.Encapsulate(colliders[i].bounds);
        return transform.position.y - b.min.y;
    }

    // ---------- IInteractable ----------

    public virtual bool CanInteract(PlayerInteractor p) => !IsHeld && !p.HandsBusy;
    public virtual string GetPrompt(PlayerInteractor p) => $"[E] {DisplayName} al";
    public virtual void Interact(PlayerInteractor p) => p.PickUp(this);

    // ---------- Durum geçişleri ----------

    /// Oyuncu eline aldığında.
    public void BeginHold(Transform holdPoint)
    {
        SetHighlighted(false);
        LeaveSlot();
        IsHeld = true;
        MakeKinematic();
        SetCollidersEnabled(false); // Elde tutarken ışını ve oyuncuyu engellemesin.
        transform.SetParent(holdPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    /// Bir yuvaya (raf, bagaj, tezgâh) yerleştirildiğinde.
    public void PlaceIn(PlacementSlot slot)
    {
        LeaveSlot();
        IsHeld = false;
        MakeKinematic();
        SetCollidersEnabled(true); // Yuvadayken tekrar alınabilmesi için açık.
        transform.SetParent(slot.transform, false);
        transform.localPosition = new Vector3(0f, PivotHeight, 0f);
        transform.localRotation = Quaternion.identity;
        CurrentSlot = slot;
    }

    /// Yere bırakıldığında / fırlatıldığında.
    public void Drop(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        LeaveSlot();
        IsHeld = false;
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position, rotation);
        SetCollidersEnabled(true);
        rb.isKinematic = false;
        SetVelocity(velocity);
    }

    void LeaveSlot()
    {
        if (CurrentSlot == null) return;
        var slot = CurrentSlot;
        CurrentSlot = null;
        slot.NotifyRemoved(this);
    }

    void MakeKinematic()
    {
        if (!rb.isKinematic) SetVelocity(Vector3.zero);
        rb.isKinematic = true;
    }

    void SetCollidersEnabled(bool enabled)
    {
        foreach (var c in colliders) if (c != null) c.enabled = enabled;
    }

    // ---------- Vurgulama ----------

    Renderer[] renderers;
    MaterialPropertyBlock mpb;

    public void SetHighlighted(bool on)
    {
        if (renderers == null) renderers = GetComponentsInChildren<Renderer>();
        if (mpb == null) mpb = new MaterialPropertyBlock();

        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!on) { r.SetPropertyBlock(null); continue; }

            var m = r.sharedMaterial;
            Color baseColor = Color.white;
            if (m != null && m.HasProperty("_BaseColor")) baseColor = m.GetColor("_BaseColor");
            else if (m != null && m.HasProperty("_Color")) baseColor = m.GetColor("_Color");

            Color c = Color.Lerp(baseColor, Color.white, 0.45f);
            mpb.Clear();
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
        }
    }

    void SetVelocity(Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
        rb.angularVelocity = Vector3.zero;
    }
}
