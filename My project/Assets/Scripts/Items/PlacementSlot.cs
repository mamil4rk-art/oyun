using System;
using UnityEngine;

/// <summary>
/// Bir eşyanın konulabileceği yer: bagaj bölmesi, raf gözü, tamir tezgâhı...
/// Görünmez bir tetik kutusudur; elinde eşya varken bakıp [E]'ye basınca eşya buraya oturur.
/// NOT: Yuva nesnesinin ölçeği (scale) 1,1,1 olmalı; görsel gövdeyi ayrı bir çocuk nesne yap.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class PlacementSlot : MonoBehaviour, IInteractable
{
    public string slotName = "Yer";

    [Header("Ne kabul eder?")]
    public bool acceptAll = true;
    public ItemCategory[] acceptedCategories = Array.Empty<ItemCategory>();

    public Carryable Occupant { get; private set; }
    public bool IsEmpty => Occupant == null;

    /// İleride: bagajdaki kutu sayısı, rafa konan ürüne etiket açma vb. bunları dinleyecek.
    public event Action<PlacementSlot, Carryable> ItemPlaced;
    public event Action<PlacementSlot, Carryable> ItemRemoved;

    void Reset()
    {
        var bc = GetComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(0.5f, 0.35f, 0.5f);
        bc.center = new Vector3(0f, 0.175f, 0f);
    }

    void Awake() => GetComponent<BoxCollider>().isTrigger = true;

    public bool Accepts(Carryable c)
    {
        if (c == null) return false;
        if (acceptAll) return true;
        if (c is ItemBox box && box.Item != null)
            return Array.IndexOf(acceptedCategories, box.Item.category) >= 0;
        return false;
    }

    // ---------- IInteractable ----------

    // Yuva doluysa ışın içindeki eşya yerine yuvaya çarpabilir; o yüzden
    // dolu yuvaya bakınca etkileşimi içindeki eşyaya devrediyoruz (al vb.).

    public bool CanInteract(PlayerInteractor p)
    {
        if (!IsEmpty) return Occupant.CanInteract(p);
        return p.HeldItem != null && Accepts(p.HeldItem);
    }

    public string GetPrompt(PlayerInteractor p)
    {
        if (!IsEmpty) return Occupant.GetPrompt(p);
        return $"[E] {p.HeldItem.DisplayName} → {slotName}";
    }

    public void Interact(PlayerInteractor p)
    {
        if (!IsEmpty) { Occupant.Interact(p); return; }

        var item = p.ReleaseHeldItem();
        if (!TryPlace(item) && item != null) p.PickUp(item);
    }

    /// Eşyayı oyuncu olmadan doğrudan yerleştirir (örn. raftan alışveriş arabasına).
    public bool TryPlace(Carryable item)
    {
        if (item == null || !IsEmpty || !Accepts(item)) return false;
        item.PlaceIn(this);
        Occupant = item;
        ItemPlaced?.Invoke(this, item);
        return true;
    }

    /// Carryable yuvadan alındığında kendisi çağırır.
    public void NotifyRemoved(Carryable c)
    {
        if (Occupant != c) return;
        Occupant = null;
        ItemRemoved?.Invoke(this, c);
    }

    void OnDrawGizmos()
    {
        var bc = GetComponent<BoxCollider>();
        if (bc == null) return;
        Gizmos.color = IsEmpty ? new Color(0.2f, 0.9f, 0.4f, 0.6f) : new Color(0.9f, 0.5f, 0.2f, 0.6f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(bc.center, bc.size);
    }
}
