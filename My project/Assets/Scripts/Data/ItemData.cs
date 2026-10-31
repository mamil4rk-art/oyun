using UnityEngine;

public enum ItemCategory
{
    Component, // Bilgisayar bileşeni
    Console,   // Oyun konsolu
    Computer,  // Toplanmış hazır bilgisayar
    Tool       // Alet (tornavida, test cihazı...)
}

/// <summary>
/// Oyundaki her satılabilir ürünün verisi. Project penceresinde
/// sağ tık > Create > PC Shop > Ürün ile yeni ürün eklenir.
/// Kod yazmadan yeni ürün eklemek için bu sistemi kullanıyoruz.
/// </summary>
[CreateAssetMenu(fileName = "YeniUrun", menuName = "PC Shop/Ürün", order = 0)]
public class ItemData : ScriptableObject
{
    [Header("Genel")]
    public string itemId;
    public string displayName;
    [TextArea] public string description;
    public ItemCategory category;
    public Sprite icon;

    [Header("Fiyat (TL)")]
    [Tooltip("Toptancıdan alış fiyatı")]
    public int wholesalePrice;
    [Tooltip("Piyasa satış fiyatı. Müşteri pazarlıkta buna göre tepki verir.")]
    public int marketPrice;

    [Header("Kutu görünümü (geçici model)")]
    public Vector3 boxSize = new Vector3(0.4f, 0.3f, 0.3f);
    public Color boxColor = new Color(0.76f, 0.6f, 0.42f);
    public float mass = 1f;
}
