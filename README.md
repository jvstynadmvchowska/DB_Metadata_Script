#**Firebird Metadata Sync Tool**

A .NET 8.0 console application designed to manage Firebird 5.0 database metadata.
This tool enables SQL script generation from an existing database, building a new database from scripts, and updating the structure of an existing database.

##**Prerequisites**
1. NET 8.0 SDK
2. Firebird 5.0 Server (running on localhost, port 3050)

##**Important Configuration Notes**
Please ensure the following before running the tool:
1. USER: Use the default SYSDBA user with the masterkey password. If your credentials differ, you will need to modify the corresponding parts of the code or connection strings.
2. PATHS: Ensure the file paths provided as arguments are correct and match your local environment.
3. SERVER: The Firebird 5.0 server must be running on localhost port 3050. If your setup is different, you will need to update the connection logic in the code.

##**Features**
1. Database Building (build-db)
Creates a new database based on the provided SQL files.
2. Script Export (export-scripts)
Generates .sql files for domains, tables, and stored procedures from an existing database.
3. Database Update (update-db)
Safely updates an existing database (skips existing tables to prevent data loss, updates/alters stored procedures).

##**Known Limitations**
Due to time constraints, a configuration file (e.g., appsettings.json) was not implemented. Consequently, administrative credentials and settings are not currently centralized for better security and maintainability. Feel free to refactor this part or implement a configuration provider.

##**Usage Examples**
1. Script Export
```powershell
DbMetaTool export-scripts --connection-string "User=SYSDBA;Password=masterkey;Database=C:\Data\db.fdb;DataSource=localhost;Dialect=3;Charset=UTF8;" --output-dir "C:\Projects\Output"
```
2. Building a New Database
```powershell
DbMetaTool build-db --db-dir "C:\Data" --scripts-dir "C:\Projects\Output"
```
3. Database Update
```powershell
DbMetaTool update-db --connection-string "User=SYSDBA;Password=masterkey;Database=C:\Data\db.fdb;DataSource=localhost;Dialect=3;Charset=UTF8;" --scripts-dir "C:\Projects\Output"
```
