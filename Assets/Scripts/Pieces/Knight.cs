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

        // Передаем все 4 аргумента в базовый класс
        base.Setup(isPlayer, newTeamColor, newSpriteColor, newPieceManager);
        
        // Инициализируем текущее здоровье после установки maxHP
        currentHP = maxHP; 
    }
}