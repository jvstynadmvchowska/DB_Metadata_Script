using FirebirdSql.Data.FirebirdClient;
using System;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace DbMetaTool
{
    public static class Program
    {
        // Przykładowe wywołania:
        // DbMetaTool build-db --db-dir "C:\db\fb5" --scripts-dir "C:\scripts"
        // DbMetaTool export-scripts --connection-string "..." --output-dir "C:\out"
        // DbMetaTool update-db --connection-string "..." --scripts-dir "C:\scripts"
        public static int Main(string[] args)
        {

            if (args.Length == 0)
            {
                Console.WriteLine("Użycie:");
                Console.WriteLine("  build-db --db-dir <ścieżka> --scripts-dir <ścieżka>");
                Console.WriteLine("  export-scripts --connection-string <connStr> --output-dir <ścieżka>");
                Console.WriteLine("  update-db --connection-string <connStr> --scripts-dir <ścieżka>");
                return 1;
            }

            try
            {
                var command = args[0].ToLowerInvariant();

                switch (command)
                {
                    case "build-db":
                        {
                            string dbDir = GetArgValue(args, "--db-dir");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            BuildDatabase(dbDir, scriptsDir);
                            Console.WriteLine("Baza danych została zbudowana pomyślnie.");
                            return 0;
                        }
                        
                    case "export-scripts":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string outputDir = GetArgValue(args, "--output-dir");

                            ExportScripts(connStr, outputDir);
                            Console.WriteLine("Skrypty zostały wyeksportowane pomyślnie.");
                            return 0;
                        }

                    case "update-db":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            UpdateDatabase(connStr, scriptsDir);
                            Console.WriteLine("Baza danych została zaktualizowana pomyślnie.");
                            return 0;
                        }

                    default:
                        Console.WriteLine($"Nieznane polecenie: {command}");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd: " + ex.Message);
                return -1;
            }
        }

        private static string GetArgValue(string[] args, string name)
        {
            int idx = Array.IndexOf(args, name);
            if (idx == -1 || idx + 1 >= args.Length)
                throw new ArgumentException($"Brak wymaganego parametru: {name}");
            return args[idx + 1];
        }

        /// <summary>
        /// Buduje nową bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void BuildDatabase(string databaseDirectory, string scriptsDirectory)
        {
            // TODO:
            // 1) Utwórz pustą bazę danych FB 5.0 w katalogu databaseDirectory.
            // 2) Wczytaj i wykonaj kolejno skrypty z katalogu scriptsDirectory
            //    (tylko domeny, tabele, procedury).
            // 3) Obsłuż błędy i wyświetl raport.

            string dbFileName = "NewDbFromScripts.fdb";
            string dbPath = Path.Combine(databaseDirectory, dbFileName);
            string connectionString = $"User=SYSDBA;Password=admin;Database={dbPath};DataSource=localhost;Dialect=3;Charset=UTF8;";

            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }

            FbConnection.CreateDatabase(connectionString);

            using (var connection = new FbConnection(connectionString))
            {
                connection.Open();

                ExecuteScriptsInDirectory(connection, Path.Combine(scriptsDirectory, "domains"));
                ExecuteScriptsInDirectory(connection, Path.Combine(scriptsDirectory, "tables"));
                ExecuteScriptsInDirectory(connection, Path.Combine(scriptsDirectory, "procedures"));
            }

            //throw new NotImplementedException();
        }

        private static void ExecuteScriptsInDirectory(FbConnection connection, string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return;

            var files = Directory.GetFiles(directoryPath, "*.sql");
            foreach (var filePath in files)
            {
                string scriptText = File.ReadAllText(filePath);
                string fileName = Path.GetFileName(filePath);

                try
                {
                    using (var command = new FbCommand(scriptText, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                    Console.WriteLine($"[SUKCES] Wykonano skrypt: {fileName}");
                }
                catch (FbException ex)
                {
                    bool objectExistsError = ex.Message.Contains("violation of PRIMARY or UNIQUE KEY constraint") ||
                                             ex.Message.Contains("unsuccessful metadata update") ||
                                             ex.Message.Contains("already exists");

                    if (objectExistsError)
                    {
                        Console.WriteLine($"[INFO] Pominięto (obiekt istnieje): {fileName}");
                    }
                    else
                    {
                        Console.WriteLine($"[BŁĄD SQL] Nie udało się wykonać {fileName}: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BŁĄD PLIKU] {fileName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Generuje skrypty metadanych z istniejącej bazy danych Firebird 5.0.
        /// </summary>
        public static void ExportScripts(string connectionString, string outputDirectory)
        {
            if (Directory.Exists(outputDirectory)) Directory.Delete(outputDirectory, true);
            Directory.CreateDirectory(outputDirectory);
            Directory.CreateDirectory(Path.Combine(outputDirectory, "domains"));
            Directory.CreateDirectory(Path.Combine(outputDirectory, "tables"));
            Directory.CreateDirectory(Path.Combine(outputDirectory, "procedures"));

            using (var connection = new FbConnection(connectionString))
            {
                connection.Open();

                string domainSql = @"
            SELECT 
                TRIM(f.RDB$FIELD_NAME),
                CASE f.RDB$FIELD_TYPE
                    WHEN 7 THEN 'SMALLINT' WHEN 8 THEN 'INTEGER' WHEN 10 THEN 'FLOAT'
                    WHEN 12 THEN 'DATE' WHEN 13 THEN 'TIME' WHEN 14 THEN 'CHAR'
                    WHEN 16 THEN 'BIGINT' WHEN 27 THEN 'DOUBLE PRECISION' WHEN 35 THEN 'TIMESTAMP'
                    WHEN 37 THEN 'VARCHAR' WHEN 261 THEN 'BLOB' ELSE 'UNKNOWN'
                END,
                COALESCE(f.RDB$CHARACTER_LENGTH, f.RDB$FIELD_LENGTH),
                f.RDB$FIELD_SCALE,
                f.RDB$FIELD_PRECISION,
                IIF(f.RDB$NULL_FLAG = 1, ' NOT NULL', '')
            FROM RDB$FIELDS f
            WHERE f.RDB$SYSTEM_FLAG = 0 AND f.RDB$FIELD_NAME NOT LIKE 'RDB$%'";

                using (var cmd = new FbCommand(domainSql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string name = reader.GetString(0);
                        string type = reader.GetString(1).Trim();
                        int length = reader.IsDBNull(2) ? 0 : reader.GetInt16(2);
                        int scale = reader.GetInt16(3);
                        int precision = reader.IsDBNull(4) ? 0 : reader.GetInt16(4);
                        string nullable = reader.GetString(5);

                        string finalType = type;
                        if ((type == "SMALLINT" || type == "INTEGER" || type == "BIGINT") && scale < 0)
                        {
                            if (precision == 0) precision = (type == "BIGINT") ? 18 : (type == "INTEGER" ? 9 : 4);
                            finalType = $"DECIMAL({precision}, {-scale})";
                        }
                        else if (type == "VARCHAR" || type == "CHAR")
                        {
                            finalType = $"{type}({length})";
                        }

                        File.WriteAllText(Path.Combine(outputDirectory, "domains", $"{name}.sql"),
                            $"CREATE DOMAIN {name} AS {finalType}{nullable};");
                    }
                }

                var tables = new List<string>();
                using (var cmd = new FbCommand("SELECT TRIM(RDB$RELATION_NAME) FROM RDB$RELATIONS WHERE RDB$SYSTEM_FLAG = 0 AND RDB$VIEW_BLR IS NULL", connection))
                using (var r = cmd.ExecuteReader()) while (r.Read()) tables.Add(r.GetString(0));

                foreach (var t in tables)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"CREATE TABLE {t} (");

                    string colSql = $@"
                SELECT TRIM(rf.RDB$FIELD_NAME), TRIM(rf.RDB$FIELD_SOURCE), f.RDB$SYSTEM_FLAG,
                CASE f.RDB$FIELD_TYPE
                    WHEN 7 THEN 'SMALLINT' WHEN 8 THEN 'INTEGER' WHEN 10 THEN 'FLOAT'
                    WHEN 12 THEN 'DATE' WHEN 13 THEN 'TIME' WHEN 14 THEN 'CHAR'
                    WHEN 16 THEN 'BIGINT' WHEN 27 THEN 'DOUBLE PRECISION' WHEN 35 THEN 'TIMESTAMP'
                    WHEN 37 THEN 'VARCHAR' WHEN 261 THEN 'BLOB' ELSE 'UNKNOWN'
                END, 
                COALESCE(f.RDB$CHARACTER_LENGTH, f.RDB$FIELD_LENGTH), f.RDB$FIELD_SCALE, f.RDB$FIELD_PRECISION
                FROM RDB$RELATION_FIELDS rf 
                JOIN RDB$FIELDS f ON rf.RDB$FIELD_SOURCE = f.RDB$FIELD_NAME 
                WHERE rf.RDB$RELATION_NAME = '{t}' 
                ORDER BY rf.RDB$FIELD_POSITION";

                    using (var cmd = new FbCommand(colSql, connection))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            string colName = r.GetString(0);
                            string domainName = r.GetString(1);
                            bool isSystem = r.GetInt32(2) == 1;
                            string baseType = r.GetString(3).Trim();
                            int length = r.IsDBNull(4) ? 0 : r.GetInt16(4);
                            int scale = r.GetInt16(5);
                            int precision = r.IsDBNull(6) ? 0 : r.GetInt16(6);

                            string typeStr;
                            if (!domainName.StartsWith("RDB$") && !isSystem)
                            {
                                typeStr = domainName;
                            }
                            else
                            {
                                if ((baseType == "SMALLINT" || baseType == "INTEGER" || baseType == "BIGINT") && scale < 0)
                                {
                                    if (precision == 0) precision = (baseType == "BIGINT") ? 18 : 9;
                                    typeStr = $"DECIMAL({precision}, {-scale})";
                                }
                                else if (baseType == "VARCHAR" || baseType == "CHAR")
                                {
                                    typeStr = $"{baseType}({length})";
                                }
                                else typeStr = baseType;
                            }
                            sb.AppendLine($"    {colName} {typeStr},");
                        }
                    }
                    string finalSql = sb.ToString().TrimEnd(',', '\r', '\n') + "\n);";
                    File.WriteAllText(Path.Combine(outputDirectory, "tables", $"{t}.sql"), finalSql);
                }

                var procedures = new List<(string Name, string Source)>();
                string procListSql = "SELECT TRIM(RDB$PROCEDURE_NAME), RDB$PROCEDURE_SOURCE FROM RDB$PROCEDURES WHERE RDB$SYSTEM_FLAG = 0";

                using (var cmd = new FbCommand(procListSql, connection))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string src = r.IsDBNull(1) ? "" : r.GetString(1);
                        procedures.Add((r.GetString(0), src));
                    }
                }

                foreach (var proc in procedures)
                {
                    string procName = proc.Name;
                    string procSource = proc.Source;

                    List<string> inputs = new List<string>();
                    List<string> outputs = new List<string>();

                    string paramSql = $@"
                SELECT 
                    TRIM(pp.RDB$PARAMETER_NAME), 
                    pp.RDB$PARAMETER_TYPE, -- 0 = IN, 1 = OUT
                    TRIM(f.RDB$FIELD_NAME), -- Nazwa domeny (jeśli użyta)
                    f.RDB$SYSTEM_FLAG,
                    CASE f.RDB$FIELD_TYPE
                        WHEN 7 THEN 'SMALLINT' WHEN 8 THEN 'INTEGER' WHEN 10 THEN 'FLOAT'
                        WHEN 12 THEN 'DATE' WHEN 13 THEN 'TIME' WHEN 14 THEN 'CHAR'
                        WHEN 16 THEN 'BIGINT' WHEN 27 THEN 'DOUBLE PRECISION' WHEN 35 THEN 'TIMESTAMP'
                        WHEN 37 THEN 'VARCHAR' WHEN 261 THEN 'BLOB' ELSE 'UNKNOWN'
                    END,
                    COALESCE(f.RDB$CHARACTER_LENGTH, f.RDB$FIELD_LENGTH),
                    f.RDB$FIELD_SCALE,
                    f.RDB$FIELD_PRECISION
                FROM RDB$PROCEDURE_PARAMETERS pp
                JOIN RDB$FIELDS f ON pp.RDB$FIELD_SOURCE = f.RDB$FIELD_NAME
                WHERE pp.RDB$PROCEDURE_NAME = '{procName}'
                ORDER BY pp.RDB$PARAMETER_TYPE, pp.RDB$PARAMETER_NUMBER";

                    using (var cmd = new FbCommand(paramSql, connection))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            string pName = r.GetString(0);
                            int pType = r.GetInt16(1);
                            string domainName = r.GetString(2);
                            bool isSystem = r.GetInt32(3) == 1;
                            string baseType = r.GetString(4).Trim();
                            int length = r.IsDBNull(5) ? 0 : r.GetInt16(5);
                            int scale = r.GetInt16(6);
                            int precision = r.IsDBNull(7) ? 0 : r.GetInt16(7);

                            string typeStr;
                            if (!domainName.StartsWith("RDB$") && !isSystem)
                            {
                                typeStr = domainName;
                            }
                            else
                            {
                                if ((baseType == "SMALLINT" || baseType == "INTEGER" || baseType == "BIGINT") && scale < 0)
                                {
                                    if (precision == 0) precision = (baseType == "BIGINT") ? 18 : 9;
                                    typeStr = $"DECIMAL({precision}, {-scale})";
                                }
                                else if (baseType == "VARCHAR" || baseType == "CHAR")
                                {
                                    typeStr = $"{baseType}({length})";
                                }
                                else typeStr = baseType;
                            }

                            string paramDef = $"{pName} {typeStr}";
                            if (pType == 0) inputs.Add(paramDef);
                            else outputs.Add(paramDef);
                        }
                    }

                    var sb = new StringBuilder();
                    sb.Append($"CREATE OR ALTER PROCEDURE {procName}");

                    if (inputs.Count > 0)
                    {
                        sb.AppendLine(" (");
                        sb.AppendLine("    " + string.Join(",\n    ", inputs));
                        sb.Append(")");
                    }

                    if (outputs.Count > 0)
                    {
                        sb.AppendLine("\nRETURNS (");
                        sb.AppendLine("    " + string.Join(",\n    ", outputs));
                        sb.Append(")");
                    }

                    sb.AppendLine("\nAS");
                    sb.AppendLine(procSource);

                    File.WriteAllText(Path.Combine(outputDirectory, "procedures", $"{procName}.sql"), sb.ToString());
                }
            }
        }

        /// <summary>
        /// Aktualizuje istniejącą bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void UpdateDatabase(string connectionString, string scriptsDirectory)
        {
            // TODO:
            // 1) Połącz się z bazą danych przy użyciu connectionString.
            // 2) Wykonaj skrypty z katalogu scriptsDirectory (tylko obsługiwane elementy).
            // 3) Zadbaj o poprawną kolejność i bezpieczeństwo zmian.

            using (var connection = new FbConnection(connectionString))
            {
                connection.Open();

                ExecuteScriptsInDirectory(connection, Path.Combine(scriptsDirectory, "domains"));
                ExecuteScriptsInDirectory(connection, Path.Combine(scriptsDirectory, "tables"));
                ExecuteScriptsInDirectory(connection, Path.Combine(scriptsDirectory, "procedures"));
            }

            //throw new NotImplementedException();
        }
    }
}
