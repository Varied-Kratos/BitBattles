using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// New
public enum CellState
{
    None,
    Friendly,
    Enemy,
    Free,
    OutOfBounds
}

public class Board : MonoBehaviour
{
    public GameObject mCellPrefab;

    // Для Clash Mini поле 5 в ширину и 10 в длину (всего 50)
    [HideInInspector]
    public Cell[,] mAllCells = new Cell[5, 10];
    public GameObject mFullBackground; // Перетащи в инспекторе
    public void Create()
    {
        mAllCells = new Cell[5, 10]; 

        #region Create
        for (int y = 0; y < 10; y++) // 10 рядов
        {
            for (int x = 0; x < 5; x++) // 5 колонок
            {
                GameObject newCell = Instantiate(mCellPrefab, transform);

                RectTransform rectTransform = newCell.GetComponent<RectTransform>();
                // Центрируем: умножаем на 100 (размер клетки) и добавляем 50 (половина клетки)
                rectTransform.anchoredPosition = new Vector2((x * 100) + 50, (y * 100) + 50);

                Cell cellComponent = newCell.GetComponent<Cell>();
                if (cellComponent != null)
                {
                    mAllCells[x, y] = cellComponent;
                    cellComponent.Setup(new Vector2Int(x, y), this);
                }
            }
        }
        #endregion

        #region Color
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                // Покраска: нижние 5 рядов - союзники, верхние 5 - враги (для визуального теста)
                // Но оставим шахматку, чтобы клетки не сливались
                if ((x + y) % 2 == 0)
                    mAllCells[x, y].GetComponent<Image>().color = new Color32(100, 200, 100, 255); 
                else
                    mAllCells[x, y].GetComponent<Image>().color = new Color32(50, 150, 50, 255);
                
                // Если хочешь подсветить сторону врага чуть иначе (например, синеватым):
                if (y >= 5) 
                {
                     Color currentColor = mAllCells[x, y].GetComponent<Image>().color;
                     mAllCells[x, y].GetComponent<Image>().color = new Color(currentColor.r, currentColor.g, currentColor.b + 0.2f);
                }
            }
        }
        #endregion
    }

    public CellState ValidateCell(int targetX, int targetY, BasePiece checkingPiece)
    {
        // Обновленная проверка границ под 5x10
        if (targetX < 0 || targetX >= 5 || targetY < 0 || targetY >= 10)
            return CellState.OutOfBounds;

        Cell targetCell = mAllCells[targetX, targetY];

        if (targetCell.mCurrentPiece != null)
        {
            if (checkingPiece.mColor == targetCell.mCurrentPiece.mColor)
                return CellState.Friendly;

            return CellState.Enemy;
        }

        return CellState.Free;
    }
    void Start()
    {
        if (mFullBackground != null)
        {
            RectTransform bgRect = mFullBackground.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(500, 1000);
            bgRect.anchoredPosition = new Vector2(250, 500); // Центр поля
        }
    }
}