using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransactionIsolationDemo.Data;
using TransactionIsolationDemo.Models;

namespace TransactionIsolationDemo.Controllers;

public class HomeController(AppDbContext context) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> ResetDb()
    {
        await context.Orders.ExecuteDeleteAsync();
        await context.Orders.AddAsync(new Order()
        {
            ProductName = "Apple",
            Quantity = 51,
            Price = 75,
            Status = "Pending"
        });
        await context.SaveChangesAsync();
        return View("Index");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
