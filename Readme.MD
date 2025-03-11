# Apstory.Scaffold.App

## Overview

**Apstory.Scaffold.App** follows an **MSSQL database-first** approach to generate CRUD stored procedures and related C# code.  
It adopts a **convention-over-configuration** methodology, reducing complexity when running the application.

## Generated SQL Procedures

When running `regen` on a table, the following SQL procedures will be generated:

- **`DelHrd`** – Hard delete from the table.
- **`DelSft`** – Soft delete (marks row as `IsActive = false`).
- **`GetByTableNameIds`** – Fetches rows based on a list of supplied IDs.
- **`GetById`** – Fetches a record based on the primary key.
- **`GetByIds`** – Fetches records based on foreign keys.
- **`GetByIdsPaging`** – Fetches a paginated set of rows.
- **`InsUpd`** – Inserts or updates an entry in the table.

## Generated C# Structure

### Dependency Injection (DI) Registration
- **`Common/ProjectName.Common.ServiceCollectionExtension/Gen/AddGeneratedRepositoriesServiceCollectionExtension.cs`**  
  - Registers repositories with SQL connection strings.
- **`Common/ProjectName.Common.ServiceCollectionExtension/Gen/AddGeneratedServicesServiceCollectionExtension.cs`**  
  - Registers services.

### Data Access Layer (DAL)
- **`Dal/ProjectName.Dal.Dapper/Gen/TableNameRepository.Gen.cs`**  
  - Contains Dapper methods to interact with the SQL database.
- **`Dal/ProjectName.Dal.Interface/Gen/ITableNameRepository.Gen.cs`**  
  - Repository interface.

### Domain Layer
- **`Domain/ProjectName.Domain/Gen/TableNameService.Gen.cs`**  
  - Service layer that interacts with the repository.
- **`Domain/ProjectName.Domain/Gen/TableNameService.Foreign.Gen.cs`**  
  - Extends the service layer with additional methods for handling foreign key relationships (depth of 1).
- **`Domain/ProjectName.Domain.Interface/Gen/ITableNameService.Gen.cs`**  
  - Interface for the service layer.
- **`Domain/ProjectName.Domain.Interface/Gen/ITableNameService.Foreign.Gen.cs`**  
  - Interface for the foreign key service layer.

### Model Layer
- **`Model/ProjectName.Model/Gen/TableName.Gen.cs`**  
  - Represents the database table as a model, including foreign key relationships.

# Installation

To install or update the tool globally use the following command:
```
dotnet tool update Apstory.Scaffold.App --global
```

## Usage

Running `Apstory.Scaffold.App` will search for a `.sqlproj` file, extract the **namespace** for the entire project, and begin watching for any `.sql` file changes.  
By default, running the application without parameters will make it run in the background, continuously watching for file changes.  
The `.sqlproj` file used can be **overridden** using the `-sqlproject` parameter.

### Available Command-Line Switches

- **`-sqlproject <path>`**  
  Overrides the SQL project path instead of letting the application search for it.

- **`-namespace <name>`**  
  Overrides the namespace for scaffolded code instead of fetching it from the `.sqlproj`.

- **`-regen <params>`**  
  Executes immediate regeneration of files.  
  - When no parameters are supplied, it regenerates **all found schemas**.  
  - You can specify:
    - A schema: **`dbo`**
    - A table: **`dbo.tablename`**
    - A procedure: **`dbo.zgen_procname`**  
  - Multiple entities can be specified using `;`, e.g.:  
    ```
    -regen dbo;dbo.Customer;dbo.zgen_GetOrders
    ```

- **`-sqlpush <params>`**  
  Pushes changes to the database.  
  - When left empty, it detects Git changes.  
  - You can specify:
    - A table: **`dbo.tablename`** (Limited functionality)
    - A procedure: **`dbo.zgen_procname`**  
  - **Requires** the `-sqldestination` switch.  
  - **Note:** Only initial table creation can be pushed, **no table updates** are pushed.  
  - Multiple entities can be specified using `;`, e.g.:  
    ```
    -sqlpush dbo.Customer;dbo.zgen_InsertOrder
    ```

- **`-sqldestination <params>`**  
  Specifies the **database connection string** for pushing changes.

- **`-clean`**  
  Deletes all existing **generated files**.
