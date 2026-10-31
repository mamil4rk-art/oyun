using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Toptancı kasası. Arabayı kasanın yanındaki şeride getir (ya da ürünü elinde getir),
/// kasaya bakıp [E] ile öde. Ödenen kutular "ödendi" olur; artık mağazadan çıkarılabilir.
/// </summary>
public class CheckoutCounter : MonoBehaviour, IInteractable
{
    [Tooltip("Arabanın durması gereken alan (bu nesneye göre yerel)")]
    public Vector3 zoneCenter = new Vector3(1.25f, 1f, 0.3f);
    public Vector3 zoneSize = new Vector3(2f, 2.5f, 5.5f);
    [Tooltip("Müşteriye dönük ekrandaki yazı")]
    public TextMesh display;

    readonly List<ItemBox> pending = new List<ItemBox>();
    PlayerInteractor player;
    float nextDisplayUpdate;

    void Start() => player = FindAnyObjectByType<PlayerInteractor>();

    bool InZone(Vector3 worldPos)
    {
        Vector3 d = transform.InverseTransformPoint(worldPos) - zoneCenter;
        return Mathf.Abs(d.x) <= zoneSize.x / 2f && Mathf.Abs(d.y) <= zoneSize.y / 2f && Mathf.Abs(d.z) <= zoneSize.z / 2f;
    }

    /// Kasada ödenecek kutuları toplar: şeritteki arabalar + oyuncunun elindeki kutu.
    int Collect(PlayerInteractor p)
    {
        pending.Clear();
        foreach (var cart in ShoppingCart.All)
        {
            if (cart == null || !InZone(cart.transform.position)) continue;
            foreach (var b in cart.Boxes) if (!b.IsPaid) pending.Add(b);
        }
        if (p != null && p.HeldItem is ItemBox held && !held.IsPaid) pending.Add(held);

        int total = 0;
        foreach (var b in pending) if (b.Item != null) total += b.Item.wholesalePrice;
        return total;
    }

    // ---------- IInteractable ----------

    public bool CanInteract(PlayerInteractor p) => true;

    public string GetPrompt(PlayerInteractor p)
    {
        int total = Collect(p);
        if (pending.Count == 0) return "Kasa: arabanı kasanın yanındaki şeride getir";
        return $"[E] Öde: {pending.Count} ürün — {MoneyManager.Format(total)}";
    }

    public void Interact(PlayerInteractor p)
    {
        int total = Collect(p);
        if (pending.Count == 0)
        {
            GameHUD.ShowMessage("Ödenecek ürün yok. Arabanı kasanın yanındaki şeride getir.");
            return;
        }
        if (MoneyManager.Instance == null || !MoneyManager.Instance.TrySpend(total, "Toptancı alışverişi")) return;

        foreach (var b in pending) b.SetPaid(true);
        GameHUD.ShowMessage($"Ödeme tamam: {pending.Count} ürün, {MoneyManager.Format(total)}. Ürünleri aracına yükleyebilirsin.", 4f);
        UpdateDisplay();
    }

    void Update()
    {
        if (display == null || Time.time < nextDisplayUpdate) return;
        nextDisplayUpdate = Time.time + 0.25f;
        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        if (display == null) return;
        int total = Collect(player);
        display.text = pending.Count == 0 ? "HOŞ GELDİNİZ" : $"{pending.Count} ürün\n{MoneyManager.Format(total)}";
    }
}
