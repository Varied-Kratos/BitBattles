using UnityEngine;

public class Knight : BasePiece
{
    public override void Setup(bool isPlayer, Color newTeamColor, Color32 newSpriteColor, PieceManager newPieceManager)
    {
        unitID = 1;
        maxHP = 15;
        damage = 5;
        attackRange = 1;
        attackSpeed = 1.2f;
        movementSpeed = 1.0f;
        cost = 3;

        base.Setup(isPlayer, newTeamColor, newSpriteColor, newPieceManager);
        // REMOVE: ApplyLevelStats(); - will be called externally after setting level
    }
}