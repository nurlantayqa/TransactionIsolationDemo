using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TransactionIsolationDemo.Data;
using TransactionIsolationDemo.Hubs;
using TransactionIsolationDemo.Models;
using TransactionIsolationDemo.Utils;

namespace TransactionIsolationDemo.Controllers;

public class PhantomReadController(AppDbContext context, IHubContext<TransactionHub> hub) : Controller
{
    private const int SleepTime = 3000;
    
    public async Task<IActionResult> Index()
    {
        var orders = await context.Orders.ToListAsync();
        return View(orders);
    }

    [HttpPost]
    public async Task<IActionResult> SimulateTransactionA([FromBody] TransactionWithErrorStateInputModel model)
    {
        await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A simulating phantom read with {model.IsolationLevel} isolation level.");

        var transactionIsolationLevel = IsolationLevelHelper.GetIsolationLevel(model.IsolationLevel);
        await using var transactionA = await context.Database.BeginTransactionAsync(transactionIsolationLevel);
        try
        {
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction A started.");

            // Read rows matching a condition
            var orders = await context.Orders.Where(o => o.Quantity > 5).ToListAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A read {orders.Count} rows where Quantity > 5. Trigger Transaction B to insert a new row.");

            await Task.Delay(SleepTime * 2); // Simulate delay before re-querying

            // Re-query to simulate phantom read
            orders = await context.Orders.Where(o => o.Quantity > 5).ToListAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A re-read {orders.Count} rows where Quantity > 5.");

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
    public async Task<IActionResult> SimulateTransactionB([FromBody] TransactionWithErrorStateInputModel model)
    {
        await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction B simulating with {model.IsolationLevel} isolation level.");

        var transactionIsolationLevel = IsolationLevelHelper.GetIsolationLevel(model.IsolationLevel);
        await using var transactionB = await context.Database.BeginTransactionAsync(transactionIsolationLevel);
        try
        {
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction B started.");

            // Insert a new row that matches the query condition
            var newOrder = new Order
            {
                ProductName = "New Product " + new Random().Next(1, 100),
                Quantity = new Random().Next(10, 100),
                Price = new Random().Next(50, 200),
                Status = "Pending"
            };
            await context.Orders.AddAsync(newOrder);
            await context.SaveChangesAsync();

            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction B inserted a new row that matches the query condition.");

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
