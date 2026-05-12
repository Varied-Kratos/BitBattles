using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public abstract class BasePiece : EventTrigger
{
    // В начало класса, где другие поля
    private HealthBar mHealthBar;
    private static Canvas mHealthBarCanvas;

    public static GameObject HealthBarPrefab;

    [HideInInspector]
    public Color mColor = Color.clear;
    public bool mIsFirstMove = true;

    protected Cell mOriginalCell = null;
    public Cell mCurrentCell = null;

    protected RectTransform mRectTransform = null;
    protected PieceManager mPieceManager;

    protected Cell mTargetCell = null;

    protected Vector3Int mMovement = Vector3Int.one;
    protected List<Cell> mHighlightedCells = new List<Cell>();

    [Header("Combat Stats")]
    public int maxHP = 10;
    public int currentHP;
    public int damage = 3;
    public int attackRange = 1;
    public float attackSpeed = 1.0f;   // задержка в секундах

    [Header("AI & GA Params")]
    public float movementSpeed = 1.0f; // модификатор скорости движения
    public int cost = 1;               // стоимость юнита для бюджета отряда
    public int unitID = 0;             // числовой идентификатор типа (Knight=1, Archer=2…)

    protected float mLastAttackTime = -999f;
    // Новые поля (добавь в начало класса)
    private static Camera mMainCamera;
    private static Canvas mMainCanvas;
    // Новый метод для создания HealthBar

    public virtual void Setup(Color newTeamColor, Color32 newSpriteColor, PieceManager newPieceManager)
    {
        mPieceManager = newPieceManager;
        mColor = newTeamColor;
        GetComponent<Image>().color = newSpriteColor;
        mRectTransform = GetComponent<RectTransform>();
        currentHP = maxHP;

        // Создаём HealthBar
        CreateHealthBar();
    }

    private void CreateHealthBar()
    {
        GameObject healthBarObj = new GameObject("HealthBar");
        healthBarObj.transform.SetParent(transform, false);

        RectTransform rt = healthBarObj.AddComponent<RectTransform>();
        rt.localPosition = Vector3.zero;

        mHealthBar = healthBarObj.AddComponent<HealthBar>();
        mHealthBar.Setup(this);
    }

    public virtual void Place(Cell newCell)
    {
        // Cell stuff
        mCurrentCell = newCell;
        mOriginalCell = newCell;
        mCurrentCell.mCurrentPiece = this;

        // Object stuff
        transform.position = newCell.transform.position;
        gameObject.SetActive(true);
    }

    public void Reset()
    {
        Kill();

        mIsFirstMove = true;

        Place(mOriginalCell);
    }

    public virtual void Kill()
    {
        // Добавляем проверку, чтобы не было ошибки
        if (mCurrentCell != null)
        {
            mCurrentCell.mCurrentPiece = null;
        }

        gameObject.SetActive(false);
    }

    public bool HasMove()
    {
        CheckPathing();

        // If no moves
        if (mHighlightedCells.Count == 0)
            return false;

        // If moves available
        return true;
    }

    public void ComputerMove()
    {
        // Get random cell
        int i = Random.Range(0, mHighlightedCells.Count);
        mTargetCell = mHighlightedCells[i];

        // Move to new cell
        Move();

        // End turn
        mPieceManager.SwitchSides(mColor);
    }

    #region Movement
    private void CreateCellPath(int xDirection, int yDirection, int movement)
    {
        // Target position
        int currentX = mCurrentCell.mBoardPosition.x;
        int currentY = mCurrentCell.mBoardPosition.y;

        // Check each cell
        for (int i = 1; i <= movement; i++)
        {
            currentX += xDirection;
            currentY += yDirection;

            // Get the state of the target cell
            CellState cellState = CellState.None;
            cellState = mCurrentCell.mBoard.ValidateCell(currentX, currentY, this);

            // If enemy, add to list, break
            if (cellState == CellState.Enemy)
            {
                mHighlightedCells.Add(mCurrentCell.mBoard.mAllCells[currentX, currentY]);
                break;
            }

            // If the cell is not free, break
            if (cellState != CellState.Free)
                break;

            // Add to list
            mHighlightedCells.Add(mCurrentCell.mBoard.mAllCells[currentX, currentY]);
        }
    }

    protected virtual void CheckPathing()
    {
        // Horizontal
        CreateCellPath(1, 0, mMovement.x);
        CreateCellPath(-1, 0, mMovement.x);

        // Vertical 
        CreateCellPath(0, 1, mMovement.y);
        CreateCellPath(0, -1, mMovement.y);

        // Upper diagonal
        CreateCellPath(1, 1, mMovement.z);
        CreateCellPath(-1, 1, mMovement.z);

        // Lower diagonal
        CreateCellPath(-1, -1, mMovement.z);
        CreateCellPath(1, -1, mMovement.z);
    }

    protected void ShowCells()
    {
        foreach (Cell cell in mHighlightedCells)
            cell.mOutlineImage.enabled = true;
    }

    protected void ClearCells()
    {
        foreach (Cell cell in mHighlightedCells)
            cell.mOutlineImage.enabled = false;

        mHighlightedCells.Clear();
    }

    protected virtual void Move()
    {
        mIsFirstMove = false;

        if (mTargetCell.mCurrentPiece != null)
            mTargetCell.RemovePiece();

        mCurrentCell.mCurrentPiece = null;

        mCurrentCell = mTargetCell;
        mCurrentCell.mCurrentPiece = this;

        transform.position = mCurrentCell.transform.position;
        mTargetCell = null;
    }
    #endregion

    #region Events
    public override void OnBeginDrag(PointerEventData eventData)
    {
        base.OnBeginDrag(eventData);

        // Test for cells
        CheckPathing();

        // Show valid cells
        ShowCells();
    }

    public override void OnDrag(PointerEventData eventData)
    {
        base.OnDrag(eventData);

        // Follow pointer
        transform.position += (Vector3)eventData.delta;

        // Check for overlapping available squares
        foreach (Cell cell in mHighlightedCells)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(cell.mRectTransform, Input.mousePosition))
            {
                // If the mouse is within a valid cell, get it, and break.
                mTargetCell = cell;
                break;
            }

            // If the mouse is not within any highlighted cell, we don't have a valid move.
            mTargetCell = null;
        }
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        base.OnEndDrag(eventData);

        // Hide
        ClearCells();

        // Return to original position
        if (!mTargetCell)
        {
            transform.position = mCurrentCell.gameObject.transform.position;
            return;
        }

        // Move to new cell
        Move();

        // End turn
        mPieceManager.SwitchSides(mColor);
    }
    #endregion

    // Поиск ближайшего врага
    public BasePiece FindNearestEnemy()
    {
        List<BasePiece> enemies;

        // Определяем, в каком списке искать врагов
        if (mColor == Color.white)
            enemies = mPieceManager.mEnemyMinis;
        else
            enemies = mPieceManager.mMyMinis;

        BasePiece nearest = null;
        float minDistance = float.MaxValue;

        foreach (BasePiece enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeSelf)
                continue;

            float dist = Vector2Int.Distance(
                mCurrentCell.mBoardPosition,
                enemy.mCurrentCell.mBoardPosition
            );

            if (dist < minDistance)
            {
                minDistance = dist;
                nearest = enemy;
            }
        }

        return nearest;
    }

    // Проверка, может ли юнит атаковать цель (по дальности)
    public bool CanAttackTarget(BasePiece target)
    {
        if (target == null || !target.gameObject.activeSelf)
            return false;

        float dist = Vector2Int.Distance(
            mCurrentCell.mBoardPosition,
            target.mCurrentCell.mBoardPosition
        );

        return dist <= attackRange;
    }

    // Нанесение урона цели
    public virtual void AttackTarget(BasePiece target)
    {
        if (target == null) return;

        target.TakeDamage(damage);
        Debug.Log($"{gameObject.name} ({unitID}) атакует на {damage} урона! У цели осталось {target.currentHP} HP");

        mLastAttackTime = Time.time;
    }

    // Получение урона
    public virtual void TakeDamage(int amount)
    {
        currentHP -= amount;

        if (currentHP <= 0)
        {
            currentHP = 0;
            Die();
        }
    }

    public virtual void Die()
    {
        Debug.Log($"{gameObject.name} ({unitID}) погибает!");

        if (mColor == Color.white)
            mPieceManager.mMyMinis.Remove(this);
        else
            mPieceManager.mEnemyMinis.Remove(this);

        if (mCurrentCell != null)
            mCurrentCell.mCurrentPiece = null;

        // HealthBar уничтожится вместе с юнитом (он дочерний)
        Destroy(gameObject);
    }
    // Получение соседней клетки в сторону цели (для движения)
    public Cell GetCellTowardsTarget(BasePiece target)
    {
        if (target == null) return null;

        Vector2Int myPos = mCurrentCell.mBoardPosition;
        Vector2Int targetPos = target.mCurrentCell.mBoardPosition;

        Vector2Int bestCell = myPos;
        float bestDist = float.MaxValue;

        // Проверяем 4 соседние клетки (вверх, вниз, влево, вправо)
        Vector2Int[] directions = new Vector2Int[]
        {
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0)
        };

        foreach (Vector2Int dir in directions)
        {
            Vector2Int newPos = myPos + dir;
            CellState state = mCurrentCell.mBoard.ValidateCell(newPos.x, newPos.y, this);

            // Идём только в свободные клетки
            if (state != CellState.Free)
                continue;

            float dist = Vector2Int.Distance(newPos, targetPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCell = newPos;
            }
        }

        if (bestCell == myPos)
            return null; // Нет доступных клеток для движения

        return mCurrentCell.mBoard.mAllCells[bestCell.x, bestCell.y];
    }

    // Движение в указанную клетку
    public virtual void MoveToCell(Cell targetCell)
    {
        if (targetCell == null) return;

        // Освобождаем текущую клетку
        if (mCurrentCell != null)
            mCurrentCell.mCurrentPiece = null;

        // Занимаем новую
        mCurrentCell = targetCell;
        mCurrentCell.mCurrentPiece = this;
        transform.position = mCurrentCell.transform.position;

        Debug.Log($"{gameObject.name} ({unitID}) движется к {mCurrentCell.mBoardPosition}");
    }

    // Основное действие юнита за ход
    public virtual void TakeTurn()
    {
        BasePiece target = FindNearestEnemy();

        if (target == null)
            return; // Нет врагов — ничего не делаем

        // Если можем атаковать — атакуем
        if (CanAttackTarget(target))
        {
            AttackTarget(target);
        }
        else
        {
            // Иначе двигаемся к цели
            Cell nextCell = GetCellTowardsTarget(target);
            if (nextCell != null)
            {
                MoveToCell(nextCell);
            }
        }

    }
}