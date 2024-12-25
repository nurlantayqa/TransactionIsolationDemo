using Microsoft.AspNetCore.SignalR;

namespace TransactionIsolationDemo.Hubs;

public class TransactionHub : Hub
{
    public async Task NotifyTransactionState(string message)
    {
        await Clients.All.SendAsync("ReceiveTransactionState", message);
    }
}