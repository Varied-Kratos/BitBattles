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
    public GameObject dragPrefab;

    private System.Type[] unitTypes = { typeof(Knight), typeof(Archer), typeof(Mage) };
    private GameObject mDraggedUnit;
    private System.Type mDraggedType;

    void Awake()
    {
        // СРАЗУ показываем и заполняем панель
        gameObject.SetActive(true);
        RefreshDraft();

        // Добавляем Drag-обработчики
        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;

            EventTrigger trigger = slots[i].button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = slots[i].button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

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

            // КЛИК — быстрый спавн
            slots[i].button.onClick.RemoveAllListeners();
            int idx = i;
            slots[i].button.onClick.AddListener(() => OnSlotClicked(idx));
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
                slots[i].nameText.text = TranslateType(type);
            if (slots[i].costText != null)
                slots[i].costText.text = $"${cost}";
        }
    }

    private string TranslateType(System.Type type)
    {
        if (type == typeof(Knight)) return "Рыцарь";
        if (type == typeof(Archer)) return "Лучник";
        if (type == typeof(Mage)) return "Маг";
        return type.Name;
    }

    private void OnSlotClicked(int index)
    {
        if (BasePiece.sBattleStarted) return;
        System.Type type = slots[index].unitType;
        pieceManager.PurchaseUnit(type);
        RefreshDraft();
    }

    private void OnBeginDragSlot(int index)
    {
        if (BasePiece.sBattleStarted) return;

        mDraggedType = slots[index].unitType;

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

    private void OnDragSlot(PointerEventData eventData)
    {
        if (mDraggedUnit != null)
            mDraggedUnit.transform.position = eventData.position;
    }

    private void OnEndDragSlot(PointerEventData eventData)
    {
        if (mDraggedUnit != null)
        {
            Destroy(mDraggedUnit);

            Cell targetCell = FindCellUnderMouse();

            if (targetCell != null && targetCell.mCurrentPiece == null)
            {
                if (targetCell.mBoardPosition.y < 5)
                {
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
            Debug.Log("Недостаточно эликсира!");
    }

    private Color GetColorForType(System.Type type)
    {
        if (type == typeof(Knight)) return new Color(0.31f, 0.49f, 0.62f);
        if (type == typeof(Archer)) return new Color(0.31f, 0.78f, 0.39f);
        return new Color(0.78f, 0.31f, 0.78f);
    }

    public void Hide() => gameObject.SetActive(false);
    public void Show()
    {
        gameObject.SetActive(true);
        RefreshDraft();
    }
}