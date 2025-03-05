# Installation

To install or update the tool globally use the following command:
```
dotnet tool update Apstory.Scaffold.App --global
```

# Using
By running ```Apstory.Scaffold.App``` the application searches for a .sqlproj file to start watching for any .sql file changes.

Available Command-Line Switches:
* -sqlproject <path>       
  Overrides the SQL project path instead of letting the application search for it.
* -namespace <name>        
  Overrides the namespace for scaffolded code instead of fetching it from the sqlproj.
* -regen <params>          
  Executes immediate regeneration of files. Will regenerate all found schemas when no additional information supplied. Can specify a schema 'dbo', a table 'dbo.tablename', or a procedure 'dbo.zgen_procname' to regenerate. Can send multiple entities with ```;```
* -sqlpush <params>        
  Pushes changes to database. Can leave empty to detect git changes. Specify a table 'dbo.tablename' (Limited Functionality), or a procedure 'dbo.zgen_procname' to push. Requires -sqldestination switch as well. Please note: No table updates are pushed, only the initial creates can be pushed. Can send multiple entities with ```;```
* -sqldestination <params> 
  Pushes changes to database. This is the connection string of the database.
* -clean                   
  Deletes existing generated files.
