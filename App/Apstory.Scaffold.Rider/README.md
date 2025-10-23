# Apstory Scaffold - JetBrains Rider Extension

A JetBrains Rider IDE extension for Apstory.Scaffold code generation tool. This plugin provides context menu commands for SQL and TypeScript files to easily generate code scaffolding.

## Features

### SQL File Commands (Right-click on .sql files)

- **Run Code Scaffold** - Generate C# code from SQL table definitions
- **Push SQL Changes** - Push SQL changes to database (requires SQL Destination configuration)
- **Delete Generated Code** - Remove generated code files

### TypeScript File Commands (Right-click on .ts files)

- **Generate SQLite Repository** - Create TypeScript repositories for SQLite

### Configuration

Access configuration through:
- Right-click context menu → Scaffold → Configure
- Tools menu → Apstory Scaffold → Configure

Configuration options:
- **SQL Destination** - Database connection string for pushing SQL changes
- **PowerShell Script** - Path to TypeScript generation PowerShell script (default: `gen-typescript.ps1`)

## Building the Plugin

### Prerequisites

- JDK 17 or later
- Gradle (included via wrapper)

### Build Commands

```bash
# Build the plugin
./gradlew build

# Run in development mode
./gradlew runIde

# Build plugin distribution
./gradlew buildPlugin
```

The built plugin will be available in `build/distributions/`.

## Installation

### From File

1. Build the plugin using `./gradlew buildPlugin`
2. Open Rider → Settings → Plugins
3. Click the gear icon → Install Plugin from Disk
4. Select the `.zip` file from `build/distributions/`
5. Restart Rider

### From JetBrains Marketplace (when published)

1. Open Rider → Settings → Plugins
2. Search for "Apstory Scaffold"
3. Click Install
4. Restart Rider

## Usage

1. Install the `Apstory.Scaffold.App` CLI tool globally:
   ```bash
   dotnet tool update Apstory.Scaffold.App --global
   ```

2. Open a project containing SQL files in Rider

3. Right-click on a SQL file → Scaffold → Choose your action

4. The plugin will execute commands in the integrated terminal

## Requirements

- JetBrains Rider 2023.3 or later
- .NET 8.0 SDK
- Apstory.Scaffold.App CLI tool installed globally

## Project Structure

```
src/main/
├── kotlin/com/apstory/scaffold/rider/
│   ├── actions/           # Action implementations
│   ├── services/          # Configuration service
│   └── utils/             # Utility classes
└── resources/
    └── META-INF/
        └── plugin.xml     # Plugin descriptor
```

## Contributing

Issues and pull requests are welcome at the [Apstory.Scaffold repository](https://github.com/Apstory/Apstory.Scaffold).

## License

MIT License - see the repository for details.
