// AutoFighter.cs — повесь на любой объект в сцене
using UnityEngine;

public class AutoFighter : MonoBehaviour
{
    public PieceManager pieceManager;
    public float startDelay = 1f;

    void Start()
    {
        Invoke(nameof(StartFight), startDelay);
    }

    void StartFight()
    {
        // Авто-расстановка (спавним пару юнитов)
        pieceManager.PurchaseUnit(typeof(Knight));
        pieceManager.PurchaseUnit(typeof(Archer));

        // Запускаем бой
        pieceManager.StartBattle();
    }

    // После конца боя — перезапуск
    public void OnBattleEnd()
    {
        Invoke(nameof(StartFight), 2f);
    }
}