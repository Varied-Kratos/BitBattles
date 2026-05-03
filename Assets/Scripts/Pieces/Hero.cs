using UnityEngine;

public class Hero : BasePiece
{
    // override сработает только если в базе есть virtual
    protected override void CheckPathing()
    {
        base.CheckPathing();
    }

    protected override void Move()
    {
        base.Move();
        Debug.Log("Hero moved to: " + mCurrentCell.mBoardPosition);
    }
}