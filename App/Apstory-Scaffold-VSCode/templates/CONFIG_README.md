# Apstory Scaffold Configuration

This file explains the configuration options available in the `.apstory/config.json` file.

## Configuration Properties

### SqlLiteRepoFolder
The folder where SQLite repository files will be generated. This is used by the "Generate SQLite Repository" command.

### Namespace
The namespace to use for generated code. If null, it will be automatically derived from the SQLProject that is loaded.

### SqlProject
The SQL project path. If null, it will use the current project's SQL folder.

### SqlDestination
The connection string for the database where pending SQL changes will be pushed. This is used when you make changes to SQL files and want to apply those changes to a database.

### Variant
The variant of the scaffold to use for code generation. If null, the default variant will be used. Currently available variants:
- **merge**: A variant where Insert/Update procedures can be given a GUID instead of automatically generating a GUID for new entries in the database.

## Example Configuration
```json
{
    "SqlLiteRepoFolder": "C:\\Projects\\MyProject\\SqliteRepositories",
    "Namespace": "Apstory.Scaffold",
    "SqlProject": "C:\\Projects\\MyProject\\Database\\MyProject.Database.sqlproj",
    "SqlDestination": "Server=myServerAddress;Database=myDataBase;Trusted_Connection=True;",
    "Variant": "merge"
}
```

## Notes
- The `Namespace` is typically left as `null` to be automatically derived from the `SqlProject`
- `SqlDestination` is only required if you need to push SQL changes to a database
- `Variant` should be set to `merge` only if you need to provide custom GUIDs for Insert/Update procedures
