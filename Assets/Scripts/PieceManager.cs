using System;
using System.Collections.Generic;
using UnityEngine;

public class PieceManager : MonoBehaviour
{
    public GameObject mPiecePrefab;

    private List<BasePiece> mMyMinis = new List<BasePiece>();
    private List<BasePiece> mEnemyMinis = new List<BasePiece>();

    private Board mBoard;

    public void Setup(Board board)
    {
        mBoard = board;

        // Игрок
        SpawnUnit(typeof(Knight), Color.white, new Color32(80, 124, 159, 255), new Vector2Int(2, 0));
        SpawnUnit(typeof(Archer), Color.white, new Color32(80, 200, 100, 255), new Vector2Int(1, 0));
        SpawnUnit(typeof(Mage), Color.white, new Color32(200, 80, 200, 255), new Vector2Int(3, 0));

        // Противник
        SpawnUnit(typeof(Knight), Color.black, new Color32(210, 95, 64, 255), new Vector2Int(2, 9));
        SpawnUnit(typeof(Archer), Color.black, new Color32(200, 50, 50, 255), new Vector2Int(0, 9));
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
}