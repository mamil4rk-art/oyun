using UnityEngine;

/// <summary>
/// 3D tabela yazılarının duvarların arkasından görünmemesi için
/// yazıya derinliği dikkate alan bir malzeme verir.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(TextMesh))]
public class SignText : MonoBehaviour
{
    [SerializeField] Shader textShader;

    Material mat;
    TextMesh textMesh;

    public void SetShader(Shader shader)
    {
        textShader = shader;
        Apply();
    }

    void OnEnable()
    {
        Font.textureRebuilt += OnFontTextureRebuilt;
        Apply();
    }

    void OnDisable()
    {
        Font.textureRebuilt -= OnFontTextureRebuilt;
        if (mat != null)
        {
            if (Application.isPlaying) Destroy(mat); else DestroyImmediate(mat);
            mat = null;
        }
    }

    void OnFontTextureRebuilt(Font f)
    {
        if (textMesh != null && textMesh.font == f) Apply();
    }

    void Apply()
    {
        if (textMesh == null) textMesh = GetComponent<TextMesh>();
        if (textMesh == null || textMesh.font == null || textShader == null) return;

        if (mat == null) mat = new Material(textShader) { hideFlags = HideFlags.DontSave };
        mat.mainTexture = textMesh.font.material.mainTexture;

        var r = GetComponent<MeshRenderer>();
        if (r.sharedMaterial != mat) r.sharedMaterial = mat;
    }
}
