using UnityEngine;

/// <summary>
/// Oyuncu yaklaşınca iki yana kayarak açılan cam kapı.
/// Bu nesnenin yerel -z yönü binanın içidir. Oyuncu içerideyken elinde ya da arabasında
/// ödenmemiş ürün varsa kapı açılmaz ve güvenlik kapısı kırmızı yanar.
/// </summary>
public class AutomaticDoor : MonoBehaviour
{
    public Transform leftPanel, rightPanel;
    public float slideDistance = 1.95f;
    public float sensorRadius = 3.5f;
    public float speed = 2.2f;
    public bool blockUnpaid = true;
    [Tooltip("Güvenlik kapısı ışıkları (normalde yeşil, ödenmemiş ürünle kırmızı yanıp söner)")]
    public Renderer[] alarmLights;

    Vector3 leftClosed, rightClosed;
    float open01;
    float nextWarn;
    PlayerInteractor player;
    MaterialPropertyBlock mpb;

    static readonly Color Green = new Color(0.2f, 1f, 0.35f);
    static readonly Color Red = new Color(1f, 0.15f, 0.1f);

    void Start()
    {
        player = FindAnyObjectByType<PlayerInteractor>();
        if (leftPanel != null) leftClosed = leftPanel.localPosition;
        if (rightPanel != null) rightClosed = rightPanel.localPosition;
        mpb = new MaterialPropertyBlock();
        SetAlarm(Green);
    }

    public static bool HasUnpaid(PlayerInteractor p)
        => (p.HeldItem is ItemBox b && !b.IsPaid) || (p.PushedCart != null && p.PushedCart.UnpaidCount > 0);

    void Update()
    {
        bool want = false, blocked = false;
        if (player != null)
        {
            Vector3 local = transform.InverseTransformPoint(player.transform.position);
            bool near = new Vector2(local.x, local.z).magnitude < sensorRadius;
            bool inside = local.z < 0f;
            if (near)
            {
                if (inside && blockUnpaid && HasUnpaid(player)) blocked = true;
                else want = true;
            }
        }

        if (blocked)
        {
            if (Time.time >= nextWarn)
            {
                GameHUD.ShowMessage("Ödenmemiş ürünler var! Önce kasada öde.", 2.5f);
                nextWarn = Time.time + 3f;
            }
            SetAlarm(Mathf.Repeat(Time.time, 0.5f) < 0.25f ? Red : Red * 0.3f);
        }
        else SetAlarm(Green);

        open01 = Mathf.MoveTowards(open01, want ? 1f : 0f, speed * Time.deltaTime);
        float s = Mathf.SmoothStep(0f, 1f, open01) * slideDistance;
        if (leftPanel != null) leftPanel.localPosition = leftClosed + Vector3.left * s;
        if (rightPanel != null) rightPanel.localPosition = rightClosed + Vector3.right * s;
    }

    void SetAlarm(Color c)
    {
        if (alarmLights == null) return;
        mpb.Clear();
        mpb.SetColor("_BaseColor", c);
        mpb.SetColor("_EmissionColor", c * 2f);
        foreach (var r in alarmLights) if (r != null) r.SetPropertyBlock(mpb);
    }
}
