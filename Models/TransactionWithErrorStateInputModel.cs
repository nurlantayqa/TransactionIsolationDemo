namespace TransactionIsolationDemo.Models;

public record TransactionWithErrorStateInputModel(string IsolationLevel, bool IsSuccess);