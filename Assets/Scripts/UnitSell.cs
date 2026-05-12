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
        if (BasePiece.sBattleStarted) return;

        int refund = Mathf.Max(1, piece.cost - 1);
        PieceManager pm = FindObjectOfType<PieceManager>();
        if (pm != null)
        {
            pm.RefundElixir(refund);
        }

        Debug.Log($"{piece.name} продан! +{refund} эликсира");

        // Убираем юнита
        if (piece.mCurrentCell != null)
            piece.mCurrentCell.mCurrentPiece = null;

        PieceManager pmRef = FindObjectOfType<PieceManager>();
        if (pmRef != null)
            pmRef.mMyMinis.Remove(piece);

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