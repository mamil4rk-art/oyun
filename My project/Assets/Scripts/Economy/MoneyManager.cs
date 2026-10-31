using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Oyuncunun kasası. Her para girişi/çıkışı buradan geçer
/// (ileride gün sonu raporu için kayıt da tutacak).
/// </summary>
public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [SerializeField] int startingMoney = 50000;

    public int Money { get; private set; }

    /// (yeni bakiye, değişim miktarı, sebep)
    public event Action<int, int, string> MoneyChanged;

    static readonly CultureInfo TR = new CultureInfo("tr-TR");

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Money = startingMoney;
    }

    public void Add(int amount, string reason)
    {
        if (amount <= 0) return;
        Money += amount;
        MoneyChanged?.Invoke(Money, amount, reason);
        GameHUD.ShowMessage($"+{Format(amount)}  {reason}");
    }

    /// Para yetiyorsa harcar ve true döner; yetmiyorsa hiçbir şey yapmaz.
    public bool TrySpend(int amount, string reason)
    {
        if (amount < 0) return false;
        if (Money < amount)
        {
            GameHUD.ShowMessage("Yetersiz bakiye!");
            return false;
        }
        Money -= amount;
        MoneyChanged?.Invoke(Money, -amount, reason);
        GameHUD.ShowMessage($"-{Format(amount)}  {reason}");
        return true;
    }

    public static string Format(int amount) => amount.ToString("N0", TR) + " TL";
}
