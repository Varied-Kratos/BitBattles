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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) 
        {
            float[] state = GetFlattenedBoardState();
            Debug.Log("Board State Length: " + state.Length + " | First element: " + state[0]);
        }
    }
}