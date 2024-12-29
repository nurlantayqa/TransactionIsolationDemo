using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TransactionIsolationDemo.Data;
using TransactionIsolationDemo.Hubs;
using TransactionIsolationDemo.Models;
using TransactionIsolationDemo.Utils;

namespace TransactionIsolationDemo.Controllers;

public class LostUpdateController(AppDbContext context, IHubContext<TransactionHub> hub) : Controller
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
        await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A simulating Lost Update with {model.IsolationLevel} isolation level.");

        var transactionIsolationLevel = IsolationLevelHelper.GetIsolationLevel(model.IsolationLevel);
        await using var transactionA = await context.Database.BeginTransactionAsync(transactionIsolationLevel);
        try
        {
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction A started.");

            // Read the record without holding locks
            var order = await context.Orders.OrderBy(o => o.Id).FirstAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A read: Quantity = {order.Quantity}. Trigger Transaction B to update the record.");

            // Simulate processing delay to allow Transaction B to act
            await Task.Delay(SleepTime * 3);

            order.Quantity += 10; // Update the record
            await context.SaveChangesAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A updated: Quantity = {order.Quantity}.");

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
        await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction B simulating Lost Update with {model.IsolationLevel} isolation level.");

        var transactionIsolationLevel = IsolationLevelHelper.GetIsolationLevel(model.IsolationLevel);
        await using var transactionB = await context.Database.BeginTransactionAsync(transactionIsolationLevel);
        try
        {
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction B started.");

            // Read and update the record
            var order = await context.Orders.OrderBy(o => o.Id).FirstAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction B read: Quantity = {order.Quantity}.");
            order.Quantity += 5;
            await context.SaveChangesAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction B updated: Quantity = {order.Quantity}.");

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