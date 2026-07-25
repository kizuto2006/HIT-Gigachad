using UnityEngine;

/// <summary>
/// Gắn lên Player GameObject cùng chỗ với PlayerHealth.
/// Hiển thị stats computed (Final*) của PlayerBaseStats realtime trong Inspector và Game view.
/// </summary>
public class StatsDisplay : MonoBehaviour
{
    [Header("── References ──")]
    public PlayerBaseStats stats;
    public PlayerHealth health;

    [Header("── Display Settings ──")]
    [Tooltip("Bật/tắt hiển thị panel stats trên màn hình Game")]
    public bool showOnScreen = false;

    [Header("── Runtime Stats (Chỉ xem) ──")]
    [SerializeField] private string characterName;
    [SerializeField] private float currentHp;
    [SerializeField] private float maxHp;
    [SerializeField] private float finalAtk;
    [SerializeField] private float finalSpeed;
    [SerializeField] private float finalJumpHeight;
    [SerializeField] private float finalProjSpeed;
    [SerializeField] private int finalProjCount;

    // GUI styling
    private GUIStyle boxStyle;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    private bool stylesInitialized;

    void Start()
    {
        InvokeRepeating(nameof(RefreshInspectorStats), 0f, 0.5f);
    }

    /// <summary>
    /// Cập nhật các field SerializeField để hiển thị trong Inspector (mỗi 0.5s).
    /// </summary>
    private void RefreshInspectorStats()
    {
        if (stats == null) return;

        characterName = (stats.characterData != null) ? stats.characterData.characterName : "N/A";
        maxHp = stats.FinalHp;
        finalAtk = stats.FinalAtk;
        finalSpeed = stats.FinalSpeed;
        finalJumpHeight = stats.FinalJumpHeight;
        finalProjSpeed = stats.FinalProjSpeed;
        finalProjCount = stats.FinalProjCount;

        if (health != null)
        {
            currentHp = health.currentHp;
        }
        else
        {
            currentHp = maxHp;
        }
    }

    private void InitStyles()
    {
        if (stylesInitialized) return;

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.75f)) },
            padding = new RectOffset(12, 12, 8, 8)
        };

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.85f, 0.2f) }, // vàng gold
            alignment = TextAnchor.MiddleLeft
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            normal = { textColor = Color.white },
            richText = true
        };

        stylesInitialized = true;
    }

    void OnGUI()
    {
        if (!showOnScreen || stats == null) return;

        InitStyles();

        float panelWidth = 260f;
        float panelHeight = 220f;
        float margin = 10f;

        Rect panelRect = new Rect(margin, margin, panelWidth, panelHeight);

        GUI.Box(panelRect, GUIContent.none, boxStyle);

        GUILayout.BeginArea(new Rect(panelRect.x + 12, panelRect.y + 8, panelRect.width - 24, panelRect.height - 16));

        // Header: tên nhân vật
        string name = (stats.characterData != null) ? stats.characterData.characterName : "Unknown";
        GUILayout.Label($"⚔ {name}", headerStyle);
        GUILayout.Space(4);

        // HP bar
        float hp = (health != null) ? health.currentHp : stats.FinalHp;
        float hpMax = stats.FinalHp;
        DrawStatLine("HP", hp, hpMax, new Color(0.2f, 0.9f, 0.3f));

        GUILayout.Space(4);

        // Các stats khác
        GUILayout.Label($"<color=#FF9966>ATK:</color>  {stats.FinalAtk:F1}", labelStyle);
        GUILayout.Label($"<color=#66FFCC>SPD:</color>  {stats.FinalSpeed:F1}", labelStyle);
        GUILayout.Label($"<color=#99CCFF>JUMP:</color> {stats.FinalJumpHeight:F1}", labelStyle);
        GUILayout.Label($"<color=#CCCCFF>Proj:</color>  {stats.FinalProjCount}  |  <color=#CCCCFF>ProjSpd:</color> {stats.FinalProjSpeed:F1}", labelStyle);

        GUILayout.EndArea();
    }

    private void DrawStatLine(string label, float current, float max, Color barColor)
    {
        GUILayout.BeginHorizontal();

        // Label
        GUILayout.Label($"<color=#{ColorUtility.ToHtmlStringRGB(barColor)}>{label}:</color>", labelStyle, GUILayout.Width(55));

        // Bar background
        Rect barRect = GUILayoutUtility.GetRect(150, 16);
        GUI.DrawTexture(barRect, MakeTex(1, 1, new Color(0.2f, 0.2f, 0.2f, 0.8f)));

        // Bar fill
        if (max > 0f)
        {
            float fill = Mathf.Clamp01(current / max);
            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * fill, barRect.height);
            GUI.DrawTexture(fillRect, MakeTex(1, 1, barColor));
        }

        // Text overlay
        GUI.Label(barRect, $" {current:F0} / {max:F0}", labelStyle);

        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// Tạo texture 1 màu dùng cho GUIStyle background.
    /// </summary>
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
