using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TransactionIsolationDemo.Data;
using TransactionIsolationDemo.Hubs;
using TransactionIsolationDemo.Models;
using TransactionIsolationDemo.Utils;

namespace TransactionIsolationDemo.Controllers;

public class DirtyReadController(AppDbContext context, IHubContext<TransactionHub> hub) : Controller
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
        await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A simulating with {model.IsolationLevel} isolation level.");

        var transactionIsolationLevel = IsolationLevelHelper.GetIsolationLevel(model.IsolationLevel);
        await using var transactionA = await context.Database.BeginTransactionAsync(transactionIsolationLevel);
        try
        {
            await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction A started.");

            var orderA = await context.Orders.OrderBy(x => x.Id).FirstAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A retrieved a record with count = {orderA.Quantity}.");

            //update the record quantiyy
            orderA.Quantity += 10;
            await context.SaveChangesAsync();

            await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction A updated the order count = {orderA.Quantity}. Trigger Transaction B to read the record.");

            // Simulate delay to allow Transaction B to read the record
            await Task.Delay(SleepTime * 2);

            // Throw exception to simulate a failure if requested
            if(!model.IsSuccess)
            {
                await hub.Clients.All.SendAsync("ReceiveTransactionState", "Transaction A failed to update the record.");
                throw new Exception("Transaction A failed to update the record.");
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

            var orderB = await context.Orders.OrderBy(x => x.Id).FirstAsync();
            await hub.Clients.All.SendAsync("ReceiveTransactionState", $"Transaction B read data: Quantity = {orderB.Quantity}.");
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
