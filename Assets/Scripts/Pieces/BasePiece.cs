using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public abstract class BasePiece : EventTrigger
{
    [HideInInspector] public Color mColor = Color.clear;
    public bool mIsFirstMove = true;

    protected Cell mOriginalCell = null;
    [HideInInspector] public Cell mCurrentCell = null;

    protected RectTransform mRectTransform = null;
    protected PieceManager mPieceManager;
    protected Cell mTargetCell = null;

    // Статы
    [Header("Combat Stats")]
    public int maxHP = 10;
    public int currentHP;
    public int damage = 3;
    public int attackRange = 1;
    public float attackSpeed = 1.0f;

    [Header("AI & GA Params")]
    public float movementSpeed = 1.0f;
    public int cost = 1;
    public int unitID = 0;

    private HealthBar mHealthBar;
    private CanvasGroup mCanvasGroup;
    public static bool sBattleStarted = false;

    public virtual void Setup(Color newTeamColor, Color32 newSpriteColor, PieceManager newPieceManager)
    {
        mPieceManager = newPieceManager;
        mColor = newTeamColor;
        GetComponent<Image>().color = newSpriteColor;
        mRectTransform = GetComponent<RectTransform>();
        mCanvasGroup = GetComponent<CanvasGroup>();
        currentHP = maxHP;
        CreateHealthBar();
    }

    // Привязка к клетке (при расстановке или перемещении)
    public virtual void Place(Cell newCell)
    {
        // Освободить старую клетку
        if (mCurrentCell != null)
            mCurrentCell.mCurrentPiece = null;

        mCurrentCell = newCell;
        mCurrentCell.mCurrentPiece = this;

        transform.position = newCell.transform.position;
        gameObject.SetActive(true);
    }

    public virtual void Kill()
    {
        if (mCurrentCell != null)
            mCurrentCell.mCurrentPiece = null;
        gameObject.SetActive(false);
    }

    #region Drag-and-Drop для расстановки
    private void ShowValidCells()
    {
        ClearHighlights();
        foreach (Cell cell in mPieceManager.mBoard.mAllCells)
        {
            // Только своя половина
            if (mColor == Color.white && cell.mBoardPosition.y >= 5) continue;
            if (mColor == Color.black && cell.mBoardPosition.y < 5) continue;

            if (mPieceManager.mBoard.ValidateCell(cell.mBoardPosition.x, cell.mBoardPosition.y, this) == CellState.Free)
            {
                if (cell.mOutlineImage != null)
                    cell.mOutlineImage.gameObject.SetActive(true);
            }
        }
    }

    public override void OnDrag(PointerEventData eventData)
    {
        base.OnDrag(eventData);
        if (sBattleStarted) return;

        transform.position += (Vector3)eventData.delta;

        mTargetCell = null;
        foreach (Cell cell in mPieceManager.mBoard.mAllCells)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(cell.mRectTransform, Input.mousePosition))
            {
                // БЛОКИРУЕМ вражескую половину по имени
                bool isPlayer = name.Contains("Player");
                if (isPlayer && cell.mBoardPosition.y >= 5) continue;   // Игроки только внизу
                if (!isPlayer && cell.mBoardPosition.y < 5) continue;   // Враги только вверху

                CellState state = mPieceManager.mBoard.ValidateCell(cell.mBoardPosition.x, cell.mBoardPosition.y, this);
                if (state == CellState.Free)
                {
                    mTargetCell = cell;
                    break;
                }
            }
        }
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        base.OnEndDrag(eventData);

        if (sBattleStarted)
        {
            ClearHighlights();
            if (mCurrentCell != null)
                transform.position = mCurrentCell.transform.position;
            return;
        }

        ClearHighlights();

        if (mTargetCell != null)
        {
            // Финальная проверка стороны по имени
            bool isPlayer = name.Contains("Player");
            if (isPlayer && mTargetCell.mBoardPosition.y >= 5)
            {
                if (mCurrentCell != null)
                    transform.position = mCurrentCell.transform.position;
                return;
            }
            if (!isPlayer && mTargetCell.mBoardPosition.y < 5)
            {
                if (mCurrentCell != null)
                    transform.position = mCurrentCell.transform.position;
                return;
            }

            Place(mTargetCell);
        }
        else
        {
            if (mCurrentCell != null)
                transform.position = mCurrentCell.transform.position;
        }
    }

 


    private void ClearHighlights()
    {
        foreach (Cell cell in mPieceManager.mBoard.mAllCells)
        {
            if (cell.mOutlineImage != null)
                cell.mOutlineImage.gameObject.SetActive(false);
        }
    }
    #endregion

    // Боевая логика (взята из твоего кода, без изменений)
    public BasePiece FindNearestEnemy()
    {
        List<BasePiece> enemies;

        // Определяем врагов по имени
        if (name.Contains("Player"))
            enemies = mPieceManager.mEnemyMinis;
        else
            enemies = mPieceManager.mMyMinis;

        BasePiece nearest = null;
        float minDist = float.MaxValue;

        foreach (BasePiece enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeSelf) continue;
            if (enemy == this) continue;
            if (enemy.mCurrentCell == null) continue;

            float dist = Vector2Int.Distance(
                mCurrentCell.mBoardPosition,
                enemy.mCurrentCell.mBoardPosition
            );

            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }

        return nearest;
    }
    public bool CanAttackTarget(BasePiece target)
    {
        if (target == null || !target.gameObject.activeSelf || mCurrentCell == null || target.mCurrentCell == null)
            return false;
        int dx = Mathf.Abs(mCurrentCell.mBoardPosition.x - target.mCurrentCell.mBoardPosition.x);
        int dy = Mathf.Abs(mCurrentCell.mBoardPosition.y - target.mCurrentCell.mBoardPosition.y);
        int dist = Mathf.Max(dx, dy);
        return dist <= attackRange;
    }

    public virtual void AttackTarget(BasePiece target)
    {
        target.TakeDamage(damage);
        Debug.Log($"{name} атакует {target.name} на {damage} урона");
    }

    public virtual void TakeDamage(int amount)
    {
        currentHP -= amount;
        if (currentHP <= 0) Die();
    }

    public virtual void Die()
    {
        Debug.Log($"{name} погибает");
        if (mColor == Color.white) mPieceManager.mMyMinis.Remove(this);
        else mPieceManager.mEnemyMinis.Remove(this);
        if (mCurrentCell != null) mCurrentCell.mCurrentPiece = null;
        Destroy(gameObject);
    }

    public Cell GetCellTowardsTarget(BasePiece target)
    {
        if (target == null) return null;

        Vector2Int myPos = mCurrentCell.mBoardPosition;
        Vector2Int tPos = target.mCurrentCell.mBoardPosition;
        Vector2Int[] dirs = { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) };

        Cell best = null;
        float bestDist = float.MaxValue;

        foreach (Vector2Int d in dirs)
        {
            Vector2Int newPos = myPos + d;

            CellState state = mPieceManager.mBoard.ValidateCell(newPos.x, newPos.y, this);
            if (state != CellState.Free) continue;

            float dist = Vector2Int.Distance(newPos, tPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = mPieceManager.mBoard.mAllCells[newPos.x, newPos.y];
            }
        }

        return best;
    }

    public virtual void MoveToCell(Cell targetCell)
    {
        if (targetCell == null) return;
        if (mCurrentCell != null) mCurrentCell.mCurrentPiece = null;
        mCurrentCell = targetCell;
        mCurrentCell.mCurrentPiece = this;
        transform.position = mCurrentCell.transform.position;
    }

    public virtual void TakeTurn()
    {
        BasePiece target = FindNearestEnemy();
        if (target == null) return;
        if (CanAttackTarget(target)) AttackTarget(target);
        else { Cell next = GetCellTowardsTarget(target); if (next != null) MoveToCell(next); }
    }

    // Статический метод для отключения драга при старте боя
    public static void DisableAllDrag()
    {
        sBattleStarted = true;
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
}