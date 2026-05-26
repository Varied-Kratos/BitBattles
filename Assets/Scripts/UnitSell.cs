using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SellZone : MonoBehaviour, IDropHandler
{
    public Color highlightColor = new Color(1f, 0.3f, 0.3f, 0.8f);
    private Color normalColor;
    private Image mImage;

    void Start()
    {
        mImage = GetComponent<Image>();
        if (mImage != null)
            normalColor = mImage.color;
    }

    public void OnDrop(PointerEventData eventData)
    {
        // Получаем перетаскиваемый объект
        GameObject droppedObject = eventData.pointerDrag;
        if (droppedObject != null)
        {
            BasePiece piece = droppedObject.GetComponent<BasePiece>();
            if (piece != null)
            {
                SellPiece(piece);
            }
        }
    }

    private void SellPiece(BasePiece piece)
    {
        int refund = piece.GetSellCost();

        PieceManager pm = FindFirstObjectByType<PieceManager>();
        if (pm != null)
        {
            pm.RefundElixir(refund);
            Debug.Log($"{piece.name} продан! +{refund} эликсира (уровень {piece.level})");
        }

        if (piece.mCurrentCell != null)
            piece.mCurrentCell.mCurrentPiece = null;

        if (pm != null)
            pm.mMyMinis.Remove(piece);
        AudioManager.Instance.PlaySell(); // ← ЗВУК ПРОДАЖИ
        Destroy(piece.gameObject);
    }

    // Подсветка при наведении (опционально)
    void OnPointerEnter()
    {
        if (mImage != null)
            mImage.color = highlightColor;
    }

    void OnPointerExit()
    {
        if (mImage != null)
            mImage.color = normalColor;
    }
}