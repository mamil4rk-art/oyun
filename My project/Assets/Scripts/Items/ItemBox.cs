using UnityEngine;

/// <summary>
/// İçinde bir ürün (bileşen, konsol vb.) olan karton kutu.
/// Toptancıdan alınan her şey önce kutu olarak gelir.
/// </summary>
public class ItemBox : Carryable
{
    [SerializeField] ItemData item;
    public ItemData Item => item;

    [Tooltip("Toptancı rafından alınıp kasada henüz ödenmemiş kutular false")]
    [SerializeField] bool paid = true;
    public bool IsPaid => paid;

    public override string DisplayName => item != null
        ? $"{item.displayName} (kutu){(paid ? "" : " — ödenmedi")}"
        : base.DisplayName;

    /// Kod içinden kutuyu doldurmak için (toptancı, sahne kurucu vb.).
    public void Setup(ItemData data) => item = data;

    public void SetPaid(bool value) => paid = value;
}
