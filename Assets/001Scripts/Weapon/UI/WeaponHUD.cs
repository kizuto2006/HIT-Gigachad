using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD hiển thị thông tin weapon và XP trong game.
/// Sử dụng OnGUI (không cần Canvas prefab) để dễ gắn vào.
/// Hiển thị: XP bar, level, danh sách vũ khí đang trang bị.
/// </summary>
public class WeaponHUD : MonoBehaviour
{
    [Header("── References ──")]
    public XPSystem xpSystem;
    public WeaponInventory weaponInventory;

    [Header("── Display Settings ──")]
    [Tooltip("Bật/tắt HUD")]
    public bool showHUD = true;

    [Tooltip("Vị trí HUD: 0 = bottom-center, 1 = top-left, 2 = top-right")]
    public int hudPosition = 0;

    // GUI Styles
    private GUIStyle boxStyle;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    private GUIStyle weaponStyle;
    private bool stylesInitialized;

    void OnGUI()
    {
        if (!showHUD) return;

        InitStyles();

        float panelWidth = 320f;
        float panelHeight = 120f;
        float margin = 10f;

        Rect panelRect;
        switch (hudPosition)
        {
            case 1: // top-left
                panelRect = new Rect(margin, margin, panelWidth, panelHeight);
                break;
            case 2: // top-right
                panelRect = new Rect(Screen.width - panelWidth - margin, margin, panelWidth, panelHeight);
                break;
            case 0: // bottom-center
            default:
                panelRect = new Rect(
                    (Screen.width - panelWidth) * 0.5f,
                    Screen.height - panelHeight - margin,
                    panelWidth, panelHeight);
                break;
        }

        GUI.Box(panelRect, GUIContent.none, boxStyle);
        GUILayout.BeginArea(new Rect(panelRect.x + 10, panelRect.y + 6, panelWidth - 20, panelHeight - 12));

        // XP & Level header
        if (xpSystem != null)
        {
            GUILayout.Label($"⚡ Level {xpSystem.CurrentLevel}", headerStyle);

            // XP Bar
            Rect barRect = GUILayoutUtility.GetRect(panelWidth - 30, 14);
            GUI.DrawTexture(barRect, MakeTex(1, 1, new Color(0.15f, 0.15f, 0.15f, 0.9f)));

            float fill = xpSystem.XPProgress;
            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * fill, barRect.height);
            GUI.DrawTexture(fillRect, MakeTex(1, 1, new Color(0.2f, 0.8f, 1f)));

            GUI.Label(barRect, $" {xpSystem.CurrentXP} / {xpSystem.XPToNextLevel} XP", labelStyle);
        }

        GUILayout.Space(4);

        // Weapon slots
        if (weaponInventory != null)
        {
            GUILayout.BeginHorizontal();
            foreach (WeaponBehaviour wb in weaponInventory.EquippedWeapons)
            {
                if (wb == null || wb.data == null) continue;

                Color rarityCol = UpgradeUI.GetRarityColor(wb.data.rarity);
                string colorHex = ColorUtility.ToHtmlStringRGB(rarityCol);

                GUILayout.Label(
                    $"<color=#{colorHex}>[{wb.data.weaponName} Lv.{wb.CurrentLevel}]</color>",
                    weaponStyle);
            }

            // Empty slots
            int emptySlots = weaponInventory.maxSlots - weaponInventory.SlotCount;
            for (int i = 0; i < emptySlots; i++)
            {
                GUILayout.Label("<color=#555555>[  —  ]</color>", weaponStyle);
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndArea();
    }

    private void InitStyles()
    {
        if (stylesInitialized) return;

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.7f)) },
            padding = new RectOffset(8, 8, 6, 6)
        };

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.2f, 0.85f, 1f) },
            alignment = TextAnchor.MiddleLeft
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = Color.white }
        };

        weaponStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            richText = true,
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(2, 2, 0, 0)
        };

        stylesInitialized = true;
    }

    private Texture2D MakeTex(int w, int h, Color col)
    {
        Color[] pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D tex = new Texture2D(w, h);
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }
}
