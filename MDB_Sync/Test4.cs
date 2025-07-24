//using System.Text.Json;
//using System.Threading;
//using System.Data.OleDb;
//using System.Data;
//using System.Net.NetworkInformation;
//using System.IO;
//using System.Text;
//using System.Diagnostics;
//using System.Collections.Concurrent;
//using System.Net.Sockets;

//namespace RobustAccessDbSync
//{
//    class SyncMetadata
//    {
//        public Dictionary<string, DateTime> TableLastSync { get; set; } = new Dictionary<string, DateTime>();
//        public List<QueuedChange> QueuedChanges { get; set; } = new List<QueuedChange>();
//    }

//    class QueuedChange
//    {
//        public string TableName { get; set; }
//        public string PkColumn { get; set; }
//        public Dictionary<string, object> RowData { get; set; }
//        public bool IsDelete { get; set; }
//        public DateTime ChangeTime { get; set; }
//    }

//    class Program
//    {
//        static string DRIVE_LETTER = "X:";
//        private static bool _syncRunning = true;
//        private const string ConflictSuffix = "_CONFLICT_RESOLVED";

//        static string SERVER_IP;
//        static string SHARE_NAME;
//        static string USERNAME;
//        static string PASSWORD;
//        private static bool _isOnline = true;
//        private static DateTime _lastOnlineTime = DateTime.MinValue;
//        private static int _syncCycleWaitMinutes = 1;
//        private static Stopwatch _cycleTimer = new Stopwatch();
//        private static DateTime _nextSyncTime = DateTime.Now;
//        static string clientDbPath;
//        static string serverDbPath;
//        static string filePath = "user_data.txt"; // File to save the input



//        static void GetClinetServerPath()
//        {
//            Console.Title = "Database Synchronization Tool";
//            Console.CursorVisible = false;

//            PrintHeader();
//            ShowGameStyleLoader("Initializing Database Synchronization Tool", 20);
//            while (true)
//            {

//                // Password input
//                do
//                {
//                    Console.Write("Enter Client Path: ");
//                    clientDbPath = Console.ReadLine();
//                    if (string.IsNullOrWhiteSpace(clientDbPath))
//                        Console.WriteLine("client path cannot empty");
//                } while (string.IsNullOrWhiteSpace(clientDbPath));

//                do
//                {
//                    Console.Write("Enter Server Path: ");
//                    serverDbPath = Console.ReadLine();
//                    if (string.IsNullOrWhiteSpace(serverDbPath))
//                        Console.WriteLine("server path cannot empty");
//                } while (string.IsNullOrWhiteSpace(serverDbPath));

//                var serverParts = serverDbPath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
//                if (serverParts.Length < 2)
//                {
//                    PrintError("Invalid server path format. Expected format: \\\\server\\share\\path\\file.mdb");

//                }

//                SERVER_IP = serverParts[0];
//                SHARE_NAME = serverParts[1];
//                // Confirmation
//                //Console.WriteLine("\nYou entered:");
//                //Console.WriteLine($"Serverpath: {serverDbPath}");
//                //Console.WriteLine($"clinetpath: {clientDbPath}");

//                Console.WriteLine("\nPress Enter to continue or type 'r' to re-enter:");

//                string input = Console.ReadLine()?.Trim().ToLower();

//                if (string.IsNullOrEmpty(input))
//                {  // user just pressed Enter
//                    File.WriteAllText(filePath, SERVER_IP);
//                    File.WriteAllText(filePath, SHARE_NAME);
//                    File.WriteAllText(filePath, clientDbPath);
//                    break;
//                }
//                else if (input == "r")
//                    continue;
//                else
//                    Console.WriteLine("Invalid input. Re-entering...\n");
//            }
//        }
//        static void GetServerCredentials()
//        {


//            while (true)
//            {
//                // Username input
//                do
//                {
//                    Console.Write("Enter USERNAME: ");
//                    USERNAME = Console.ReadLine();
//                    if (string.IsNullOrWhiteSpace(USERNAME))
//                        Console.WriteLine("USERNAME cannot be empty.");
//                } while (string.IsNullOrWhiteSpace(USERNAME));

//                // Password input
//                do
//                {
//                    Console.Write("Enter PASSWORD: ");
//                    PASSWORD = ReadPassword();
//                    if (string.IsNullOrWhiteSpace(PASSWORD))
//                        Console.WriteLine("PASSWORD cannot be empty.");
//                } while (string.IsNullOrWhiteSpace(PASSWORD));

//                // Confirmation
//                //Console.WriteLine("\nYou entered:");
//                //Console.WriteLine($"USERNAME: {USERNAME}");
//                //Console.WriteLine($"PASSWORD: {new string('*', PASSWORD.Length)}");

//                Console.WriteLine("\nPress Enter to continue or type 'r' to re-enter:");

//                string input = Console.ReadLine()?.Trim().ToLower();

//                if (string.IsNullOrEmpty(input))
//                {  // user just pressed Enter
//                    File.WriteAllText(filePath, USERNAME);
//                    File.WriteAllText(filePath, PASSWORD);
//                    break;
//                }
//                else if (input == "r")
//                    continue;
//                else
//                    Console.WriteLine("Invalid input. Re-entering...\n");
//            }
//        }

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static async Task Main()
//        {
//            try
//            {

//                // Check if credentials file exists
//                if (File.Exists(filePath))
//                {
//                    string[] savedData = File.ReadAllLines(filePath);

//                    if (savedData.Length >= 5) // Ensure we have all required data
//                    {
//                        Console.WriteLine("Saved credentials found:");
//                        Console.WriteLine($"Username: {savedData[0]}");
//                        Console.WriteLine($"Server Path: {savedData[4]}");
//                        Console.WriteLine($"Client Path: {savedData[5]}");

//                        Console.Write("\nUse saved credentials? (Y/N): ");
//                        var key = Console.ReadKey();

//                        if (key.Key == ConsoleKey.Y)
//                        {
//                            // Use saved credentials
//                            USERNAME = savedData[0];
//                            PASSWORD = savedData[1];
//                            SERVER_IP = savedData[2];
//                            SHARE_NAME = savedData[3];
//                            serverDbPath = savedData[4];
//                            clientDbPath = savedData[5];

//                            Console.WriteLine("\nUsing saved credentials...");
//                        }
//                        else
//                        {
//                            // Get new credentials
//                            GetClinetServerPath();
//                            GetServerCredentials();
//                        }
//                    }
//                    else
//                    {
//                        Console.WriteLine("Saved credentials are incomplete.");
//                        GetClinetServerPath();
//                        GetServerCredentials();
//                    }
//                }
//                else
//                {
//                    // First run - get new credentials
//                    GetClinetServerPath();
//                    GetServerCredentials();

//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine("Please Run as Administrator");
//            }
//            Console.Write("\nSave these credentials for next time? (Y/N): ");
//            var saveKey = Console.ReadKey();
//            if (saveKey.Key == ConsoleKey.Y)
//            {
//                File.WriteAllLines(filePath, new[] {
//            USERNAME,
//            PASSWORD,
//            SERVER_IP,
//            SHARE_NAME,
//            serverDbPath,
//            clientDbPath
//        });
//                Console.WriteLine("\n\nCredentials saved!");
//            }
//            else
//            {
//                Console.WriteLine("\n");
//            }

//            bool isNewClientDb = false;

//            if (!HasMdbExtension(clientDbPath))
//            {
//                if (File.Exists(Path.Combine(clientDbPath, Path.GetFileName(serverDbPath))))
//                {
//                    clientDbPath = Path.Combine(clientDbPath, Path.GetFileName(serverDbPath));
//                }
//                else
//                {
//                    while (true)
//                    {
//                        //string destFolder = clientDbPath; // Initialize with current clientDbPath

//                        // If directory doesn't exist, get new input and restart the check immediately
//                        if (!Directory.Exists(clientDbPath))
//                        {
//                            PrintError($"ERROR: Destination folder does not exist: {clientDbPath}");
//                            Console.Write("Enter a valid destination folder: ");
//                            clientDbPath = Console.ReadLine();
//                            continue; // Restart loop to check the new input
//                        }

//                        // Proceed with network operations if directory exists
//                        clientDbPath = Path.Combine(clientDbPath, Path.GetFileName(serverDbPath));
//                        RunCommand($"net use {DRIVE_LETTER} /delete", false);

//                        // PrintInfo("Mounting shared folder...");
//                        var connectCmd = $"net use {DRIVE_LETTER} \\\\{SERVER_IP}\\{SHARE_NAME} /user:{USERNAME} {PASSWORD} /persistent:no";

//                        if (!RunCommand(connectCmd))
//                        {
//                            PrintError("ERROR: Failed to connect to shared folder.");
//                            GetServerCredentials();
//                            GetClinetServerPath();
//                            continue;
//                        }

//                        string serverFilePath = Path.Combine(DRIVE_LETTER, Path.GetFileName(serverDbPath));

//                        if (!File.Exists(serverFilePath))
//                        {
//                            PrintError($"ERROR: File does not exist on server: {Path.GetFileName(serverDbPath)}");
//                            RunCommand($"net use {DRIVE_LETTER} /delete", false);
//                            Console.Write("Enter a valid server DB path: ");
//                            serverDbPath = Console.ReadLine();
//                            continue;
//                        }

//                        PrintInfo("Copying file from server...");
//                        //try
//                        //{
//                        //    File.Copy(serverFilePath, clientDbPath, true);
//                        //    PrintSuccess($"File successfully copied to: {clientDbPath}");
//                        //    PrintInfo("Synchronization is COmplete Do you want to Start Sync (s) or Q for quit");
//                        //    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
//                        //    {
//                        //        _syncRunning = false;
//                        //        PrintWarning("Stopping synchronization...");
//                        //        break;
//                        //    }

//                        //    await Task.Delay(100);
//                        //    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.S)
//                        //    {
//                        //        _syncRunning = true;
//                        //        PrintWarning("Starting synchronization...");
//                        //    }
//                        //    await Task.Delay(100);
//                        //    isNewClientDb = true;
//                        //    break; // Exit loop on success
//                        //}
//                        //catch (Exception ex)
//                        //{
//                        //    PrintError($"ERROR: File copy failed. {ex.Message}");
//                        //}
//                        try
//                        {
//                            File.Copy(serverFilePath, clientDbPath, true);
//                            PrintSuccess($"File successfully copied to: {clientDbPath}");
//                            isNewClientDb = true;

//                            // Clear any buffered key presses
//                            while (Console.KeyAvailable)
//                                Console.ReadKey(true);

//                            // User input handling
//                            PrintInfo("Synchronization is complete. (S) to Start Sync or (Q) to quit");

//                            while (true)
//                            {
//                                if (Console.KeyAvailable)
//                                {
//                                    var key = Console.ReadKey(true).Key;

//                                    if (key == ConsoleKey.Q)
//                                    {
//                                        _syncRunning = false;
//                                        PrintWarning("Stopping synchronization...");
//                                        Environment.Exit(0);
//                                        break;
//                                    }
//                                    else if (key == ConsoleKey.S)
//                                    {
//                                        _syncRunning = true;
//                                        PrintWarning("Starting synchronization...");
//                                        break;
//                                    }
//                                }
//                                await Task.Delay(100);
//                            }

//                            break; // Exit loop on success
//                        }
//                        catch (IOException ioEx)
//                        {
//                            PrintError($"File access error: {ioEx.Message}");
//                            // Consider adding retry logic here with a delay
//                        }
//                        catch (UnauthorizedAccessException authEx)
//                        {
//                            PrintError($"Permission denied: {authEx.Message}");
//                        }
//                        catch (Exception ex)
//                        {
//                            PrintError($"ERROR: File copy failed. {ex.Message}");
//                        }
//                        RunCommand($"net use {DRIVE_LETTER} /delete", false);
//                    }
//                }
//            }

//            string syncMetaFile = "sync_metadata.json";
//            SyncMetadata metadata = null;

//            if (!File.Exists(clientDbPath))
//            {
//                PrintInfo("Client database not found. Attempting to pull from server...");
//                if (await PullDatabaseFromServer(serverDbPath, clientDbPath))
//                {
//                    PrintSuccess("Successfully pulled database from server to client.");
//                    isNewClientDb = true;
//                }
//                else
//                {
//                    PrintError("\nPress any key to exit...");
//                    Console.ReadKey();
//                    return;
//                }
//            }

//            ShowGameStyleLoader("Verifying database files", 10);
//            Console.WriteLine();

//            if (!VerifyDatabaseFiles(clientDbPath, serverDbPath))
//            {
//                PrintError("\nPress any key to exit...");
//                Console.ReadKey();
//                return;
//            }

//            string clientConnStr = $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={clientDbPath};";
//            string serverConnStr = $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={serverDbPath};";

//            ShowGameStyleLoader("Testing database connections", 20);
//            if (!TestConnection("Client DB", clientConnStr) || !TestConnection("Server DB", serverConnStr))
//            {
//                PrintError("\nPress any key to exit...");
//                Console.ReadKey();
//                return;
//            }



//            //ShowGameStyleLoader("Loading synchronization metadata", 10);
//            //Console.WriteLine();
//            //var clientTables = GetAllTableNames(clientConnStr);
//            //var serverTables = GetAllTableNames(serverConnStr);
//            //var allTables = clientTables.Union(serverTables).ToList();

//            //// Bi-directional table structure sync
//            //foreach (var table in allTables)
//            //{
//            //    // Client → Server
//            //    SyncTableStructure(clientConnStr, serverConnStr, table);
//            //    // Server → Client
//            //    SyncTableStructure(serverConnStr, clientConnStr, table);
//            //}

//            bool Testingdata = isNewClientDb;
//            metadata = LoadSyncMetadata(syncMetaFile) ?? new SyncMetadata();
//            InitializeMetadata(metadata, clientConnStr, serverConnStr, isNewClientDb);

//            PrintSuccess("\nStarting optimized synchronization...");
//            PrintInfo("Press 'Q' then Enter to stop synchronization.\n");

//            var syncTask = Task.Run(() => ContinuousSync(serverConnStr, clientConnStr, syncMetaFile, metadata));

//            while (_syncRunning)
//            {

//                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
//                {
//                    _syncRunning = false;
//                    PrintWarning("Stopping synchronization...");
//                }
//                await Task.Delay(100);
//            }

//            await syncTask;
//            PrintInfo("\nSynchronization stopped. Press any key to exit.");
//            Console.CursorVisible = true;
//            Console.ReadKey();
//        }


//        static string ReadPassword()
//        {
//            string password = "";
//            ConsoleKeyInfo key;

//            do
//            {
//                key = Console.ReadKey(true);

//                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
//                {
//                    password += key.KeyChar;
//                    Console.Write("*");
//                }
//                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
//                {
//                    password = password.Substring(0, (password.Length - 1));
//                    Console.Write("\b \b");
//                }
//            } while (key.Key != ConsoleKey.Enter);

//            Console.WriteLine();
//            return password;
//        }
//        static void SyncTableStructure(string sourceConnStr, string targetConnStr, string tableName)
//        {
//            try
//            {
//                using var targetConn = new OleDbConnection(targetConnStr);
//                targetConn.Open();

//                // Skip if table already exists in target
//                if (TableExists(targetConn, tableName)) return;

//                using var sourceConn = new OleDbConnection(sourceConnStr);
//                sourceConn.Open();

//                string pkColumn = GetPrimaryKeyColumn(sourceConnStr, tableName);
//                string pkColumn1 = GetPrimaryKeyColumn(targetConnStr, tableName);

//                if (string.IsNullOrEmpty(pkColumn))
//                {

//                    ExecuteNonQuery(sourceConn, $"ALTER TABLE [{tableName}] ADD COLUMN [DefaultSynCID] GUID");
//                    ExecuteNonQuery(sourceConn, $"ALTER TABLE [{tableName}] ADD CONSTRAINT pk_{tableName}_DefaultSynCID PRIMARY KEY ([DefaultSynCID])");

//                }
//                if (string.IsNullOrEmpty(pkColumn1))
//                {

//                    ExecuteNonQuery(sourceConn, $"ALTER TABLE [{tableName}] ADD COLUMN [DefaultSynCID] GUID");
//                    ExecuteNonQuery(sourceConn, $"ALTER TABLE [{tableName}] ADD CONSTRAINT pk_{tableName}_DefaultSynCID PRIMARY KEY ([DefaultSynCID])");

//                }
//                // Get table schema from source
//                DataTable schema = sourceConn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables,
//                        new object[] { null, null, tableName, "TABLE" });

//                if (schema.Rows.Count == 0) return;

//                // Get columns information
//                DataTable columns = sourceConn.GetOleDbSchemaTable(OleDbSchemaGuid.Columns,
//                    new object[] { null, null, tableName, null });

//                // Get primary key information
//                DataTable primaryKeys = sourceConn.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys,
//                    new object[] { null, null, tableName });

//                // Build CREATE TABLE statement
//                var createTableSql = new StringBuilder($"CREATE TABLE [{tableName}] (");

//                foreach (DataRow column in columns.Rows)
//                {
//                    string columnName = column["COLUMN_NAME"].ToString();
//                    string dataType = GetSqlDataType(column);
//                    bool isPrimaryKey = primaryKeys.Select($"COLUMN_NAME = '{columnName}'").Length > 0;

//                    createTableSql.Append($"[{columnName}] {dataType}");

//                    if (isPrimaryKey)
//                        createTableSql.Append(" PRIMARY KEY");

//                    createTableSql.Append(", ");
//                }

//                // Add Serverzeit if not exists
//                if (columns.Select("COLUMN_NAME = 'Serverzeit'").Length == 0)
//                {
//                    createTableSql.Append("[Serverzeit] DATETIME DEFAULT Now(), ");
//                }

//                // Remove trailing comma and close
//                createTableSql.Length -= 2;
//                createTableSql.Append(")");

//                // Execute creation
//                ExecuteNonQuery(targetConn, createTableSql.ToString());
//                Console.WriteLine($"Created table {tableName} in target database");


//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error synchronizing table {tableName}: {ex.Message}");
//            }
//        }
//        static void ExecuteNonQuery(OleDbConnection conn, string sql, params (string, object)[] parameters)
//        {
//            using var cmd = new OleDbCommand(sql, conn);
//            foreach (var (name, value) in parameters)
//            {
//                cmd.Parameters.AddWithValue(name, value);
//            }
//            cmd.ExecuteNonQuery();
//        }
//        static void PrintHeader()
//        {
//            Console.ForegroundColor = ConsoleColor.Cyan;
//            Console.WriteLine("\nDatabase Synchronization Tool");
//            Console.WriteLine("-----------------------------");
//            Console.ResetColor();
//        }

//        static void PrintInfo(string message)
//        {
//            Console.ForegroundColor = ConsoleColor.Cyan;
//            Console.WriteLine($"[{DateTime.Now:T}] {message}");
//            Console.ResetColor();
//        }

//        static void PrintSuccess(string message)
//        {
//            Console.ForegroundColor = ConsoleColor.Green;
//            Console.WriteLine($"[{DateTime.Now:T}] {message}");
//            Console.ResetColor();
//        }

//        static void PrintWarning(string message)
//        {
//            Console.ForegroundColor = ConsoleColor.Yellow;
//            Console.WriteLine($"[{DateTime.Now:T}] {message}");
//            Console.ResetColor();
//        }

//        static void PrintError(string message)
//        {
//            Console.ForegroundColor = ConsoleColor.Red;
//            Console.WriteLine($"[{DateTime.Now:T}] {message}");
//            Console.ResetColor();
//        }


//        static void InitializeMetadata(SyncMetadata metadata, string clientConnStr, string serverConnStr, bool isNewClientDb)
//        {
//            var allTables = GetAllTableNames(clientConnStr)
//                .Union(GetAllTableNames(serverConnStr))
//                .Distinct(StringComparer.OrdinalIgnoreCase)
//                .ToList();

//            //var clientTables = GetAllTableNames(clientConnStr);
//            //var serverTables = GetAllTableNames(serverConnStr);
//            //var allTables = clientTables.Union(serverTables).ToList();

//            // Bi-directional table structure sync
//            foreach (var table in allTables)
//            {

//                // Client → Server
//                SyncTableStructure(clientConnStr, serverConnStr, table);
//                // Server → Client
//                SyncTableStructure(serverConnStr, clientConnStr, table);
//            }

//            foreach (var table in allTables)
//            {


//                if (!metadata.TableLastSync.ContainsKey(table))
//                {
//                    try
//                    {
//                        using var conn = new OleDbConnection(clientConnStr);
//                        conn.Open();
//                        // First check if Serverzeit column exists
//                        bool hasServerzeit = ColumnExists(conn, table, "Serverzeit");
//                        if (!hasServerzeit)
//                        {
//                            metadata.TableLastSync[table] = DateTime.UtcNow;
//                            continue;
//                        }
//                        using var cmd = new OleDbCommand($"SELECT MAX(Serverzeit) FROM [{table}]", conn);
//                        var result = cmd.ExecuteScalar();

//                        if (result != DBNull.Value && result != null)
//                        {


//                            metadata.TableLastSync[table] = (DateTime)result;
//                            PrintInfo($"Initialized table '{table}' with max Serverzeit: {(DateTime)result:yyyy-MM-dd HH:mm:ss}");

//                        }
//                        else
//                        {
//                            metadata.TableLastSync[table] = DateTime.MinValue;
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        metadata.TableLastSync[table] = DateTime.MinValue;
//                        //PrintWarning($"Could not initialize metadata for table '{table}': {ex.Message}");
//                    }
//                }
//            }
//        }

//        // Helper method to check if column exists
//        static bool ColumnExists(OleDbConnection conn, string tableName, string columnName)
//        {
//            try
//            {
//                DataTable columns = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Columns,
//                    new object[] { null, null, tableName, null });

//                return columns.Rows.Cast<DataRow>()
//                    .Any(row => row["COLUMN_NAME"].ToString().Equals(columnName, StringComparison.OrdinalIgnoreCase));
//            }
//            catch
//            {
//                return false;
//            }
//        }
//        static bool RunCommand(string command, bool showOutput = true)
//        {
//            try
//            {
//                ProcessStartInfo procInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
//                {
//                    RedirectStandardOutput = !showOutput,
//                    RedirectStandardError = !showOutput,
//                    UseShellExecute = false,
//                    CreateNoWindow = !showOutput
//                };

//                using (Process proc = Process.Start(procInfo))
//                {
//                    proc.WaitForExit();
//                    return proc.ExitCode == 0;
//                }
//            }
//            catch (Exception ex)
//            {
//                PrintError("Command execution failed: " + ex.Message);
//                return false;
//            }
//        }

//        public static bool HasMdbExtension(string path)
//        {
//            if (string.IsNullOrWhiteSpace(path))
//                return false;

//            string extension = Path.GetExtension(path);


//            return extension.Equals(".mdb", StringComparison.OrdinalIgnoreCase) || extension.Equals(".crm", StringComparison.OrdinalIgnoreCase);
//        }

//        static async Task<bool> PullDatabaseFromServer(string serverPath, string clientPath)
//        {
//            try
//            {
//                var serverParts = serverPath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
//                if (serverParts.Length < 2)
//                {
//                    PrintError("Invalid server path format. Expected format: \\\\server\\share\\path\\file.mdb");
//                    return false;
//                }

//                string serverIP = serverParts[0];
//                string shareName = serverParts[1];
//                string serverFilePath = string.Join("\\", serverParts.Skip(2));
//                string fileName = Path.GetFileName(serverPath);

//                if (!PingHost("127.0.0.1") || !PingHost(serverIP))
//                {
//                    PrintError("ERROR: Network connectivity issues");
//                    return false;
//                }

//                bool isClientPathDirectory = Directory.Exists(clientPath) ||
//                                           (clientPath.EndsWith("\\") ||
//                                            clientPath.EndsWith("/"));

//                string finalClientPath;
//                if (isClientPathDirectory)
//                {
//                    Directory.CreateDirectory(clientPath);
//                    finalClientPath = Path.Combine(clientPath, fileName);
//                }
//                else
//                {
//                    string directory = Path.GetDirectoryName(clientPath);
//                    if (!string.IsNullOrEmpty(directory))
//                    {
//                        Directory.CreateDirectory(directory);
//                    }
//                    finalClientPath = clientPath;
//                }

//                RunCommand($"net use {DRIVE_LETTER} /delete", false);

//                PrintInfo("Mounting server share...");
//                string connectCmd = $"net use {DRIVE_LETTER} \\\\{serverIP}\\{shareName} /user:{USERNAME} {PASSWORD} /persistent:no";
//                if (!RunCommand(connectCmd))
//                {
//                    PrintError("ERROR: Failed to connect to shared folder");
//                    return false;
//                }

//                string serverFile = $"{DRIVE_LETTER}\\{serverFilePath}";
//                if (!File.Exists(serverFile))
//                {
//                    PrintError($"ERROR: File does not exist on server: {serverFilePath}");
//                    RunCommand($"net use {DRIVE_LETTER} /delete", false);
//                    return false;
//                }

//                PrintInfo($"Copying file from server to {finalClientPath}...");
//                try
//                {
//                    File.Copy(serverFile, finalClientPath, true);
//                    PrintSuccess("File successfully copied");
//                    return true;
//                }

//                catch (Exception ex)
//                {
//                    PrintError($"ERROR: File copy failed: {ex.Message}");
//                    return false;
//                }
//                finally
//                {
//                    RunCommand($"net use {DRIVE_LETTER} /delete", false);
//                }
//            }
//            catch (Exception ex)
//            {
//                PrintError($"Error pulling database from server: {ex.Message}");
//                return false;
//            }
//        }

//        static bool CheckNetworkConnection(string ip)
//        {
//            try
//            {
//                using (var tcpClient = new TcpClient())
//                {
//                    var result = tcpClient.BeginConnect(ip, 445, null, null);
//                    var success = result.AsyncWaitHandle.WaitOne(2000);
//                    if (tcpClient.Connected) tcpClient.EndConnect(result);
//                    return success;
//                }
//            }
//            catch
//            {
//                return false;
//            }
//        }

//        static async Task ContinuousSync(
//            string serverConnStr,
//            string clientConnStr,
//            string syncMetaFile,
//            SyncMetadata metadata)
//        {

//            while (_syncRunning)
//            {
//                try
//                {
//                    var clientTables = GetAllTableNames(clientConnStr);
//                    var serverTables = GetAllTableNames(serverConnStr);
//                    var allTables = clientTables.Union(serverTables).ToList();

//                    // Bi-directional table structure sync
//                    foreach (var table in allTables)
//                    {
//                        // Client → Server
//                        SyncTableStructure(clientConnStr, serverConnStr, table);

//                        // Server → Client
//                        SyncTableStructure(serverConnStr, clientConnStr, table);
//                        _cycleTimer.Restart();
//                        PrintInfo($"Starting sync cycle at {DateTime.Now:T}");

//                        _isOnline = CheckNetworkConnection(SERVER_IP);

//                        if (_isOnline)
//                        {
//                            if (_lastOnlineTime == DateTime.MinValue)
//                            {
//                                PrintSuccess("Connection restored");
//                            }
//                            _lastOnlineTime = DateTime.Now;

//                            if (metadata.QueuedChanges.Count > 0)
//                            {
//                                PrintInfo($"Processing {metadata.QueuedChanges.Count} queued changes");
//                                await ProcessQueuedChanges(metadata, clientConnStr, serverConnStr);
//                                SaveSyncMetadata(syncMetaFile, metadata);
//                            }

//                            int totalChanges = 0;
//                            foreach (var tableName in allTables)
//                            {
//                                try
//                                {

//                                    DateTime lastSync = metadata.TableLastSync.ContainsKey(tableName)
//                                        ? metadata.TableLastSync[tableName]
//                                        : DateTime.MinValue;

//                                    PrintInfo($"Syncing {tableName} since {lastSync:yyyy-MM-dd HH:mm:ss}");

//                                    int serverToClient = await SyncDirection(
//                                        sourceConnStr: serverConnStr,
//                                        targetConnStr: clientConnStr,
//                                        tableName: tableName,
//                                        lastSync: lastSync,
//                                        isServerToClient: true,
//                                        metadata: metadata
//                                    );

//                                    int clientToServer = await SyncDirection(
//                                        sourceConnStr: clientConnStr,
//                                        targetConnStr: serverConnStr,
//                                        tableName: tableName,
//                                        lastSync: lastSync,
//                                        isServerToClient: false,
//                                        metadata: metadata
//                                    );

//                                    if (serverToClient > 0 || clientToServer > 0)
//                                    {
//                                        PrintSuccess($"{tableName} sync: Server→Client: {serverToClient}, Client→Server: {clientToServer}");
//                                        metadata.TableLastSync[tableName] = DateTime.UtcNow;
//                                        totalChanges += serverToClient + clientToServer;
//                                    }
//                                    else
//                                    {
//                                        PrintInfo($"No changes for {tableName}");
//                                    }
//                                }
//                                catch (Exception ex)
//                                {
//                                    PrintError($"Error syncing table {tableName}: {ex.Message}");
//                                }
//                            }

//                            _cycleTimer.Stop();
//                            PrintSuccess($"Sync cycle completed in {_cycleTimer.Elapsed.TotalSeconds:0.00} seconds. Total changes: {totalChanges}");

//                            if (totalChanges == 0)
//                            {
//                                PrintInfo("No changes detected in this cycle");
//                            }
//                        }
//                        else
//                        {
//                            if (_lastOnlineTime != DateTime.MinValue)
//                            {
//                                PrintWarning("Connection lost - entering offline mode");
//                                _lastOnlineTime = DateTime.MinValue;
//                            }

//                            await QueueLocalChanges(metadata, clientConnStr);
//                        }

//                        // Wait for next cycle with countdown
//                        _nextSyncTime = DateTime.Now.AddMinutes(_syncCycleWaitMinutes);
//                        PrintInfo($"Next sync at {_nextSyncTime:T}");

//                        while (DateTime.Now < _nextSyncTime && _syncRunning)
//                        {
//                            TimeSpan remaining = _nextSyncTime - DateTime.Now;
//                            Console.Write($"\rWaiting for next sync in {remaining.Minutes}:{remaining.Seconds:00}...");
//                            await Task.Delay(100);
//                        }

//                        Console.WriteLine();
//                    }
//                }

//                catch (Exception ex)
//                {
//                    PrintError($"Sync cycle error: {ex.Message}");
//                }
//                finally
//                {
//                    SaveSyncMetadata(syncMetaFile, metadata);
//                }
//            }
//        }

//        static async Task ProcessQueuedChanges(
//            SyncMetadata metadata,
//            string clientConnStr,
//            string serverConnStr)
//        {
//            var processedChanges = new List<QueuedChange>();

//            foreach (var change in metadata.QueuedChanges)
//            {
//                try
//                {
//                    if (change.IsDelete)
//                    {
//                        using (var conn = new OleDbConnection(serverConnStr))
//                        {
//                            conn.Open();
//                            DeleteRecord(conn, change.TableName, change.PkColumn, change.RowData[change.PkColumn]);
//                            PrintSuccess($"Applied queued delete for {change.TableName} (PK: {change.RowData[change.PkColumn]})");
//                        }
//                    }
//                    else
//                    {
//                        using (var conn = new OleDbConnection(serverConnStr))
//                        {
//                            conn.Open();
//                            if (ApplyChangeWithConflictResolution(
//                                conn,
//                                change.TableName,
//                                change.RowData,
//                                isServerToClient: false,
//                                change.PkColumn))
//                            {
//                                PrintSuccess($"Applied queued change for {change.TableName} (PK: {change.RowData[change.PkColumn]})");
//                            }
//                        }
//                    }

//                    processedChanges.Add(change);
//                }
//                catch (Exception ex)
//                {
//                    PrintError($"Failed to apply queued change: {ex.Message}");
//                }
//            }

//            foreach (var change in processedChanges)
//            {
//                metadata.QueuedChanges.Remove(change);
//            }
//        }

//        static async Task QueueLocalChanges(SyncMetadata metadata, string clientConnStr)
//        {
//            foreach (var tableName in GetAllTableNames(clientConnStr))
//            {
//                try
//                {
//                    DateTime lastSync = metadata.TableLastSync.ContainsKey(tableName)
//                        ? metadata.TableLastSync[tableName]
//                        : DateTime.MinValue;

//                    string pkColumn = GetPrimaryKeyColumn(clientConnStr, tableName);
//                    if (string.IsNullOrEmpty(pkColumn)) continue;

//                    // Queue inserts/updates
//                    using (var conn = new OleDbConnection(clientConnStr))
//                    {
//                        conn.Open();
//                        string query = $@"SELECT * FROM [{tableName}] WHERE Serverzeit > @lastSync";

//                        using (var cmd = new OleDbCommand(query, conn))
//                        {
//                            cmd.Parameters.AddWithValue("@lastSync", lastSync);

//                            using (var reader = cmd.ExecuteReader())
//                            {
//                                while (reader.Read())
//                                {
//                                    var row = new Dictionary<string, object>();
//                                    for (int i = 0; i < reader.FieldCount; i++)
//                                    {
//                                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
//                                    }

//                                    metadata.QueuedChanges.Add(new QueuedChange
//                                    {
//                                        TableName = tableName,
//                                        PkColumn = pkColumn,
//                                        RowData = row,
//                                        IsDelete = false,
//                                        ChangeTime = DateTime.UtcNow
//                                    });
//                                }
//                            }
//                        }
//                    }

//                    // Queue deletes by checking for records that exist in metadata but not in database
//                    var existingPks = new HashSet<object>();
//                    using (var conn = new OleDbConnection(clientConnStr))
//                    {
//                        conn.Open();
//                        string query = $"SELECT [{pkColumn}] FROM [{tableName}]";

//                        using (var cmd = new OleDbCommand(query, conn))
//                        using (var reader = cmd.ExecuteReader())
//                        {
//                            while (reader.Read())
//                            {
//                                existingPks.Add(reader.IsDBNull(0) ? null : reader.GetValue(0));
//                            }
//                        }
//                    }

//                    // Check queued changes for records that might have been deleted
//                    foreach (var change in metadata.QueuedChanges.ToList())
//                    {
//                        if (change.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase) &&
//                            !change.IsDelete && !existingPks.Contains(change.RowData[change.PkColumn]))
//                        {
//                            // Convert update to delete
//                            metadata.QueuedChanges.Remove(change);
//                            metadata.QueuedChanges.Add(new QueuedChange
//                            {
//                                TableName = tableName,
//                                PkColumn = pkColumn,
//                                RowData = new Dictionary<string, object> { { pkColumn, change.RowData[pkColumn] } },
//                                IsDelete = true,
//                                ChangeTime = DateTime.UtcNow
//                            });
//                        }
//                    }
//                }
//                catch (Exception ex)
//                {
//                    PrintError($"Error queuing changes for {tableName}: {ex.Message}");
//                }
//            }
//        }

//        static async Task<int> SyncDirection(
//        string sourceConnStr,
//            string targetConnStr,
//            string tableName,
//            DateTime lastSync,
//            bool isServerToClient,
//            SyncMetadata metadata)
//        {
//            int changesApplied = 0;
//            DateTime maxTimestamp = lastSync;

//            try
//            {
//                using (var sourceConn = new OleDbConnection(sourceConnStr))
//                {
//                    sourceConn.Open();

//                    string pkColumn = GetPrimaryKeyColumn(sourceConnStr, tableName);
//                    //if (string.IsNullOrEmpty(pkColumn))
//                    //{

//                    //    ExecuteNonQuery(sourceConn, $"ALTER TABLE [{tableName}] ADD COLUMN [DefaultSynCID] GUID");
//                    //    ExecuteNonQuery(sourceConn, $"ALTER TABLE [{tableName}] ADD PRIMARY KEY ([SyncId])");

//                    //}

//                    using (var targetconn = new OleDbConnection(targetConnStr))
//                    {
//                        targetconn.Open();
//                        if (!TableExists(sourceConn, tableName) || !TableExists(targetconn, tableName))
//                        {
//                            CreateTableFromSource(sourceConn, targetconn, tableName);
//                        }
//                    }

//                    //string query = $@"SELECT * FROM [{tableName}] 
//                    //                WHERE Serverzeit > @lastSync
//                    //                ORDER BY Serverzeit";
//                    // Modified query with DESC and optional TOP clause for better performance

//                    string query = $@"SELECT * FROM [{tableName}] 
//                                                WHERE Serverzeit > @lastSync
//                                                ORDER BY Serverzeit DESC";  // Newest first

//                    using (var cmd = new OleDbCommand(query, sourceConn))
//                    {
//                        cmd.Parameters.AddWithValue("@lastSync", lastSync);

//                        using (var reader = cmd.ExecuteReader())
//                        {
//                            while (reader.Read())
//                            {
//                                var row = new Dictionary<string, object>();
//                                for (int i = 0; i < reader.FieldCount; i++)
//                                {
//                                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
//                                }

//                                using (var targetConn = new OleDbConnection(targetConnStr))
//                                {
//                                    targetConn.Open();

//                                    if (ApplyChangeWithConflictResolution(
//                                    targetConn,
//                                    tableName,
//                                    row,
//                                    isServerToClient,
//                                    pkColumn))
//                                    {
//                                        changesApplied++;
//                                        var rowTimestamp = Convert.ToDateTime(row["Serverzeit"]);
//                                        if (rowTimestamp > maxTimestamp)
//                                            maxTimestamp = rowTimestamp;
//                                    }
//                                }
//                            }
//                        }
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                // PrintError($"Error syncing {tableName}: {ex.Message}");
//            }

//            if (changesApplied > 0 && maxTimestamp > lastSync)
//            {
//                metadata.TableLastSync[tableName] = maxTimestamp;
//            }

//            return changesApplied;
//        }


//        static bool ApplyChangeWithConflictResolution(
//             OleDbConnection targetConn,
//             string tableName,
//             Dictionary<string, object> row,
//             bool isServerToClient,
//             string pkColumn)
//        {
//            try
//            {
//                var pkValue = row[pkColumn];

//                //if (!TableExists(targetConn, tableName))
//                //{
//                //    CreateTableFromSource(targetConn, row, tableName);
//                //}

//                bool exists = RecordExists(targetConn, tableName, pkColumn, pkValue);

//                if (!exists)
//                {
//                    return InsertRecord(targetConn, tableName, row);
//                }

//                var targetLastModified = GetLastModified(targetConn, tableName, pkColumn, pkValue);
//                var incomingLastModified = Convert.ToDateTime(row["Serverzeit"]);

//                if (isServerToClient || incomingLastModified > targetLastModified)
//                {
//                    return UpdateRecord(targetConn, tableName, row, pkColumn);
//                }

//                return false;
//            }
//            catch (Exception ex)
//            {
//                PrintError($"Error applying change to {tableName}: {ex.Message}");
//                return false;
//            }
//        }


//        static void CreateTableFromSource(OleDbConnection sourceConn, OleDbConnection targetConn, string tableName)
//        {
//            try
//            {
//                // Get table schema from source
//                DataTable schema = sourceConn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables,
//                    new object[] { null, null, tableName, "TABLE" });

//                if (schema.Rows.Count == 0) return;

//                // Get columns information
//                DataTable columns = sourceConn.GetOleDbSchemaTable(OleDbSchemaGuid.Columns,
//                    new object[] { null, null, tableName, null });

//                // Get primary key information
//                DataTable primaryKeys = sourceConn.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys,
//                    new object[] { null, null, tableName });

//                // Build CREATE TABLE statement
//                var createTableSql = new StringBuilder($"CREATE TABLE [{tableName}] (");

//                foreach (DataRow column in columns.Rows)
//                {
//                    string columnName = column["COLUMN_NAME"].ToString();
//                    string dataType = GetSqlDataType(column);
//                    bool isPrimaryKey = primaryKeys.Select($"COLUMN_NAME = '{columnName}'").Length > 0;

//                    createTableSql.Append($"[{columnName}] {dataType}");

//                    if (isPrimaryKey)
//                        createTableSql.Append(" PRIMARY KEY");

//                    createTableSql.Append(", ");
//                }

//                // Add LastModified column if it doesn't exist
//                if (columns.Select("COLUMN_NAME = 'Serverzeit'").Length == 0)
//                {
//                    createTableSql.Append("[Serverzeit] DATETIME DEFAULT Now(), ");
//                }

//                // Remove trailing comma and close statement
//                createTableSql.Length -= 2;
//                createTableSql.Append(")");

//                // Execute creation
//                using var cmd = new OleDbCommand(createTableSql.ToString(), targetConn);
//                cmd.ExecuteNonQuery();

//                Console.WriteLine($"Created table {tableName} in {targetConn} database");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error creating table {tableName}: {ex.Message}");
//            }
//        }

//        static string GetSqlDataType(DataRow column)

//        {
//            int oleDbType = (int)column["DATA_TYPE"];
//            int size = column["CHARACTER_MAXIMUM_LENGTH"] is DBNull ? 0 : Convert.ToInt32(column["CHARACTER_MAXIMUM_LENGTH"]);

//            switch (oleDbType)
//            {
//                case 130: return size > 0 ? $"TEXT({size})" : "TEXT(255)";
//                case 3: return "INTEGER";
//                case 5: return "DOUBLE";
//                case 7: return "DATETIME";
//                case 11: return "BIT";
//                case 72: return "GUID";
//                case 203: return "MEMO";
//                default: return "TEXT(255)";
//            }
//        }

//        static SyncMetadata LoadSyncMetadata(string path)
//        {
//            if (!File.Exists(path)) return new SyncMetadata();

//            try
//            {
//                var json = File.ReadAllText(path);
//                return JsonSerializer.Deserialize<SyncMetadata>(json);
//            }
//            catch (Exception ex)
//            {
//                PrintError($"Error loading metadata: {ex.Message}");
//                return new SyncMetadata();
//            }
//        }

//        static void SaveSyncMetadata(string path, SyncMetadata metadata)
//        {
//            try
//            {
//                var options = new JsonSerializerOptions { WriteIndented = true };
//                var json = JsonSerializer.Serialize(metadata, options);
//                File.WriteAllText(path, json);
//            }
//            catch (Exception ex)
//            {
//                PrintError($"Error saving metadata: {ex.Message}");
//            }
//        }

//        static bool PingHost(string nameOrAddress)
//        {
//            try
//            {
//                using (var ping = new Ping())
//                {
//                    var reply = ping.Send(nameOrAddress, 2000);
//                    return reply.Status == IPStatus.Success;
//                }
//            }
//            catch
//            {
//                return false;
//            }
//        }

//        static void ShowGameStyleLoader(string message, int totalSteps)
//        {
//            Console.Write(message + " ");
//            int progressBarWidth = 30;

//            for (int i = 0; i <= totalSteps; i++)
//            {
//                double percentage = (double)i / totalSteps;
//                int filledBars = (int)(percentage * progressBarWidth);
//                string bar = new string('█', filledBars).PadRight(progressBarWidth, '-');

//                Console.Write($"\r{message} [{bar}] {percentage * 100:0}%");
//                Thread.Sleep(20);
//            }
//            Console.WriteLine();
//        }

//        static bool VerifyDatabaseFiles(string clientPath, string serverPath)
//        {
//            if (!File.Exists(clientPath))
//            {
//                PrintError($"\nClient database not found at: {clientPath}");
//                return false;
//            }

//            if (!File.Exists(serverPath))
//            {
//                PrintError($"\nServer database not found at: {serverPath}");
//                return false;
//            }

//            return true;
//        }

//        static bool TestConnection(string name, string connectionString)
//        {
//            try
//            {
//                using var connection = new OleDbConnection(connectionString);
//                connection.Open();
//                PrintSuccess($"{name} connection successful");
//                return true;
//            }
//            catch (Exception ex)
//            {
//                PrintError($"\n{name} connection failed: {ex.Message}");
//                return false;
//            }
//        }

//        static List<string> GetAllTableNames(string connectionString)
//        {
//            var tables = new List<string>();
//            try
//            {
//                using var conn = new OleDbConnection(connectionString);
//                conn.Open();
//                DataTable schemaTables = conn.GetSchema("Tables");

//                foreach (DataRow row in schemaTables.Rows)
//                {
//                    string tableName = row["TABLE_NAME"].ToString();
//                    string tableType = row["TABLE_TYPE"].ToString();

//                    if (tableType == "TABLE" && !tableName.StartsWith("MSys")
//                        && !tableName.StartsWith("~TMP") && !tableName.StartsWith("_"))
//                    {
//                        tables.Add(tableName);
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                PrintError($"Error getting table names: {ex.Message}");
//            }
//            return tables;
//        }

//        static string GetPrimaryKeyColumn(string connectionString, string tableName)
//        {
//            try
//            {
//                using var conn = new OleDbConnection(connectionString);
//                conn.Open();

//                DataTable schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys,
//                    new object[] { null, null, tableName });

//                if (schema.Rows.Count > 0)
//                {
//                    return schema.Rows[0]["COLUMN_NAME"].ToString();
//                }

//                DataTable columns = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Columns,
//                    new object[] { null, null, tableName, null });

//                foreach (DataRow row in columns.Rows)
//                {
//                    string col = row["COLUMN_NAME"].ToString();

//                    if (col.Equals("ID", StringComparison.OrdinalIgnoreCase) ||
//                        col.Equals("GUID", StringComparison.OrdinalIgnoreCase))
//                    {
//                        return col;
//                    }
//                }

//                return null;
//            }
//            catch
//            {
//                return null;
//            }
//        }

//        static bool TableExists(OleDbConnection conn, string tableName)
//        {
//            try
//            {
//                DataTable schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables,
//                    new object[] { null, null, tableName, "TABLE" });
//                return schema.Rows.Count > 0;
//            }
//            catch
//            {
//                return false;
//            }
//        }

//        static bool RecordExists(OleDbConnection conn, string tableName, string pkColumn, object pkValue)
//        {
//            try
//            {
//                string query = $"SELECT COUNT(*) FROM [{tableName}] WHERE [{pkColumn}] = ?";
//                using var cmd = new OleDbCommand(query, conn);
//                cmd.Parameters.AddWithValue($"@{pkColumn}", pkValue);
//                return (int)cmd.ExecuteScalar() > 0;
//            }
//            catch
//            {
//                return false;
//            }
//        }

//        static bool InsertRecord(OleDbConnection conn, string tableName, Dictionary<string, object> row)
//        {
//            try
//            {
//                var columns = row.Keys.ToList();
//                var columnList = string.Join(", ", columns.Select(c => $"[{c}]"));
//                var valuePlaceholders = string.Join(", ", columns.Select(_ => "?"));

//                string insertQuery = $@"INSERT INTO [{tableName}] ({columnList}) VALUES ({valuePlaceholders})";

//                using var cmd = new OleDbCommand(insertQuery, conn);
//                foreach (var col in columns)
//                    cmd.Parameters.AddWithValue($"@{col}", row[col] ?? DBNull.Value);

//                return cmd.ExecuteNonQuery() > 0;
//            }
//            catch (Exception ex)
//            {
//                PrintError($"Error inserting record into {tableName}: {ex.Message}");
//                return false;
//            }
//        }

//        static bool UpdateRecord(OleDbConnection conn, string tableName, Dictionary<string, object> row, string pkColumn)
//        {
//            try
//            {
//                var columns = row.Keys.Where(k => k != pkColumn).ToList();
//                var updateSet = string.Join(", ", columns.Select(c => $"[{c}] = ?"));
//                string updateQuery = $@"UPDATE [{tableName}] SET {updateSet} WHERE [{pkColumn}] = ?";

//                using var cmd = new OleDbCommand(updateQuery, conn);
//                foreach (var col in columns)
//                    cmd.Parameters.AddWithValue($"@{col}", row[col] ?? DBNull.Value);
//                cmd.Parameters.AddWithValue($"@{pkColumn}", row[pkColumn]);

//                return cmd.ExecuteNonQuery() > 0;
//            }
//            catch (Exception ex)
//            {
//                PrintError($"Error updating record in {tableName}: {ex.Message}");
//                return false;
//            }
//        }

//        static DateTime GetLastModified(OleDbConnection conn, string tableName, string pkColumn, object pkValue)
//        {
//            try
//            {
//                string query = $"SELECT Serverzeit FROM [{tableName}] WHERE [{pkColumn}] = ?";
//                using var cmd = new OleDbCommand(query, conn);
//                cmd.Parameters.AddWithValue($"@{pkColumn}", pkValue);
//                var result = cmd.ExecuteScalar();
//                return (result != DBNull.Value && result != null) ? Convert.ToDateTime(result) : DateTime.MinValue;
//            }
//            catch
//            {
//                return DateTime.MinValue;
//            }
//        }

//        static void DeleteRecord(OleDbConnection conn, string tableName, string pkColumn, object pkValue)
//        {
//            try
//            {
//                string query = $"DELETE FROM [{tableName}] WHERE [{pkColumn}] = ?";
//                using var cmd = new OleDbCommand(query, conn);
//                cmd.Parameters.AddWithValue($"@{pkColumn}", pkValue);
//                cmd.ExecuteNonQuery();
//                PrintSuccess($"Deleted record from {tableName} (PK: {pkValue})");
//            }
//            catch (Exception ex)
//            {
//                PrintError($"Error deleting record from {tableName}: {ex.Message}");
//            }
//        }
//    }
//}