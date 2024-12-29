namespace TransactionIsolationDemo.Models;

public record TransactionWithLockStateInputModel(string IsolationLevel, bool IsExclusiveLock);