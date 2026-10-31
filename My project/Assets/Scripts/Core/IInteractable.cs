/// <summary>
/// Oyuncunun bakıp [E] ile etkileşebildiği her şey bu arayüzü uygular:
/// kutular, raf yuvaları, bagaj, tezgâh, kasa, dükkân bilgisayarı vb.
/// </summary>
public interface IInteractable
{
    /// Şu an etkileşim mümkün mü? (Örn. elin doluyken kutu alamazsın.)
    bool CanInteract(PlayerInteractor interactor);

    /// Ekranda gösterilecek ipucu metni. Örn: "[E] Kutuyu al"
    string GetPrompt(PlayerInteractor interactor);

    /// [E]'ye basıldığında çalışır.
    void Interact(PlayerInteractor interactor);
}
