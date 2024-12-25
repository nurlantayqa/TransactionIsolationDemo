using TransactionIsolationDemo.Models;

namespace TransactionIsolationDemo.Data;

using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders { get; set; }
}