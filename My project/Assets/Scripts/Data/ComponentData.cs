using UnityEngine;

public enum ComponentType { CPU, Motherboard, RAM, GPU, PSU, Storage, Case, CPUCooler }

/// İşlemci ile anakartın uyumu bununla kontrol edilecek (kurgusal soket adları).
public enum SocketType { None, S100, S200 }

public enum MemoryType { None, DDR4, DDR5 }

/// <summary>
/// Bilgisayar bileşeni verisi. Toplama aşamasında uyumluluk
/// (soket, RAM tipi, güç) bu alanlardan kontrol edilecek.
/// </summary>
[CreateAssetMenu(fileName = "YeniBilesen", menuName = "PC Shop/Bilgisayar Bileşeni", order = 1)]
public class ComponentData : ItemData
{
    [Header("Bileşen")]
    public ComponentType componentType;

    [Tooltip("İşlemci ve anakart için")]
    public SocketType socket;

    [Tooltip("RAM ve anakart için")]
    public MemoryType memoryType;

    [Tooltip("Bu parçanın çektiği güç (Watt)")]
    public int powerDraw;

    [Tooltip("Sadece güç kaynağı (PSU) için: verebildiği güç (Watt)")]
    public int psuWattage;

    [Tooltip("Performans puanı: hazır PC'nin değerini ve müşteri isteğini etkiler")]
    public int performanceScore;

    void OnValidate() => category = ItemCategory.Component;
}
