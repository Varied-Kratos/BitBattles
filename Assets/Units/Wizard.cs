using UnityEngine;

public class Mage : BasePiece
{
    public override void Setup(Color newTeamColor, Color32 newSpriteColor, PieceManager newPieceManager)
    {
        unitID = 3;
        maxHP = 6;
        damage = 7;
        attackRange = 4;
        attackSpeed = 1.5f;
        movementSpeed = 1.0f;
        cost = 4;

        base.Setup(newTeamColor, newSpriteColor, newPieceManager);
        mMovement = new Vector3Int(1, 1, 0);
    }
}