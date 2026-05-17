using UnityEngine;

public class Mage : BasePiece
{
    public override void Setup(bool isPlayer, Color newTeamColor, Color32 newSpriteColor, PieceManager newPieceManager)
    {
        unitID = 3;
        maxHP = 6;
        damage = 7;
        attackRange = 4;
        attackSpeed = 1.5f;
        movementSpeed = 1.0f;
        cost = 4;

        base.Setup(isPlayer, newTeamColor, newSpriteColor, newPieceManager);
        if (level > 1) 
        {
            ApplyLevelStats();
        }
    }
}