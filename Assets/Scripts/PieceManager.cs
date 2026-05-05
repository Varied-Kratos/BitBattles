using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct UnitSpawnCommand {
    public int unitTypeID; 
    public int x;
    public int y;
    public int team; // 0 для тебя, 1 для противника
}

public class PieceManager : MonoBehaviour
{
    public GameObject mPiecePrefab;

    private List<BasePiece> mMyMinis = new List<BasePiece>();
    private List<BasePiece> mEnemyMinis = new List<BasePiece>();

    private Board mBoard;

    public void Setup(Board board)
    {
        mBoard = board;
        
        SpawnUnit(typeof(Hero), Color.white, new Color32(80, 124, 159, 255), new Vector2Int(2, 0));
        SpawnUnit(typeof(Hero), Color.black, new Color32(210, 95, 64, 255), new Vector2Int(2, 9));
    }

    public void SpawnUnit(Type unitType, Color teamColor, Color32 spriteColor, Vector2Int pos)
    {
        GameObject newPieceObject = Instantiate(mPiecePrefab);
        newPieceObject.transform.SetParent(transform);
        newPieceObject.transform.localScale = Vector3.one;

        BasePiece newPiece = (BasePiece)newPieceObject.AddComponent(unitType);
        
        newPiece.Setup(teamColor, spriteColor, this);
        newPiece.Place(mBoard.mAllCells[pos.x, pos.y]);

        if (teamColor == Color.white)
            mMyMinis.Add(newPiece);
        else
            mEnemyMinis.Add(newPiece);
    }

    public void SwitchSides(Color color)
    {
        Debug.Log("Action by: " + color);
    }

    public List<BasePiece> GetActiveUnits()
    {
        List<BasePiece> allUnits = new List<BasePiece>();
        allUnits.AddRange(mMyMinis);
        allUnits.AddRange(mEnemyMinis);
        return allUnits;
    }

    public void ClearBoard() {
        // Чистим всех юнитов на доске
        foreach (var cell in mBoard.mAllCells) {
            if (cell.mCurrentPiece != null) {
                Destroy(cell.mCurrentPiece.gameObject);
                cell.mCurrentPiece = null;
            }
        }
        // Обнуляем списки
        mMyMinis.Clear();
        mEnemyMinis.Clear();
    }
}