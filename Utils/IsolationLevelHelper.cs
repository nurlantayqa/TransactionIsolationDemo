namespace TransactionIsolationDemo.Utils;

public class IsolationLevelHelper
{
    public static System.Data.IsolationLevel GetIsolationLevel(string level)
    {
        return level switch
        {
            "READ UNCOMMITTED" => System.Data.IsolationLevel.ReadUncommitted,
            "READ COMMITTED" => System.Data.IsolationLevel.ReadCommitted,
            "REPEATABLE READ" => System.Data.IsolationLevel.RepeatableRead,
            "SNAPSHOT" => System.Data.IsolationLevel.Snapshot,
            "SERIALIZABLE" => System.Data.IsolationLevel.Serializable,
            _ => System.Data.IsolationLevel.ReadCommitted,
        };
    }
}