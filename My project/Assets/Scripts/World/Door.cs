using UnityEngine;

/// <summary>
/// [E] ile açılıp kapanan kapı. Bu bileşen menteşe noktasındaki nesneye eklenir,
/// kapı paneli onun çocuğudur. Elin doluyken de açabilirsin.
/// </summary>
public class Door : MonoBehaviour, IInteractable
{
    public string doorName = "Kapı";
    [Tooltip("Açılma açısı. Eksi değer kapıyı diğer tarafa açar.")]
    public float openAngle = -95f;
    public float openSpeed = 220f; // derece/saniye

    bool isOpen;
    float currentAngle;
    Quaternion closedRotation;

    void Awake() => closedRotation = transform.localRotation;

    public bool CanInteract(PlayerInteractor p) => true;

    public string GetPrompt(PlayerInteractor p) => isOpen ? $"[E] {doorName} kapat" : $"[E] {doorName} aç";

    public void Interact(PlayerInteractor p) => isOpen = !isOpen;

    void Update()
    {
        float target = isOpen ? openAngle : 0f;
        if (Mathf.Approximately(currentAngle, target)) return;
        currentAngle = Mathf.MoveTowards(currentAngle, target, openSpeed * Time.deltaTime);
        transform.localRotation = closedRotation * Quaternion.Euler(0f, currentAngle, 0f);
    }
}
