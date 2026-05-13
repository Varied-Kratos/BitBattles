using UnityEngine;

public class Archer : BasePiece
{
    // Добавляем bool isPlayer в аргументы
    public override void Setup(bool isPlayer, Color newTeamColor, Color32 newSpriteColor, PieceManager newPieceManager)
    {
        unitID = 2;
        maxHP = 8;
        damage = 4;
        attackRange = 3;
        attackSpeed = 0.8f;
        movementSpeed = 1.0f;
        cost = 2;

        // Передаем isPlayer первым аргументом
        base.Setup(isPlayer, newTeamColor, newSpriteColor, newPieceManager);
        
        // Инициализируем здоровье
        currentHP = maxHP;
    }
}