using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TransactionIsolationDemo.Data;
using TransactionIsolationDemo.Hubs;
using TransactionIsolationDemo.Models;
using TransactionIsolationDemo.Utils;

namespace TransactionIsolationDemo.Controllers;

public class NonRepeatableReadController(AppDbContext context, IHubContext<TransactionHub> hub) : Controller
{
    private const int SleepTime = 3000;

    public async Task<IActionResult> Index()
    {
        var orders = await context.Orders.ToListAsync();
        return View(orders);
    }

    [HttpPost]
    public async Task<IActionResult> SimulateTransactionA([FromBody] TransactionBasicInputModel model)
    {
        await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A simulating Non-Repeatable Read with {model.IsolationLevel} isolation level.");

        var transactionIsolationLevel = IsolationLevelHelper.GetIsolationLevel(model.IsolationLevel);
        await using var transactionA = await context.Database.BeginTransactionAsync(transactionIsolationLevel);
        try
        {
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction A started.");

            // First read
            var order = await context.Orders.AsNoTracking().OrderBy(o => o.Id).FirstAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A first read: Quantity = {order.Quantity}. Trigger Transaction B to update the record.");

            // Simulate delay to allow Transaction B to update the record
            await Task.Delay(SleepTime * 2);

            // Re-read the record
            order = await context.Orders.AsNoTracking().OrderBy(o => o.Id).FirstAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A re-read: Quantity = {order.Quantity}.");

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
        await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction B simulating Non-Repeatable Read with {model.IsolationLevel} isolation level.");

        var transactionIsolationLevel = IsolationLevelHelper.GetIsolationLevel(model.IsolationLevel);
        await using var transactionB = await context.Database.BeginTransactionAsync(transactionIsolationLevel);
        try
        {
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction B started.");

            // Update the record
            var order = await context.Orders.OrderBy(o => o.Id).FirstAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction B read: Quantity = {order.Quantity}.");
            order.Quantity += 10; // Modify the record
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