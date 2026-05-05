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
        int index = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Cell cell = mBoard.mAllCells[x, y];
                
                if (cell.mCurrentPiece != null)
                {
                    state[index++] = 1.0f; // Unit Type ID
                    state[index++] = (cell.mCurrentPiece.mColor == Color.white) ? 1.0f : -1.0f; // Team
                }
                else
                {
                    state[index++] = 0.0f;
                    state[index++] = 0.0f;
                }
            }
        }

        return state;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) // Нажми Пробел во время игры
        {
            float[] state = GetFlattenedBoardState();
            Debug.Log("Board State Length: " + state.Length + " | First element: " + state[0]);
        }
    }
}