using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TransactionIsolationDemo.Data;
using TransactionIsolationDemo.Hubs;
using TransactionIsolationDemo.Models;

namespace TransactionIsolationDemo.Controllers;

public class TransactionController(AppDbContext context, IHubContext<TransactionHub> hub) : Controller
{
    private const int SleepTime = 3000;
    public async Task<IActionResult> Index()
    {
        var orders = await context.Orders.ToListAsync();
        return View(orders);
    }

    [HttpPost]
    public async Task<IActionResult> SimulateTransactionA([FromBody] TransactionAInputModel model)
    {
        await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A simulating with {model.IsolationLevel} isolation level.");

        var transactionIsolationLevel = GetIsolationLevel(model.IsolationLevel);
        await using var transactionA = await context.Database.BeginTransactionAsync(transactionIsolationLevel);
        try
        {
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction A started.");

            var orderA = await context.Orders.FirstOrDefaultAsync();
            if(orderA != null)
            {
                await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A retrieved a record with count = {orderA.Quantity}.");
                await Task.Delay(SleepTime); // Simulate a delay before updating
                
                orderA.Quantity += 10;
                await context.SaveChangesAsync();

                await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A updated the order count = {orderA.Quantity}.");
            }

            await Task.Delay(SleepTime); // Simulate a delay before committing

            if(!model.IsSuccess)
            {
                await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction A failed to update the record.");
                throw new Exception("Transaction A failed to update the record.");
            }

            await transactionA.CommitAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction A committed.");
        }
        catch
        {
            await transactionA.RollbackAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction A rolled back.");
        }

        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> SimulateTransactionB([FromBody] TransactionBInputModel model)
    {
        await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction B simulating with {model.IsolationLevel} isolation level.");

        var transactionIsolationLevel = GetIsolationLevel(model.IsolationLevel);
        await using var transactionB = await context.Database.BeginTransactionAsync(transactionIsolationLevel);
        try
        {
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction B started.");

            var orderB = await context.Orders.FirstOrDefaultAsync();
            if(orderB != null)
            {
                await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction B read data: Quantity = {orderB.Quantity}.");
            }

            await transactionB.CommitAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction B committed.");
        }
        catch
        {
            await transactionB.RollbackAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction B rolled back.");
        }

        return Ok();
    }

    private System.Data.IsolationLevel GetIsolationLevel(string level)
    {
        return level switch
        {
            "READ UNCOMMITTED" => System.Data.IsolationLevel.ReadUncommitted,
            "READ COMMITTED" => System.Data.IsolationLevel.ReadCommitted,
            "REPEATABLE READ" => System.Data.IsolationLevel.RepeatableRead,
            "SERIALIZABLE" => System.Data.IsolationLevel.Serializable,
            _ => System.Data.IsolationLevel.ReadCommitted,
        };
    }
}