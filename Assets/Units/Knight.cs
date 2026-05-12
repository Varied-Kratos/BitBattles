using UnityEngine;

public class Knight : BasePiece
{
    public override void Setup(Color newTeamColor, Color32 newSpriteColor, PieceManager newPieceManager)
    {
        unitID = 1;
        maxHP = 15;
        damage = 5;
        attackRange = 1;
        attackSpeed = 1.2f;
        movementSpeed = 1.0f;
        cost = 3;

        base.Setup(newTeamColor, newSpriteColor, newPieceManager);
        mMovement = new Vector3Int(1, 1, 0);
    }
}