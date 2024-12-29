# Transaction Isolation Demo

This project demonstrates the effects of different transaction isolation levels in a database using ASP.NET Core, EF Core and SignalR. The application allows users to simulate transactions with various isolation levels and observe the results in real-time.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
- [Usage](#usage)

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)

## Installation

1. Clone the repository:
    ```sh
    git clone https://github.com/nurlanvalizada/TransactionIsolationDemo.git
    cd TransactionIsolationDemo
    ```

2. Restore .NET dependencies:
    ```sh
    dotnet restore
    ```

## Configuration

1. Update the connection strings in `appsettings.json` to match your SQL Server configuration.

   `appsettings.json`:
    ```json
    {
      "ConnectionStrings": {
        "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=TransactionIsolationDemo;Trusted_Connection=True;"
      }
    }
    ```

2. Apply database migrations:
    ```sh
    dotnet ef database update
    ```

## Usage

1. Run the application:
    ```sh
    dotnet run
    ```

2. Open your browser and navigate to https://localhost:7270 or http://localhost:5233 depending of run profile to access the application.

3. Use Scenario menu page interface to simulate transactions with different isolation levels and observe the results in the transaction log.

4. You can test Dirty Read, Non-Repeatable Read, Phantom Read, Lost Update and Write Skew scenarios.

5. See notes on different pages to understand the results.

6. SignalR is used on each page to see the real-time updates of the transaction log.