using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    private BasePiece mOwner;
    private Image mGreenImage;
    private Image mRedImage;
    private RectTransform mGreenRect;
    private RectTransform mRedRect;
    private float mFullWidth;
    private TextMeshProUGUI mStarsText;
    public void Setup(BasePiece owner)
    {
        mOwner = owner;
        mFullWidth = mOwner.maxHP * 5f; // длина пропорциональна maxHP

        // Чёрная полоска (недостающее HP) — динамическая ширина
        GameObject redObj = new GameObject("RedFill");
        redObj.transform.SetParent(transform, false);
        mRedImage = redObj.AddComponent<Image>();
        mRedImage.color = new Color(0.15f, 0.15f, 0.15f, 1f); // почти чёрный
        mRedRect = redObj.GetComponent<RectTransform>();
        mRedRect.anchorMin = mRedRect.anchorMax = new Vector2(0f, 0.5f);
        mRedRect.pivot = new Vector2(0f, 0.5f);
        mRedRect.sizeDelta = new Vector2(0f, 8); // изначально ширина 0
        mRedRect.anchoredPosition = Vector2.zero; // появится сразу за зелёной (будет задано в Update)

        // Зелёная полоска (текущее HP)
        GameObject greenObj = new GameObject("GreenFill");
        greenObj.transform.SetParent(transform, false);
        mGreenImage = greenObj.AddComponent<Image>();
        mGreenImage.color = Color.green;
        mGreenRect = greenObj.GetComponent<RectTransform>();
        mGreenRect.anchorMin = mGreenRect.anchorMax = new Vector2(0f, 0.5f);
        mGreenRect.pivot = new Vector2(0f, 0.5f);
        mGreenRect.sizeDelta = new Vector2(mFullWidth, 8);
        mGreenRect.anchoredPosition = Vector2.zero; // левый край строго по левому краю контейнера

        // Контейнер HealthBar — по центру юнита, приподнят на 35px
        RectTransform myRect = GetComponent<RectTransform>();
        myRect.sizeDelta = new Vector2(mFullWidth, 8);
        myRect.anchorMin = myRect.anchorMax = new Vector2(0.5f, 0.5f);
        myRect.pivot = new Vector2(0.5f, 0.5f);
        myRect.anchoredPosition = new Vector2(0, 35);

        // Звёзды — КРУПНЫЕ и ЗАМЕТНЫЕ
        GameObject starsObj = new GameObject("Stars");
        starsObj.transform.SetParent(transform, false);
        mStarsText = starsObj.AddComponent<TextMeshProUGUI>();
        mStarsText.fontSize = 18;          // БЫЛО 12, СТАЛО 18
        mStarsText.fontStyle = FontStyles.Bold; // ЖИРНЫЕ
        mStarsText.alignment = TMPro.TextAlignmentOptions.Center;
        mStarsText.color = Color.yellow;

        // Добавляем обводку для контраста
        mStarsText.outlineWidth = 0.3f;
        mStarsText.outlineColor = Color.black;

        RectTransform starsRect = starsObj.GetComponent<RectTransform>();
        starsRect.sizeDelta = new Vector2(mFullWidth, 22); // БЫЛО 14, СТАЛО 22
        starsRect.anchorMin = starsRect.anchorMax = new Vector2(0.5f, 0.5f);
        starsRect.pivot = new Vector2(0.5f, 0.5f);
        starsRect.anchoredPosition = new Vector2(0, 18); // ВЫШЕ над HealthBar

        UpdateStars();
    }

    void Update()
    {
        if (mOwner == null || !mOwner.gameObject.activeSelf)
        {
            Destroy(gameObject);
            return;
        }

        float healthPercent = (float)mOwner.currentHP / mOwner.maxHP;
        healthPercent = Mathf.Clamp01(healthPercent);

        // Зелёная полоска — текущее HP
        float greenWidth = mFullWidth * healthPercent;
        mGreenRect.sizeDelta = new Vector2(greenWidth, 8);

        // Чёрная полоска — недостающее HP
        float redWidth = mFullWidth - greenWidth;
        mRedRect.sizeDelta = new Vector2(redWidth, 8);
        // Размещаем чёрную сразу за зелёной
        mRedRect.anchoredPosition = new Vector2(greenWidth, 0f);

        // Цвет зелёной части
        if (healthPercent > 0.6f)
            mGreenImage.color = Color.green;
        else if (healthPercent > 0.3f)
            mGreenImage.color = Color.yellow;
        else
            mGreenImage.color = Color.red;
        UpdateStars();
    }

    private void UpdateStars()
    {
        if (mStarsText != null && mOwner != null)
        {
            string stars = "";
            for (int i = 0; i < mOwner.level; i++)
                stars += "S";  // ← МЕНЯЕМ ★ НА S

            mStarsText.text = stars;
            mStarsText.fontSize = 22;
            mStarsText.fontStyle = FontStyles.Bold;
            mStarsText.outlineWidth = 0.4f;
            mStarsText.outlineColor = Color.black;

            if (mOwner.level == 1) mStarsText.color = Color.gray;
            else if (mOwner.level == 2) mStarsText.color = Color.yellow;
            else mStarsText.color = new Color(1f, 0.5f, 0f);
        }
    }
}