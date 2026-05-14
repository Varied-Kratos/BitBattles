using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class DraftManager : MonoBehaviour
{
    [System.Serializable]
    public class DraftSlot
    {
        public Button button;
        public Image icon;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI costText;
        [HideInInspector] public System.Type unitType;
    }

    public DraftSlot[] slots = new DraftSlot[3];
    public PieceManager pieceManager;
    public GameObject dragPrefab; // Префаб для перетаскивания (простой Image)

    private System.Type[] unitTypes = { typeof(Knight), typeof(Archer), typeof(Mage) };
    private GameObject mDraggedUnit;
    private System.Type mDraggedType;

    void Start()
    {
        gameObject.SetActive(false);

        // Добавляем Drag-обработчики на каждый слот
        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;

            // Добавляем EventTrigger для драга
            EventTrigger trigger = slots[i].button.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry beginDrag = new EventTrigger.Entry();
            beginDrag.eventID = EventTriggerType.BeginDrag;
            beginDrag.callback.AddListener((data) => OnBeginDragSlot(index));
            trigger.triggers.Add(beginDrag);

            EventTrigger.Entry drag = new EventTrigger.Entry();
            drag.eventID = EventTriggerType.Drag;
            drag.callback.AddListener((data) => OnDragSlot((PointerEventData)data));
            trigger.triggers.Add(drag);

            EventTrigger.Entry endDrag = new EventTrigger.Entry();
            endDrag.eventID = EventTriggerType.EndDrag;
            endDrag.callback.AddListener((data) => OnEndDragSlot((PointerEventData)data));
            trigger.triggers.Add(endDrag);
        }
    }

    public void RefreshDraft()
    {
        gameObject.SetActive(true);

        for (int i = 0; i < slots.Length; i++)
        {
            System.Type type = unitTypes[Random.Range(0, unitTypes.Length)];
            slots[i].unitType = type;

            int cost = pieceManager.GetUnitCost(type);

            if (slots[i].icon != null)
                slots[i].icon.color = GetColorForType(type);
            if (slots[i].nameText != null)
                slots[i].nameText.text = type.Name;
            if (slots[i].costText != null)
                slots[i].costText.text = $"💰{cost}";
        }
    }

    // КЛИК — быстрый спавн
    public void OnSlotClicked(int index)
    {
        if (BasePiece.sBattleStarted) return;
        System.Type type = slots[index].unitType;
        pieceManager.PurchaseUnit(type);
        RefreshDraft();
    }

    // DRAG — начало перетаскивания
    private void OnBeginDragSlot(int index)
    {
        if (BasePiece.sBattleStarted) return;

        mDraggedType = slots[index].unitType;

        // Создаём визуал для перетаскивания
        mDraggedUnit = Instantiate(dragPrefab, transform.parent);
        mDraggedUnit.transform.position = Input.mousePosition;

        Image img = mDraggedUnit.GetComponent<Image>();
        if (img != null)
        {
            img.color = GetColorForType(mDraggedType);
            img.raycastTarget = false;
        }

        CanvasGroup cg = mDraggedUnit.GetComponent<CanvasGroup>();
        if (cg == null) cg = mDraggedUnit.AddComponent<CanvasGroup>();
        cg.alpha = 0.8f;
        cg.blocksRaycasts = false;
    }

    // DRAG — движение
    private void OnDragSlot(PointerEventData eventData)
    {
        if (mDraggedUnit != null)
            mDraggedUnit.transform.position = eventData.position;
    }

    // DRAG — отпускание
    private void OnEndDragSlot(PointerEventData eventData)
    {
        if (mDraggedUnit != null)
        {
            Destroy(mDraggedUnit);

            // Ищем клетку под мышью
            Cell targetCell = FindCellUnderMouse();

            if (targetCell != null && targetCell.mCurrentPiece == null)
            {
                // Проверяем, что это половина игрока
                if (targetCell.mBoardPosition.y < 5)
                {
                    // Покупаем и ставим на эту клетку
                    pieceManager.PurchaseUnitAtCell(mDraggedType, targetCell);
                    RefreshDraft();
                }
            }
        }
    }

    private Cell FindCellUnderMouse()
    {
        Board board = pieceManager.mBoard;
        if (board == null) return null;

        foreach (Cell cell in board.mAllCells)
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

    public void SkipDraft()
    {
        if (BasePiece.sBattleStarted) return;

        if (pieceManager.SpendElixir(1))
            RefreshDraft();
        else
            Debug.Log("Недостаточно эликсира для пропуска!");
    }

    private Color GetColorForType(System.Type type)
    {
        if (type == typeof(Knight)) return new Color(0.31f, 0.49f, 0.62f);
        if (type == typeof(Archer)) return new Color(0.31f, 0.78f, 0.39f);
        return new Color(0.78f, 0.31f, 0.78f);
    }

    public void Hide() => gameObject.SetActive(false);
    public void Show() => gameObject.SetActive(true);
}