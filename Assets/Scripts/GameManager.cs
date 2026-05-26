using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public Board mBoard;
    public PieceManager mPieceManager;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    void Start()
    {
        if (mBoard != null && mPieceManager != null)
        {
            mBoard.Create();
            mPieceManager.Setup(mBoard);
        }
    }

    void Update()
    {
        // ������ ��� �� ������� ������� (��� �����)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            mPieceManager.StartBattle();
        }
    }
}