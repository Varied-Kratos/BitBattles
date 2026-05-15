using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public abstract class BasePiece : EventTrigger
{
    [HideInInspector] public bool mIsPlayer; // Определяет команду
    protected Image mMainImage;
    [HideInInspector] public Color mColor = Color.clear;
    public bool mIsFirstMove = true;

    protected Cell mOriginalCell = null;
    [HideInInspector] public Cell mCurrentCell = null;

    protected RectTransform mRectTransform = null;
    protected PieceManager mPieceManager;
    protected Cell mTargetCell = null;

    [Header("Level System")]
    public int level = 1;
    public int maxLevel = 3;
    public Sprite[] levelSprites; // 3 спрайта для уровней (назначаются в инспекторе для каждого префаба)

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

    

    private Vector2Int mLastDirection = Vector2Int.zero;
    private HealthBar mHealthBar;
    private CanvasGroup mCanvasGroup;
    public static bool sBattleStarted = false;

   
    protected virtual void Awake()
    {
        mMainImage = GetComponent<Image>();
        mRectTransform = GetComponent<RectTransform>();
    }
    public virtual void Setup(bool isPlayer, Color newTeamColor, Color32 newSpriteColor, PieceManager newPieceManager)
    {
        mPieceManager = newPieceManager;
        mColor = newTeamColor;
        GetComponent<Image>().color = newSpriteColor;
        mRectTransform = GetComponent<RectTransform>();
        currentHP = maxHP;

        Image img = GetComponent<Image>();
        if (img != null)
            img.color = Color.white;

        level = 1; // сбрасываем уровень при создании
        UpdateLevelAppearance();
        CreateHealthBar();
    }

    public void UpdateLevelAppearance()
    {
        if (levelSprites != null && levelSprites.Length >= level && levelSprites[level - 1] != null)
        {
            Image img = GetComponent<Image>();
            if (img != null) img.sprite = levelSprites[level - 1];
        }
    }

    public int GetSellCost()
    {
        int baseCost = cost;

        // Считаем, сколько всего вложено
        int totalInvested = baseCost;
        for (int i = 2; i <= level; i++)
            totalInvested *= 2;

        // Возвращаем на 1 меньше (минимум 1)
        int refund = totalInvested - 1;
        if (refund < 1) refund = 1;

        return refund;
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
            // Используем mIsPlayer вместо цвета для логики зон
            if (mIsPlayer && cell.mBoardPosition.y >= 5) continue; 
            if (!mIsPlayer && cell.mBoardPosition.y < 5) continue;

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
                // Проверка стороны ТОЛЬКО для расстановки (до боя)
                if (!sBattleStarted)
                {
                    bool isPlayer = name.Contains("Player");
                    if (isPlayer && cell.mBoardPosition.y >= 5) continue;
                    if (!isPlayer && cell.mBoardPosition.y < 5) continue;
                }

                CellState state = mPieceManager.mBoard.ValidateCell(cell.mBoardPosition.x, cell.mBoardPosition.y, this);

                if (state == CellState.Free)
                {
                    mTargetCell = cell;
                    break;
                }

                // Слияние
                if (state == CellState.Friendly && cell.mCurrentPiece != null && cell.mCurrentPiece != this)
                {
                    BasePiece other = cell.mCurrentPiece;
                    if (other.name.Contains("Player") &&
                        other.GetType() == GetType() &&
                        other.level == level &&
                        other.level < other.maxLevel)
                    {
                        mTargetCell = cell;
                        break;
                    }
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
            // СЛИЯНИЕ
            if (mTargetCell.mCurrentPiece != null && mTargetCell.mCurrentPiece != this)
            {
                BasePiece other = mTargetCell.mCurrentPiece;
                if (other.name.Contains("Player") &&
                    other.GetType() == GetType() &&
                    other.level == level &&
                    other.level < other.maxLevel)
                {
                    Debug.Log($"СЛИЯНИЕ: {name} → {other.name}");
                    if (mCurrentCell != null)
                        mCurrentCell.mCurrentPiece = null;
                    mPieceManager.FuseUnits(other, this);
                    return;
                }

                // Клетка занята, но не для слияния — возврат
                if (mCurrentCell != null)
                    transform.position = mCurrentCell.transform.position;
                return;
            }

            // Обычная установка
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

        // Ищем врагов по ИМЕНИ, а не по цвету
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

            // Дополнительная проверка: враг должен быть в противоположной команде
            if (name.Contains("Player") && enemy.name.Contains("Player")) continue;
            if (!name.Contains("Player") && !enemy.name.Contains("Player")) continue;

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
    [Header("Attack Sprites")]
    public Sprite[] attackSprites; // 1 или 4 спрайта атаки (по направлениям)
    private Sprite mOriginalSprite; // чтобы запомнить исходный спрайт

    public virtual void TakeDamage(int amount)
    {
        currentHP -= amount;
        StartCoroutine(DamageFlash());
        if (currentHP <= 0) Die();
    }

    private IEnumerator DamageFlash()
    {
        Image img = GetComponent<Image>();
        if (img == null) yield break;

        Color original = img.color;
        img.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        img.color = original;
    }

    public virtual void Die()
    {
        Debug.Log($"{name} погибает");
        // Используем mIsPlayer вместо mColor
        if (mIsPlayer) mPieceManager.mMyMinis.Remove(this);
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

            // БЕЗ проверки стороны — юниты могут ходить везде во время боя
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

        if (CanAttackTarget(target))
        {
            AttackTarget(target);
        }
        else
        {
            Cell next = GetCellTowardsTarget(target);
            if (next != null)
            {
                MoveToCell(next);
            }
            // Если не можем двигаться — просто пропускаем ход
        }
    }

    private Coroutine mAttackAnimation;

    public virtual void AttackTarget(BasePiece target)
    {
        target.TakeDamage(damage);

        // Меняем спрайт на атакующий
        if (attackSprites != null && attackSprites.Length > 0 && attackSprites[0] != null)
        {
            Image img = GetComponent<Image>();
            if (img != null)
            {
                mOriginalSprite = img.sprite; // запоминаем
                img.sprite = attackSprites[0]; // ставим атакующий
            }
        }

        // Запускаем возврат спрайта через время
        StartCoroutine(ResetAttackSprite());

        Debug.Log($"{name} атакует {target.name} на {damage} урона");
    }

    private IEnumerator ResetAttackSprite()
    {
        yield return new WaitForSeconds(0.3f); // длительность атакующего спрайта

        Image img = GetComponent<Image>();
        if (img != null && mOriginalSprite != null)
        {
            img.sprite = mOriginalSprite;
        }
    }

    private IEnumerator MeleeStrike(BasePiece target)
    {
        Image targetImg = target?.GetComponent<Image>();
        Vector3 originalPos = transform.position;
        Vector3 targetPos = target.transform.position;
        Vector3 dir = (targetPos - originalPos).normalized;

        float t = 0;
        while (t < 0.1f) { t += Time.deltaTime; transform.position = Vector3.Lerp(originalPos, originalPos + dir * 25f, t / 0.1f); yield return null; }

        if (targetImg != null) { Color c = targetImg.color; targetImg.color = Color.red; yield return new WaitForSeconds(0.1f); targetImg.color = c; }

        t = 0;
        while (t < 0.1f) { t += Time.deltaTime; transform.position = Vector3.Lerp(originalPos + dir * 25f, originalPos, t / 0.1f); yield return null; }
        transform.position = originalPos;
    }

    private IEnumerator ArrowShot(BasePiece target)
    {
        Image targetImg = target?.GetComponent<Image>();
        Vector3 originalPos = transform.position;
        Vector3 dir = (transform.position - target.transform.position).normalized;

        float t = 0;
        while (t < 0.1f) { t += Time.deltaTime; transform.position = Vector3.Lerp(originalPos, originalPos + dir * 15f, t / 0.1f); yield return null; }

        if (targetImg != null) { Color c = targetImg.color; targetImg.color = Color.red; yield return new WaitForSeconds(0.1f); targetImg.color = c; }

        t = 0;
        while (t < 0.1f) { t += Time.deltaTime; transform.position = Vector3.Lerp(originalPos + dir * 15f, originalPos, t / 0.1f); yield return null; }
        transform.position = originalPos;
    }

    private IEnumerator MagicBolt(BasePiece target)
    {
        Image targetImg = target?.GetComponent<Image>();
        Vector3 originalPos = transform.position;

        float t = 0;
        while (t < 0.15f) { t += Time.deltaTime; float y = Mathf.Sin(t / 0.15f * Mathf.PI) * 15f; transform.position = originalPos + new Vector3(0, y, 0); yield return null; }
        transform.position = originalPos;

        if (targetImg != null) { Color c = targetImg.color; targetImg.color = Color.magenta; yield return new WaitForSeconds(0.15f); targetImg.color = c; }
    }
    // Статический метод для отключения драга при старте боя
    public static void DisableAllDrag()
    {
        sBattleStarted = true;
    }

    public void ApplyLevelStats()
    {
        float multiplier = 1f;

        switch (level)
        {
            case 1:
                multiplier = 1f;
                break;
            case 2:
                multiplier = 2f;      // ×2 (было ×1.5)
                break;
            case 3:
                multiplier = 4.5f;    // ×3.5 (было ×2.25)
                break;
        }

        maxHP = Mathf.RoundToInt(maxHP * multiplier);
        damage = Mathf.RoundToInt(damage * multiplier);
        currentHP = maxHP;
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
    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        if (sBattleStarted) return;

        mOriginalCell = mCurrentCell;
        // Перемещаем в конец иерархии, чтобы юнит был ПОВЕРХ всех остальных при таскании
        transform.SetAsLastSibling(); 
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        base.OnBeginDrag(eventData);
        if (sBattleStarted) return;

        // Показываем клетки, куда можно ставить (свою половину)
        ShowValidCells();
    }
}