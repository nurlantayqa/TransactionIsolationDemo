using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TransactionIsolationDemo.Data;
using TransactionIsolationDemo.Hubs;
using TransactionIsolationDemo.Models;
using TransactionIsolationDemo.Utils;

namespace TransactionIsolationDemo.Controllers;

public class WriteSkewController(AppDbContext context, IHubContext<TransactionHub> hub) : Controller
{
    private const int SleepTime = 3000;
    
    public async Task<IActionResult> Index()
    {
        var orders = await context.Orders.ToListAsync();
        return View(orders);
    }

    [HttpPost]
    public async Task<IActionResult> SimulateTransactionA([FromBody] TransactionWithLockStateInputModel model)
    {
        await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A simulating with {model.IsolationLevel} isolation level.");

        var transactionIsolationLevel = IsolationLevelHelper.GetIsolationLevel(model.IsolationLevel);
        await using var transactionA = await context.Database.BeginTransactionAsync(transactionIsolationLevel);
        try
        {
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction A started.");
            if(model.IsExclusiveLock)
            {
                await context.Database.ExecuteSqlRawAsync("SELECT * FROM Orders WITH (XLOCK) WHERE Status = 'Waiting'");
            }
          
            var waitingCount = await context.Orders.Where(o => o.Status == "Waiting").CountAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A checked waiting count: {waitingCount}. Trigger Transaction B to insert a new row.");
            
            await Task.Delay(SleepTime * 2); // Simulate delay

            if (waitingCount < 1) // Business rule: only 1 order can be on waiting
            {
                var newOrder = new Order
                {
                    ProductName = "Order Waiting " + new Random().Next(1, 100),
                    Quantity = 50,
                    Price = 99,
                    Status = "Waiting"
                };
                await context.Orders.AddAsync(newOrder);
                await context.SaveChangesAsync();
                await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction A added a new waiting order.");
            }

            await transactionA.CommitAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction A committed.");
        }
        catch(Exception exc)
        {
            await transactionA.RollbackAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction A rolled back. Exception: " + exc.Message);
        }

        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> SimulateTransactionB([FromBody] TransactionBasicInputModel model)
    {
        await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction B simulating with {model.IsolationLevel} isolation level.");

        var transactionIsolationLevel = IsolationLevelHelper.GetIsolationLevel(model.IsolationLevel);
        await using var transactionB = await context.Database.BeginTransactionAsync(transactionIsolationLevel);
        try
        {
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction B started.");

            var waitingCount = await context.Orders.Where(o => o.Status == "Waiting").CountAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction B checked waiting count: {waitingCount}.");

            if (waitingCount < 1) // Business rule: only 1 order can be on waiting
            {
                var newOrder = new Order
                {
                    ProductName = "Order Waiting " + new Random().Next(1, 100),
                    Quantity = 55,
                    Price = 88,
                    Status = "Waiting"
                };
                await context.Orders.AddAsync(newOrder);
                await context.SaveChangesAsync();
                await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction B added a new waiting order.");
            }

            await transactionB.CommitAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction B committed.");
        }
        catch(Exception exc)
        {
            await transactionB.RollbackAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction B rolled back. Exception: " + exc.Message);
        }

        return Ok();
    }
}
