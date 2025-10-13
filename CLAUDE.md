# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Apstory.Scaffold is a code generation tool that follows a **database-first approach** for MSSQL databases. It generates CRUD stored procedures and corresponding C# code using **convention-over-configuration** methodology. The repository contains three main components:

1. **Apstory.Scaffold.App** - .NET 8.0 CLI tool (distributed as a dotnet global tool)
2. **Apstory.Scaffold.VisualStudio** - Visual Studio extension
3. **Apstory-Scaffold-VSCode** - VSCode extension (TypeScript)

## Architecture

### C# Solution Structure

The .NET solution is organized into three layers:

- **App/** - Contains the CLI application and IDE extensions
  - `Apstory.Scaffold.App/` - Main CLI tool (dotnet tool)
  - `Apstory.Scaffold.VisualStudio/` - Visual Studio extension
  - `Apstory-Scaffold-VSCode/` - VSCode extension
- **Domain/** - Business logic and code generation
  - `Apstory.Scaffold.Domain/Parser/` - SQL and TypeScript parsers
  - `Apstory.Scaffold.Domain/Scaffold/` - Code generation scaffolds
  - `Apstory.Scaffold.Domain/Service/` - Core services
  - `Apstory.Scaffold.Domain/Util/` - Utility classes
- **Model/** - Data models and configuration classes
  - `Apstory.Scaffold.Model/Config/` - Configuration models
  - `Apstory.Scaffold.Model/Sql/` - SQL model representations
  - `Apstory.Scaffold.Model/Typescript/` - TypeScript model representations

### Code Generation Flow

1. The tool searches for a `.sqlproj` file and extracts the project namespace
2. Parsers (`SqlTableParser`, `SqlProcedureParser`) analyze SQL table definitions and stored procedures
3. Scaffold classes generate code in a layered architecture:
   - **SQL Procedures**: `DelHrd`, `DelSft`, `GetByTableNameIds`, `GetById`, `GetByIds`, `GetByIdsPaging`, `InsUpd`
   - **Models**: `ProjectName.Model/Gen/TableName.Gen.cs`
   - **Repository Layer**: `ProjectName.Dal.Dapper/Gen/TableNameRepository.Gen.cs` and interfaces
   - **Service Layer**: `ProjectName.Domain/Gen/TableNameService.Gen.cs` with foreign key extensions
   - **DI Registration**: ServiceCollectionExtension classes for repository and service registration

### Worker Pattern

The CLI uses a worker pattern (BackgroundService) for different operations:
- `SqlScaffoldWatcherWorker` - File system watcher mode (default)
- `SqlScaffoldRegenerationWorker` - Regeneration mode (`-regen`)
- `SqlScaffoldDeleteWorker` - Deletion mode (`-delete`)
- `SqlUpdateWorker` - SQL push mode (`-sqlpush`)
- `TypescriptSearchPageWorker` - Angular search page generation (`-ngsearchpage`)
- `SqlLiteWorker` - SQLite repository generation (`-tsdalfolder`)

## Common Commands

### Building the .NET CLI Tool

```bash
# Build the solution
dotnet build Apstory.Scaffold.sln

# Build specific project
dotnet build App/Apstory.Scaffold.App/Apstory.Scaffold.App.csproj

# Pack and install globally (for testing)
cd App/Apstory.Scaffold.App
dotnet pack
dotnet tool update Apstory.Scaffold.App --global
```

### Building the VSCode Extension

```bash
cd App/Apstory-Scaffold-VSCode

# Install dependencies
npm install

# Type checking
npm run check-types

# Lint
npm run lint

# Compile (development)
npm run compile

# Watch mode (for development)
npm run watch

# Package for distribution
npm run package
```

### Testing the VSCode Extension

Press F5 in VSCode to launch the extension in debug mode.

### Creating VSCode Extension Package

```bash
# Install VSCE globally (if not already installed)
npm install -g @vscode/vsce

# Create .vsix file
cd App/Apstory-Scaffold-VSCode
vsce pack
```

The generated `.vsix` file can be installed by dragging it into VSCode's Extensions tab.

## CLI Tool Usage

### Installation

```bash
dotnet tool update Apstory.Scaffold.App --global
```

### Running the Tool

By default, running `Apstory.Scaffold.App` searches for a `.sqlproj` file and enters watch mode:

```bash
Apstory.Scaffold.App
```

### Key Command-Line Switches

- `-sqlproject <path>` - Override SQL project path
- `-namespace <name>` - Override namespace
- `-regen [schema|table|procedure]` - Regenerate code (e.g., `dbo`, `dbo.Customer`, `dbo;dbo.Orders`)
- `-delete [schema|table|procedure]` - Delete generated code
- `-sqlpush [table|procedure]` - Push SQL changes to database (requires `-sqldestination`)
- `-sqldestination <connectionstring>` - Database connection string for push operations
- `-variant <type>` - Generation variants (e.g., `merge` for custom ID inserts)
- `-tsmodel <path>` - TypeScript model path for SQLite generation
- `-ngsearchpage` - Generate Angular search page
- `-tsdalfolder` - Generate TypeScript DAL service
- `-help` - Show all available commands

## Important Conventions

### SQL Naming

- Generated stored procedures follow naming pattern: `zgen_[TableName]_[Operation]`
- Only one of these switches can be used at a time: `-regen`, `-delete`, `-sqldestination`, `-ngsearchpage`, `-tsdalfolder`

### Custom Return Types

Stored procedures can specify custom return types using the `@ReturnType` annotation at the beginning of the procedure:

```sql
-- @ReturnType: CustomerSummary
CREATE PROCEDURE [dbo].[zgen_Customer_GetSummary]
    @CustomerId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT * FROM CustomerSummary WHERE CustomerId = @CustomerId
END
```

**Behavior:**
- When a custom return type is specified, the generated methods will **always return a list** of that type (`List<CustomReturnType>`)
- Without the annotation, the default behavior uses the table name derived from the procedure name
- The custom return type must be a valid model in the target namespace
- This works across all generated layers: Repository, Repository Interface, Service, Service Interface, and Foreign Service layers

**Example:**
```sql
-- @ReturnType: OrderDetails
CREATE PROCEDURE [dbo].[zgen_Order_GetDetailsByCustomer]
    @CustomerId UNIQUEIDENTIFIER
AS
BEGIN
    -- Complex query returning OrderDetails model instead of Order
END
```

This generates methods like:
- Repository: `Task<List<OrderDetails>> GetOrderDetailsByCustomer(Guid customerId)`
- Service: `Task<List<OrderDetails>> GetOrderDetailsByCustomer(Guid customerId)`

### File Generation

- All generated files include `.Gen.cs` suffix to distinguish them from custom code
- Generated code should not be manually edited as it will be overwritten
- The tool searches for a `.sqlproj` file, preferring paths containing `/DB/` or `\DB\` when multiple projects exist

### Configuration

The tool uses `CSharpConfig` model which is derived from:
1. The `.sqlproj` file's `<RootNamespace>` element (with `.DB` removed)
2. Or the `-namespace` override parameter

## VSCode Extension Details

The VSCode extension provides context menu commands for:
- `extension.generateSQLiteRepo` - Generate SQLite Repository (TypeScript files)
- `apstoryScaffold.runCodeScaffold` - Run Code Scaffold (SQL files)
- `apstoryScaffold.pushSqlChanges` - Push SQL Changes (SQL files)
- `apstoryScaffold.configure` - Configure settings
- `apstoryScaffold.delete` - Delete generated code (SQL files)

Commands are registered in [extension.ts](App/Apstory-Scaffold-VSCode/src/extension.ts) and organized into:
- [typescript-commands.ts](App/Apstory-Scaffold-VSCode/src/commands/typescript-commands.ts)
- [sql-commands.ts](App/Apstory-Scaffold-VSCode/src/commands/sql-commands.ts)
- [vscode-commands.ts](App/Apstory-Scaffold-VSCode/src/commands/vscode-commands.ts)

Configuration is managed via [config-util.ts](App/Apstory-Scaffold-VSCode/src/utils/config-util.ts).

## Dependencies

### .NET CLI Tool
- .NET 8.0
- Dapper (data access)
- Microsoft.Data.SqlClient (SQL Server connectivity)
- Microsoft.Extensions.Hosting (worker pattern)
- Microsoft.CodeAnalysis.CSharp (code generation)

### VSCode Extension
- TypeScript 5.7+
- VSCode Engine 1.97.0+
- esbuild (bundling)
- ESLint (linting)
