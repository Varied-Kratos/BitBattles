using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceManager : MonoBehaviour
{
    public GameObject mPiecePrefab;

    public List<BasePiece> mMyMinis = new List<BasePiece>();
    public List<BasePiece> mEnemyMinis = new List<BasePiece>();
    public static bool IsBattleActive { get; private set; }
    public Board mBoard;

    [Header("Battle Settings")]
    public float turnDelay = 1.0f;
    private bool mBattleInProgress = false;

    public void Setup(Board board)
    {
        mBoard = board;

        // Враги (isPlayer = false)
        SpawnUnit(typeof(Knight), Color.black, new Color32(210, 95, 64, 255), new Vector2Int(2, 9), false);
        SpawnUnit(typeof(Archer), Color.black, new Color32(200, 50, 50, 255), new Vector2Int(0, 9), false);
        SpawnUnit(typeof(Mage), Color.black, new Color32(180, 50, 180, 255), new Vector2Int(4, 9), false);

        Debug.Log($"mMyMinis.Count = {mMyMinis.Count}, mEnemyMinis.Count = {mEnemyMinis.Count}");
    }

    public void SpawnUnit(Type unitType, Color teamColor, Color32 spriteColor, Vector2Int pos, bool isPlayer)
    {
        Debug.Log($"SpawnUnit: {unitType.Name}, isPlayer={isPlayer}, список: {(isPlayer ? "mMyMinis" : "mEnemyMinis")}");

        GameObject newPieceObject = Instantiate(mPiecePrefab);
        newPieceObject.transform.SetParent(transform);
        newPieceObject.transform.localScale = Vector3.one;

        BasePiece newPiece = (BasePiece)newPieceObject.AddComponent(unitType);
        newPiece.name = $"{unitType.Name}_{(isPlayer ? "Player" : "Enemy")}";

        newPiece.Setup(teamColor, spriteColor, this);
        newPiece.Place(mBoard.mAllCells[pos.x, pos.y]);

        if (isPlayer)
        {
            mMyMinis.Add(newPiece);
            Debug.Log($"Добавлен в mMyMinis: {newPiece.name}");
        }
        else
        {
            mEnemyMinis.Add(newPiece);
            Debug.Log($"Добавлен в mEnemyMinis: {newPiece.name}");
        }
    }

    private IEnumerator BattleLoop()
    {
        Debug.Log("=== БОЙ НАЧАЛСЯ ===");

        int round = 0;
        while (mMyMinis.Count > 0 && mEnemyMinis.Count > 0 && round < 50)
        {
            round++;
            CleanDeadUnits();
            List<BasePiece> allUnits = GetAliveUnits();

            Dictionary<BasePiece, BasePiece> attacks = new Dictionary<BasePiece, BasePiece>();
            Dictionary<BasePiece, Cell> desiredMoves = new Dictionary<BasePiece, Cell>();

            // ФАЗА 1: ВЫБОР ЦЕЛЕЙ
            foreach (BasePiece unit in allUnits)
            {
                if (unit == null || !unit.gameObject.activeSelf) continue;

                BasePiece enemy = unit.FindNearestEnemy();
                if (enemy == null || !enemy.gameObject.activeSelf) continue;

                if (unit.CanAttackTarget(enemy))
                {
                    attacks[unit] = enemy;
                    Debug.Log($"{unit.name} → будет атаковать {enemy.name}");
                }
                else
                {
                    Cell nextCell = unit.GetCellTowardsTarget(enemy);
                    if (nextCell != null && nextCell.mCurrentPiece == null)
                    {
                        desiredMoves[unit] = nextCell;
                        Debug.Log($"{unit.name} → движется к {nextCell.mBoardPosition}");
                    }
                }
            }

            // ФАЗА 2: ДВИЖЕНИЕ
            foreach (var kvp in desiredMoves)
            {
                BasePiece unit = kvp.Key;
                Cell targetCell = kvp.Value;

                if (unit != null && unit.gameObject.activeSelf &&
                    targetCell.mCurrentPiece == null)
                {
                    unit.MoveToCell(targetCell);
                }
            }

            yield return new WaitForSeconds(0.2f);

            // ФАЗА 3: АТАКА (каждый атакует свою цель ОДИН раз)
            foreach (var kvp in attacks)
            {
                BasePiece attacker = kvp.Key;
                BasePiece defender = kvp.Value;

                if (attacker != null && attacker.gameObject.activeSelf &&
                    defender != null && defender.gameObject.activeSelf &&
                    attacker.CanAttackTarget(defender))
                {
                    attacker.AttackTarget(defender);
                }
            }

            yield return new WaitForSeconds(turnDelay);
        }

        if (mMyMinis.Count > 0)
            Debug.Log("=== ПОБЕДА ИГРОКА! ===");
        else if (mEnemyMinis.Count > 0)
            Debug.Log("=== ПОБЕДА ВРАГА! ===");
        else
            Debug.Log("=== НИЧЬЯ! ===");

        mBattleInProgress = false;
    }

    private List<BasePiece> GetAliveUnits()
    {
        List<BasePiece> allUnits = new List<BasePiece>();
        allUnits.AddRange(mMyMinis);
        allUnits.AddRange(mEnemyMinis);
        return allUnits;
    }

    public void SwitchSides(Color color)
    {
        Debug.Log("Action by: " + color);
    }

    public List<BasePiece> GetActiveUnits()
    {
        return GetAliveUnits();
    }

    public void StartBattle()
    {
        if (mBattleInProgress) return;
        IsBattleActive = true;
        mBattleInProgress = true;
        BasePiece.DisableAllDrag();                      // блокируем драг
        StartCoroutine(BattleLoop());
    }

    private void CleanDeadUnits()
    {
        mMyMinis.RemoveAll(unit => unit == null || !unit.gameObject.activeSelf);
        mEnemyMinis.RemoveAll(unit => unit == null || !unit.gameObject.activeSelf);
    }
}