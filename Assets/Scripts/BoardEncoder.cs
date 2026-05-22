using UnityEngine;

public class BoardEncoder : MonoBehaviour
{
    public Board mBoard; 

    public float[] GetFlattenedBoardState()
    {
        int width = 5;
        int height = 10;
        int featuresPerCell = 2; 

        float[] state = new float[width * height * featuresPerCell];
        
        PieceManager pm = FindObjectOfType<PieceManager>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Вычисляем индекс так, чтобы он всегда совпадал с порядком в Python
                int index = (y * width + x) * featuresPerCell;
                Cell cell = mBoard.mAllCells[x, y];
                
                if (cell.mCurrentPiece != null)
                {
                    state[index] = (float)cell.mCurrentPiece.unitID; 
                    
                    // Проверяем команду: 1.0 для игрока, -1.0 для врага
                    if (pm.mMyMinis.Contains(cell.mCurrentPiece))
                        state[index + 1] = 1.0f;
                    else if (pm.mEnemyMinis.Contains(cell.mCurrentPiece))
                        state[index + 1] = -1.0f;
                    else
                        state[index + 1] = 0.0f; // На случай ошибки
                }
                else
                {
                    state[index] = 0.0f;
                    state[index + 1] = 0.0f;
                }
            }
        }
        return state;
    }

    // BoardEncoder.cs - ДОБАВИТЬ ЭТОТ МЕТОД, старый GetFlattenedBoardState() НЕ ТРОГАТЬ!

    public float[] GetGAState()
    {
        // 5x5 = 25 клеток (только нижняя половина игрока)
        float[] state = new float[100]; // 100, потому что 4 слоя по 25
        
        PieceManager pm = FindObjectOfType<PieceManager>();
        
        // СЛОЙ 1 (0-24): Свои юниты
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                int idx = y * 5 + x;
                Cell cell = mBoard.mAllCells[x, y];
                if (cell.mCurrentPiece != null && pm != null && pm.mMyMinis.Contains(cell.mCurrentPiece))
                    state[idx] = 1f;
                else
                    state[idx] = 0f;
            }
        }
        
        // СЛОЙ 2 (25-49): Камни
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                int idx = 25 + y * 5 + x;
                if (pm != null && pm.blockedCells.Contains(new Vector2Int(x, y)))
                    state[idx] = 1f;
                else
                    state[idx] = 0f;
            }
        }
        
        // СЛОЙ 3 (50-74): Хилки
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                int idx = 50 + y * 5 + x;
                if (pm != null && pm.healCells.Contains(new Vector2Int(x, y)))
                    state[idx] = 1f;
                else
                    state[idx] = 0f;
            }
        }
        
        // СЛОЙ 4 (75-99): Враги на моей половине
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                int idx = 75 + y * 5 + x;
                Cell cell = mBoard.mAllCells[x, y];
                if (cell.mCurrentPiece != null && pm != null && pm.mEnemyMinis.Contains(cell.mCurrentPiece))
                    state[idx] = 1f;
                else
                    state[idx] = 0f;
            }
        }
        Debug.Log($"[DEBUG] Слой 1 (свои, первая клетка [0]): {state[0]}");
        Debug.Log($"[DEBUG] Слой 4 (враги, первая клетка [75]): {state[75]}");
        
        return state; // 100 чисел
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) 
        {
            float[] state = GetFlattenedBoardState();
            Debug.Log("Board State Length: " + state.Length + " | First element: " + state[0]);
        }
    }
}