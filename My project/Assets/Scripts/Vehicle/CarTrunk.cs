using UnityEngine;

/// <summary>
/// Arabanın bagaj kapağı: [E] ile açılır / kapanır. Bu bileşen menteşe noktasındaki nesneye eklenir,
/// kapak mesh'leri onun çocuğudur. Kapak kapalıyken bagaj yuvalarına ulaşılamaz.
/// </summary>
public class CarTrunk : MonoBehaviour, IInteractable
{
    [Tooltip("Açılma açısı (yerel x ekseni etrafında; artı değer arka kenarı kaldırır).")]
    public float openAngle = 72f;
    public float openSpeed = 150f; // derece/saniye
    public PlacementSlot[] slots;

    bool isOpen;
    float currentAngle;
    Quaternion closedRotation;

    public bool IsOpen => isOpen;

    void Awake()
    {
        closedRotation = transform.localRotation;
        SetSlotsReachable(false);
    }

    public bool CanInteract(PlayerInteractor p) => true;

    public string GetPrompt(PlayerInteractor p) => isOpen ? "[E] Bagajı kapat" : "[E] Bagajı aç";

    public void Interact(PlayerInteractor p)
    {
        isOpen = !isOpen;
        SetSlotsReachable(isOpen);
    }

    void SetSlotsReachable(bool on)
    {
        if (slots == null) return;
        foreach (var s in slots)
            if (s != null && s.TryGetComponent<Collider>(out var c)) c.enabled = on;
    }

    void Update()
    {
        float target = isOpen ? openAngle : 0f;
        if (Mathf.Approximately(currentAngle, target)) return;
        currentAngle = Mathf.MoveTowards(currentAngle, target, openSpeed * Time.deltaTime);
        transform.localRotation = closedRotation * Quaternion.Euler(currentAngle, 0f, 0f);
    }
}
