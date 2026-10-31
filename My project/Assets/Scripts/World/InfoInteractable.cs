using UnityEngine;

/// <summary>
/// Henüz yapılmamış özellikler için yer tutucu: bakınca ipucu, [E]'ye basınca mesaj gösterir.
/// (Örn. dükkân bilgisayarı bir sonraki aşamalarda gerçek arayüzüyle gelecek.)
/// </summary>
public class InfoInteractable : MonoBehaviour, IInteractable
{
    public string prompt = "[E] Kullan";
    [TextArea] public string message = "Yakında!";

    public bool CanInteract(PlayerInteractor p) => p.HeldItem == null;
    public string GetPrompt(PlayerInteractor p) => prompt;
    public void Interact(PlayerInteractor p) => GameHUD.ShowMessage(message, 3f);
}
