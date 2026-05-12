using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UnitSpawnButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public GameObject unitPrefab;
    public string unitType;
    public Color teamColor = Color.white;
    public Color32 unitColor;

    private GameObject mDraggedUnit;
    private Canvas mCanvas;

    void Start()
    {
        mCanvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (BasePiece.sBattleStarted) return;   // ← добавить
        mDraggedUnit = Instantiate(unitPrefab, mCanvas.transform);
        mDraggedUnit.transform.position = eventData.position;
        mDraggedUnit.transform.SetAsLastSibling();

        // ВАЖНО: делаем видимым
        CanvasGroup cg = mDraggedUnit.GetComponent<CanvasGroup>();
        if (cg == null) cg = mDraggedUnit.AddComponent<CanvasGroup>();
        cg.alpha = 1f;           // ПОЛНАЯ ВИДИМОСТЬ
        cg.blocksRaycasts = false;

        // Красим в нужный цвет
        Image img = mDraggedUnit.GetComponent<Image>();
        if (img != null)
        {
            img.color = unitColor;
            img.raycastTarget = false;
        }

        // Размер
        RectTransform rt = mDraggedUnit.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(70, 70);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (BasePiece.sBattleStarted) return;   // ← добавить
        if (mDraggedUnit != null)
            mDraggedUnit.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (BasePiece.sBattleStarted) return;   // ← добавить
        if (PieceManager.IsBattleActive) return;
        if (mDraggedUnit != null)
        {
            Cell targetCell = FindCellUnderMouse();

            if (targetCell != null && targetCell.mCurrentPiece == null)
            {
                // Проверка стороны
                if (teamColor == Color.white && targetCell.mBoardPosition.y >= 5)
                {
                    Debug.Log("Нельзя ставить на вражескую половину!");
                }
                else
                {
                    SpawnUnit(targetCell);
                }
            }

            Destroy(mDraggedUnit);
        }
    }

    private Cell FindCellUnderMouse()
    {
        Cell[] cells = FindObjectsOfType<Cell>();
        foreach (Cell cell in cells)
        {
            if (cell.mRectTransform != null &&
                RectTransformUtility.RectangleContainsScreenPoint(
                cell.mRectTransform, Input.mousePosition))
            {
                return cell;
            }
        }
        return null;
    }

    private void SpawnUnit(Cell cell)
    {
        PieceManager pm = FindObjectOfType<PieceManager>();
        if (pm != null)
        {
            System.Type type = null;

            switch (unitType)
            {
                case "Knight": type = typeof(Knight); break;
                case "Archer": type = typeof(Archer); break;
                case "Mage": type = typeof(Mage); break;
            }

            if (type != null)
            {
                pm.SpawnUnit(type, teamColor, unitColor, cell.mBoardPosition, true);
            }
        }
    }
}