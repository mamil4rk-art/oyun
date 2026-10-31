using UnityEngine;

/// <summary>
/// Toptancı rafındaki bir ürün bölümü. Bakıp [E]'ye basınca o üründen yeni bir kutu çıkar:
/// araba itiyorsan doğrudan arabaya, boş elliysen eline. Kutu kasada ödenene kadar "ödenmedi" sayılır.
/// Raftaki kutular sadece görüntü; stok şimdilik sınırsız.
/// </summary>
public class ShelfProduct : MonoBehaviour, IInteractable
{
    public ItemData item;
    [Tooltip("Pasif kutu şablonu; her alışta bunun kopyası çıkar (sahne kurucu oluşturur).")]
    public ItemBox boxTemplate;

    public bool CanInteract(PlayerInteractor p)
    {
        if (item == null || boxTemplate == null || p.HeldItem != null) return false;
        return p.PushedCart == null || p.PushedCart.HasRoom;
    }

    public string GetPrompt(PlayerInteractor p)
    {
        string price = MoneyManager.Format(item.wholesalePrice);
        return p.PushedCart != null
            ? $"[E] {item.displayName} → arabaya ekle   ({price})"
            : $"[E] {item.displayName} al   ({price})";
    }

    public void Interact(PlayerInteractor p)
    {
        var box = Instantiate(boxTemplate, transform.position, Quaternion.identity);
        box.name = "Kutu - " + item.displayName;
        box.SetPaid(false);
        box.gameObject.SetActive(true);

        if (p.PushedCart != null)
        {
            if (p.PushedCart.TryAdd(box))
                GameHUD.ShowMessage($"{item.displayName} arabaya eklendi  ({p.PushedCart.Count}/{p.PushedCart.Capacity})", 1.5f);
            else
            {
                Destroy(box.gameObject);
                GameHUD.ShowMessage("Araba dolu!");
            }
            return;
        }
        p.PickUp(box);
    }
}
