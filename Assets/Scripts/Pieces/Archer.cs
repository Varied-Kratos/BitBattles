using UnityEngine;

public class Archer : BasePiece
{
    public override void Setup(bool isPlayer, Color newTeamColor, Color32 newSpriteColor, PieceManager newPieceManager)
    {
        unitID = 2;
        maxHP = 8;
        damage = 4;
        attackRange = 3;
        attackSpeed = 0.8f;
        movementSpeed = 1.0f;
        cost = 2;

        base.Setup(isPlayer, newTeamColor, newSpriteColor, newPieceManager);
        // REMOVE: ApplyLevelStats();
    }
}