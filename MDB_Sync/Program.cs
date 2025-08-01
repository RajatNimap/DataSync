﻿using System.Text.Json;
using System.Data.OleDb;
using System.Data;
using System.Net.NetworkInformation;
using System.Text;
using System.Diagnostics;
using System.Net.Sockets;
using IniParser;
using IniParser.Model;

namespace RobustAccessDbSync
{
    // Replace existing SyncMetadata with this version

    class SyncTimestamps
    {
        public DateTime ServerToClient { get; set; } = DateTime.MinValue;
        public DateTime ClientToServer { get; set; } = DateTime.MinValue;
    }

    class SyncMetadata
    {
        public Dictionary<string, SyncTimestamps> TableLastSync { get; set; } = new();
    }
 
    class Program
    {
        static string DRIVE_LETTER = "X:";
        static bool _syncRunning = true;
        static bool _isOnline = true;
        static DateTime _lastOnlineTime = DateTime.MinValue;
        static int _syncCycleWaitMinutes = 1;
        static Stopwatch _cycleTimer = new Stopwatch();
        static DateTime _nextSyncTime = DateTime.Now;
        static string clientDbPath;
        static string serverDbPath;
        static string SERVER_IP;
        static string SHARE_NAME;
        static string USERNAME;
        static string PASSWORD;
        static string? rememberedClientPath = null;
        static string  clientFileFolder;
        static List<string> Client_Folders = [];
        static string syncMetaFile = "sync_metadata.json";

        static void GetClientsServerPath()
        {
            Console.Title = "Database Synchronization Tool";
            Console.CursorVisible = false;
            PrintHeader();
            ShowGameStyleLoader("Initializing Database Synchronization Tool", 20);
            while (true)
            {
                do
                {
                    Console.Write("Enter Client Path: ");
                    clientDbPath = Console.ReadLine();

                    if(!Directory.Exists(Path.GetDirectoryName(clientDbPath)) || string.IsNullOrWhiteSpace(clientDbPath))
                    {
                        Console.WriteLine($"Path should be valid and cannot empty");
                        clientDbPath = string.Empty; // Reset to re-prompt
                    }
                } while (string.IsNullOrWhiteSpace(clientDbPath));

                do
                {
                    Console.Write("Enter Server Path: ");
                    serverDbPath = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(serverDbPath))
                        Console.WriteLine("server path cannot empty");
                } while (string.IsNullOrWhiteSpace(serverDbPath));

                var serverParts = serverDbPath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                if (serverParts.Length < 2)
                {
                    PrintError("Invalid server path format. Expected format: \\\\server\\share\\path\\file.mdb");
                }

                SERVER_IP = serverParts[0];
                SHARE_NAME = serverParts[1];


                Console.WriteLine("\nPress Enter to continue or type 'r' to re-enter:");

                string input = Console.ReadLine()?.Trim().ToLower();

                if (string.IsNullOrEmpty(input))
                {
                    break;
                }
                else if (input == "r")
                    continue;
                else
                    Console.WriteLine("Invalid input. Re-entering...\n");
            }
        }
        static void GetServerCredentials()
        {
            while (true)
            {
                do
                {
                    Console.Write("Enter USERNAME: ");
                    USERNAME = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(USERNAME))
                        Console.WriteLine("USERNAME cannot be empty.");
                } while (string.IsNullOrWhiteSpace(USERNAME));

                do
                {
                    Console.Write("Enter PASSWORD: ");
                    PASSWORD = ReadPassword();
                    if (string.IsNullOrWhiteSpace(PASSWORD))
                        Console.WriteLine("PASSWORD cannot be empty.");
                } while (string.IsNullOrWhiteSpace(PASSWORD));

                Console.WriteLine("\nPress Enter to continue or type 'r' to re-enter:");

                string input = Console.ReadLine()?.Trim().ToLower();

                if (string.IsNullOrEmpty(input))
                {
                    break;
                }
                else if (input == "r")
                    continue;
                else
                    Console.WriteLine("Invalid input. Re-entering...\n");
            }
        }
    
        static void GetClientPathCredentials()
        {
            Client_Folders.Clear();
            int i = 1;
            while (true)
            {
                Console.Write($"Enter client folder path #{i} (leave blank to stop): ");
                string path = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(path))
                    break;

                if (Directory.Exists(path))
                {
                    Client_Folders.Add(path);
                    i++;
                }
                else
                {
                    Console.WriteLine("Invalid folder. Please try again.");
                }
            }

            if (Client_Folders.Count == 0)
                Console.WriteLine("Warning: No folders entered for sync.");
        }


        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        static async Task Main()
        {
            var parser = new FileIniDataParser();
            IniData data = null;
            const string pointerFile = "last_path.txt"; // Stores last-used client DB path
            try
            {

                // Step 1: Try to read last-used client path
                if (File.Exists(pointerFile))
                {
                    rememberedClientPath = File.ReadAllText(pointerFile)?.Trim();
                }

                // Step 2: Try to load config.ini if we know where to look
                if (!string.IsNullOrEmpty(rememberedClientPath))
                {

                    //string iniPath = Path.Combine(Path.GetDirectoryName(rememberedClientPath), "config.ini");
                    string iniPath = Path.Combine(rememberedClientPath, "config.ini");


                    if (File.Exists(iniPath))
                    {
                        data = parser.ReadFile(iniPath);

                        USERNAME = data["Credentials"]["Username"];
                        PASSWORD = data["Credentials"]["Password"];
                        SERVER_IP = data["Server"]["IP"];
                        SHARE_NAME = data["Server"]["Share"];
                        serverDbPath = data["Server"]["Path"];
                        clientDbPath = data["Client"]["Path"];
                        //   Client_Folder = data["folder"]["Path"];

                        // Load multiple folders
                        int index = 1;
                        while (true)
                        {
                            string key = $"Path{index}";
                            if (data["folder"].ContainsKey(key))
                            {
                                Client_Folders.Add(data["folder"][key]);
                                index++;
                            }
                            else break;
                        }

                        Console.WriteLine("Loaded saved configuration.");
                    }
                }

                // Step 3: If config not loaded, ask user and save it
                if (data == null)
                {
                    Console.WriteLine("No saved configuration found. Please enter details.");

                    GetClientsServerPath();       // sets clientDbPath

                    GetServerCredentials();      // sets USERNAME, PASSWORD
                    GetClientPathCredentials(); // sets Client_Folder   

                    data = new IniData();
                    data.Sections.AddSection("folder");


                    data["Credentials"]["Username"] = USERNAME;
                    data["Credentials"]["Password"] = PASSWORD;
                    data["Server"]["IP"] = SERVER_IP;
                    data["Server"]["Share"] = SHARE_NAME;
                    data["Server"]["Path"] = serverDbPath;
                    data["Client"]["Path"] = clientDbPath;

                    // data["folder"]["Path"] = Client_Folder;
                    for (int i = 0; i < Client_Folders.Count; i++)
                    {
                        data["folder"][$"Path{i + 1}"] = Client_Folders[i];
                    }

                    string basePath = File.Exists(clientDbPath)
                     ? Path.GetDirectoryName(clientDbPath)
                     : clientDbPath;

                    Console.WriteLine(basePath);
                    string iniPath = Path.Combine(basePath, "config.ini");
                    Console.Write(iniPath);

                    parser.WriteFile(iniPath, data);
              
                }

                Console.WriteLine("Ready to sync using loaded configuration.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(" ERROR: " + ex.Message);
                Console.ResetColor();
            }


            bool isNewClientDb = false;

            if (!HasMdbExtension(clientDbPath))
            {  // If clientDbPath does not have .mdb or .crm extension saving to know client root folder for future references
                clientFileFolder = clientDbPath;
                File.WriteAllText(pointerFile, clientFileFolder);

                if (File.Exists(Path.Combine(clientDbPath, Path.GetFileName(serverDbPath))))
                {
                    clientDbPath = Path.Combine(clientDbPath, Path.GetFileName(serverDbPath));
                }

                else
                {
                    while (true)
                    {
                        //string destFolder = clientDbPath; // Initialize with current clientDbPath

                        // If directory doesn't exist, get new input and restart the check immediately
                        if (!Directory.Exists(clientDbPath))
                        {
                            PrintError($"ERROR: Destination folder does not exist: {clientDbPath}");
                            Console.Write("Enter a valid destination folder: ");
                            clientDbPath = Console.ReadLine();
                            continue; // Restart loop to check the new input
                        }

                        // Proceed with network operations if directory exists
                        clientDbPath = Path.Combine(clientDbPath, Path.GetFileName(serverDbPath));
                        RunCommand($"net use {DRIVE_LETTER} /delete", false);

                        // PrintInfo("Mounting shared folder...");
                        var connectCmd = $"net use {DRIVE_LETTER} \\\\{SERVER_IP}\\{SHARE_NAME} /user:{USERNAME} {PASSWORD} /persistent:no";

                        if (!RunCommand(connectCmd))
                        {
                            PrintError("ERROR: Failed to connect to shared folder.");
                            GetServerCredentials();
                            GetClientsServerPath();
                            continue;
                        }

                         string serverFilePath = Path.Combine(DRIVE_LETTER, serverDbPath);
                       

                        if (!File.Exists(serverFilePath))
                        {
                            PrintError($"ERROR: File does not exist on server: {Path.GetFileName(serverDbPath)}");
                            RunCommand($"net use {DRIVE_LETTER} /delete", false);
                            Console.Write("Enter a valid server DB path: ");
                            serverDbPath = Console.ReadLine();
                            continue;
                        }

                        PrintInfo("Copying file from server...");

                        try
                        {
                            File.Copy(serverFilePath, clientDbPath, true);
                            PrintSuccess($"File successfully copied to: {clientDbPath}");
                            isNewClientDb = true;

                            // Clear any buffered key presses
                            while (Console.KeyAvailable)
                                Console.ReadKey(true);

                            // User input handling
                            PrintInfo("Synchronization is complete. (S) to Start Sync or (Q) to quit");

                            while (true)
                            {
                                if (Console.KeyAvailable)
                                {
                                    var key = Console.ReadKey(true).Key;

                                    if (key == ConsoleKey.Q)
                                    {
                                        _syncRunning = false;
                                        PrintWarning("Stopping synchronization...");
                                        Environment.Exit(0);
                                        break;
                                    }
                                    else if (key == ConsoleKey.S)
                                    {
                                        _syncRunning = true;
                                        PrintWarning("Starting synchronization...");
                                        break;
                                    }
                                }
                                await Task.Delay(100);
                            }

                            break; // Exit loop on success
                        }
                        catch (IOException ioEx)
                        {
                            PrintError($"File access error: {ioEx.Message}");
                            // Consider adding retry logic here with a delay
                        }
                        catch (UnauthorizedAccessException authEx)
                        {
                            PrintError($"Permission denied: {authEx.Message}");
                        }
                        catch (Exception ex)
                        {
                            PrintError($"ERROR: File copy failed. {ex.Message}");
                        }
                        RunCommand($"net use {DRIVE_LETTER} /delete", false);
                    }
                }
            }
            else
            {
                clientFileFolder = Path.GetDirectoryName(clientDbPath);
                clientFileFolder = clientDbPath;
                File.WriteAllText(pointerFile, clientFileFolder);

            }


            SyncMetadata metadata = null;

            ShowGameStyleLoader("Verifying database files", 10);
            Console.WriteLine();

            if (!VerifyDatabaseFiles(clientDbPath, serverDbPath))
            {
                PrintError("\nPress any key to exit...");
                Console.ReadKey();
                return;
            }
            string serverConnStr = $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={serverDbPath};";
            string clientConnStr = $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={clientDbPath};";

            using var clientConn = new OleDbConnection($"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={clientDbPath};");
            using var serverConn = new OleDbConnection($"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={serverDbPath};");
            clientConn.Open();
            serverConn.Open();

            ShowGameStyleLoader("Testing database connections", 20);
            if (!TestConnection("Client DB", clientConn) || !TestConnection("Server DB", serverConn))
            {
                PrintError("\nPress any key to exit...");
                Console.ReadKey();
                return;
            }


            // UpdateNullServerzeit(serverConn, clientConn);
            metadata = LoadSyncMetadata(syncMetaFile) ?? new SyncMetadata();
            InitializeMetadata(metadata, serverConn, clientConn, isNewClientDb);

            PrintSuccess("\nStarting optimized synchronization...");
            PrintInfo("Press 'S' to stop, 'R' to restart, 'Q' to quit.\n");

            var syncTask = Task.Run(() => ContinuousSync(serverConn, clientConn, syncMetaFile, metadata));
            var fileSyncTask = Task.Run(() => PeriodicFileSyncRunner(clientDbPath, serverDbPath, 1)); // every 5 minutes

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;

                    if (key == ConsoleKey.Q)
                    {
                        if (_syncRunning)
                        {
                            _syncRunning = false;
                            PrintWarning("Stopping synchronization...");
                            await syncTask;
                            await fileSyncTask;
                        }
                        break;
                    }
                    else if (key == ConsoleKey.S)
                    {
                        if (_syncRunning)
                        {
                            _syncRunning = false;
                            PrintWarning("Stopping synchronization...");
                            await syncTask;
                            await fileSyncTask;

                            PrintInfo("Synchronization stopped. Press 'R' to restart or 'Q' to quit.");
                        }
                    }
                    else if (key == ConsoleKey.R)
                    {
                        if (_syncRunning)
                        {
                            _syncRunning = false;
                            PrintWarning("Restarting synchronization...");
                            await syncTask;
                            await fileSyncTask;

                        }
                        else
                        {
                            PrintWarning("Starting synchronization...");
                        }
                        
                        _syncRunning = true;
                        syncTask = Task.Run(() => ContinuousSync(serverConn, clientConn, syncMetaFile, metadata));
                        fileSyncTask = Task.Run(() => PeriodicFileSyncRunner(clientDbPath, serverDbPath, 1));
                    }
                }

                await Task.Delay(100);
            }

            PrintInfo("\nExited. Press any key to close.");
            Console.CursorVisible = true;
            Console.ReadKey();
        }


        static string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);

                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, (password.Length - 1));
                    Console.Write("\b \b");
                }
            } while (key.Key != ConsoleKey.Enter);

            Console.WriteLine();
            return password;
        }

        static void UpdateNullServerzeitForTable(OleDbConnection connection, string tableName)
        {
            DateTime startTime = DateTime.Now;
            string query = $@"UPDATE [{tableName}] SET Serverzeit = ? WHERE Serverzeit IS NULL";

            using (var command = new OleDbCommand(query, connection))
            {
                DateTime nowUtc = SafeTimestamp(DateTime.Now);

                command.Parameters.AddWithValue("?", nowUtc);
                try
                {
                    command.ExecuteNonQuery();
                }

                catch (Exception ex)
                {
                    // Optionally log or handle the error
                    // Console.WriteLine($"Error updating table '{tableName}' in database: {ex.Message}");
                }
                DateTime endTime = DateTime.Now;

                TimeSpan elapsed = endTime - startTime;

                //string logEntry = $"Table: {tableName}, Elapsed Time: {elapsed.TotalSeconds:F2} seconds{Environment.NewLine}";
                //File.AppendAllText(logPath, logEntry);
            }
        }

        static void SyncTableStructure(OleDbConnection sourceConnStr, OleDbConnection targetConn, string tableName, SyncMetadata metadata)
        {
            try
            {
                //using var targetConn = new OleDbConnection(targetConnStr);
                //targetConn.Open();

                // Skip if table already exists in target
                if (TableExists(targetConn, tableName)) return;

                //using var sourceConn = new OleDbConnection(sourceConnStr);
                //sourceConn.Open();

                // Get table schema from source
                DataTable schema = sourceConnStr.GetOleDbSchemaTable(OleDbSchemaGuid.Tables,
                    new object[] { null, null, tableName, "TABLE" });

                if (schema.Rows.Count == 0) return;

                // Get columns information
                DataTable columns = sourceConnStr.GetOleDbSchemaTable(OleDbSchemaGuid.Columns,
                    new object[] { null, null, tableName, null });

                // Get primary key information
                DataTable primaryKeys = sourceConnStr.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys,
                    new object[] { null, null, tableName });

                // Build CREATE TABLE statement
                var createTableSql = new StringBuilder($"CREATE TABLE [{tableName}] (");

                foreach (DataRow column in columns.Rows)
                {
                    string columnName = column["COLUMN_NAME"].ToString();
                    string dataType = GetSqlDataType(column);
                    bool isPrimaryKey = primaryKeys.Select($"COLUMN_NAME = '{columnName}'").Length > 0;

                    createTableSql.Append($"[{columnName}] {dataType}");

                    if (isPrimaryKey)
                        createTableSql.Append(" PRIMARY KEY");

                    createTableSql.Append(", ");
                }

                // Add Serverzeit if not exists
                if (columns.Select("COLUMN_NAME = 'Serverzeit'").Length == 0)
                {
                    createTableSql.Append("[Serverzeit] DATETIME DEFAULT Now(), ");
                }

                // Remove trailing comma and close
                createTableSql.Length -= 2;
                createTableSql.Append(")");

                // Execute creation
                ExecuteNonQuery(targetConn, createTableSql.ToString());
                Console.WriteLine($"Created table {tableName} in target database");

                metadata.TableLastSync[tableName] = new SyncTimestamps
                {
                    ServerToClient = DateTime.MinValue,
                    ClientToServer = DateTime.MinValue
                };

                SaveSyncMetadata(syncMetaFile, metadata);
                PrintInfo($"[New Table] Initialized sync time for '{tableName}' to {DateTime.MinValue:yyyy-MM-dd HH:mm:ss}");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error synchronizing table {tableName}: {ex.Message}");
            }
        }
        static void ExecuteNonQuery(OleDbConnection conn, string sql, params (string, object)[] parameters)
        {
            using var cmd = new OleDbCommand(sql, conn);
            foreach (var (name, value) in parameters)
            {
                cmd.Parameters.AddWithValue(name, value);
            }
            cmd.ExecuteNonQuery();
        }
        static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nDatabase Synchronization Tool");
            Console.WriteLine("-----------------------------");
            Console.ResetColor();
        }

        static void PrintInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{DateTime.Now:T}] {message}");
            Console.ResetColor();
        }

        static void PrintSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{DateTime.Now:T}] {message}");
            Console.ResetColor();
        }

        static void PrintWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{DateTime.Now:T}] {message}");
            Console.ResetColor();
        }

        static void PrintError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now:T}] {message}");
            Console.ResetColor();
        }

        static void InitializeMetadata(SyncMetadata metadata, OleDbConnection clientConn, OleDbConnection serverConn, bool isNewClientDb)
        {
            var allTables = GetAllTableNames(clientConn)
                .Union(GetAllTableNames(serverConn))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var table in allTables)
            {
                foreach (var db in new[] { (conn: clientConn, label: "client"), (conn: serverConn, label: "server") })
                {
                    try
                    {
                        bool hasServerzeit = ColumnExists(db.conn, table, "Serverzeit");
                        if (!hasServerzeit)
                        {
                            string alterSql = $"ALTER TABLE [{table}] ADD COLUMN [Serverzeit] DATETIME DEFAULT Now()";
                            ExecuteNonQuery(db.conn, alterSql);

                            DateTime utcNow = SafeTimestamp(DateTime.UtcNow);
                            string updateSql = $"UPDATE [{table}] SET Serverzeit = ?";
                            using var updateCmd = new OleDbCommand(updateSql, db.conn);
                            updateCmd.Parameters.AddWithValue("?", utcNow);
                            updateCmd.ExecuteNonQuery();

                            if (!metadata.TableLastSync.ContainsKey(table))
                            {
                                metadata.TableLastSync[table] = new SyncTimestamps
                                {
                                    ServerToClient = utcNow,
                                    ClientToServer = utcNow,
                                };
                                SaveSyncMetadata(syncMetaFile, metadata);
                            }

                            continue;
                        }

                        
                        if (!metadata.TableLastSync.ContainsKey(table))
                        {
                            metadata.TableLastSync[table] = new SyncTimestamps
                            {
                                ServerToClient = DateTime.MinValue,
                                ClientToServer = DateTime.MinValue
                            };
                        }

                        using var cmd = new OleDbCommand($"SELECT MAX(Serverzeit) FROM [{table}]", db.conn);
                        var result = cmd.ExecuteScalar();
                        DateTime syncTime = (result != DBNull.Value && result != null)
                            ? ((DateTime)result).ToUniversalTime()
                            : DateTime.MinValue;

                        if (db.label == "server")
                        {
                            metadata.TableLastSync[table].ServerToClient = syncTime;
                            // PrintInfo($"[Server] Initialized table '{table}' with Serverzeit: {syncTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
                        }
                        else if (db.label == "client")
                        {
                            metadata.TableLastSync[table].ClientToServer = syncTime;
                            // PrintInfo($"[Client] Initialized table '{table}' with Serverzeit: {syncTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
                        }

                        SaveSyncMetadata(syncMetaFile, metadata);

                    }
                    catch (Exception ex)
                    {
                        if (!metadata.TableLastSync.ContainsKey(table))
                        {
                            metadata.TableLastSync[table] = new SyncTimestamps
                            {
                                ServerToClient = DateTime.MinValue,
                                ClientToServer = DateTime.MinValue
                            };
                            SaveSyncMetadata(syncMetaFile, metadata);
                        }
                    }
                }
            }

            foreach (var table in allTables)
            {
                SyncTableStructure(clientConn, serverConn, table, metadata);
                SyncTableStructure(serverConn, clientConn, table, metadata);
            }
        }

        static bool ColumnExists(OleDbConnection conn, string tableName, string columnName)
        {
            try
            {
                DataTable columns = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Columns,
                    new object[] { null, null, tableName, null });

                return columns.Rows.Cast<DataRow>()
                    .Any(row => row["COLUMN_NAME"].ToString().Equals(columnName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
        static bool RunCommand(string command, bool showOutput = true)
        {
            try
            {
                ProcessStartInfo procInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    RedirectStandardOutput = !showOutput,
                    RedirectStandardError = !showOutput,
                    UseShellExecute = false,
                    CreateNoWindow = !showOutput
                };

                using (Process proc = Process.Start(procInfo))
                {
                    proc.WaitForExit();
                    return proc.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                PrintError("Command execution failed: " + ex.Message);
                return false;
            }
        }

        public static bool HasMdbExtension(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string extension = Path.GetExtension(path);
            return extension.Equals(".mdb", StringComparison.OrdinalIgnoreCase) || extension.Equals(".crm", StringComparison.OrdinalIgnoreCase);
        }

        static async Task<bool> PullDatabaseFromServer(string serverPath, string clientPath)
        {
            try
            {
                var serverParts = serverPath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                if (serverParts.Length < 2)
                {
                    PrintError("Invalid server path format. Expected format: \\\\server\\share\\path\\file.mdb");
                    return false;
                }

                string serverIP = serverParts[0];
                string shareName = serverParts[1];
                string serverFilePath = string.Join("\\", serverParts.Skip(2));
                string fileName = Path.GetFileName(serverPath);

                if (!PingHost("127.0.0.1") || !PingHost(serverIP))
                {
                    PrintError("ERROR: Network connectivity issues");
                    return false;
                }

                bool isClientPathDirectory = Directory.Exists(clientPath) ||
                                           (clientPath.EndsWith("\\") ||
                                            clientPath.EndsWith("/"));

                string finalClientPath;
                if (isClientPathDirectory)
                {
                    Directory.CreateDirectory(clientPath);
                    finalClientPath = Path.Combine(clientPath, fileName);
                }
                else
                {
                    string directory = Path.GetDirectoryName(clientPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    finalClientPath = clientPath;
                }

                RunCommand($"net use {DRIVE_LETTER} /delete", false);

                PrintInfo("Mounting server share...");
                string connectCmd = $"net use {DRIVE_LETTER} \\\\{serverIP}\\{shareName} /user:{USERNAME} {PASSWORD} /persistent:no";
                if (!RunCommand(connectCmd))
                {
                    PrintError("ERROR: Failed to connect to shared folder");
                    return false;
                }

                string serverFile = $"{DRIVE_LETTER}\\{serverFilePath}";
                if (!File.Exists(serverFile))
                {
                    PrintError($"ERROR: File does not exist on server: {serverFilePath}");
                    RunCommand($"net use {DRIVE_LETTER} /delete", false);
                    return false;
                }

                PrintInfo($"Copying file from server to {finalClientPath}...");
                try
                {
                    File.Copy(serverFile, finalClientPath, true);
                    PrintSuccess("File successfully copied");
                    return true;
                }

                catch (Exception ex)
                {
                    PrintError($"ERROR: File copy failed: {ex.Message}");
                    return false;
                }
                finally
                {
                    RunCommand($"net use {DRIVE_LETTER} /delete", false);
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error pulling database from server: {ex.Message}");
                return false;
            }
        }

        static bool CheckNetworkConnection(string ip)
        {
            try
            {
                using (var tcpClient = new TcpClient())
                {
                    var result = tcpClient.BeginConnect(ip, 445, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(2000);
                    if (tcpClient.Connected) tcpClient.EndConnect(result);
                    return success;
                }
            }
            catch
            {
                return false;
            }
        }
   

        static void SyncFiles(string sourceFolder, string targetFolder, string logFile, string direction,List<string>? Helper,bool isFUllSErvertoCLient)
        {
            
            if (!Directory.Exists(sourceFolder))
                return;

            string today = DateTime.Today.ToString("yyyy-MM-dd");
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logFile);

            // Get ALL files (no extension filter)
            var sourceFiles = Directory
                .EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".ldb", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            // Create ALL directories, including empty ones
            var sourceDirs = Directory.EnumerateDirectories(sourceFolder, "*", SearchOption.AllDirectories);
            foreach (var dir in sourceDirs)
            {
                string relativePath = Path.GetRelativePath(sourceFolder, dir);
                string targetDir = Path.Combine(targetFolder, relativePath);
                Directory.CreateDirectory(targetDir);  // Ensures empty folders are created
            }

            var iniLines = File.Exists(logPath) ? File.ReadAllLines(logPath).ToList() : new List<string>();
            if (!iniLines.Contains($"[{today}]"))
                iniLines.Add($"[{today}]");

            int fileCount = iniLines.Count(line => line.StartsWith("file") && line.Contains("="));

            foreach (var src in sourceFiles)
            {
                string relativePath = Path.GetRelativePath(sourceFolder, src);

                if (isFUllSErvertoCLient)
                {
                    string topLevel = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                    if (Helper.Contains(topLevel, StringComparer.OrdinalIgnoreCase))
                        continue; // Skip directory and all its subdirs
                }
                string dest = Path.Combine(targetFolder, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);  // Ensure directory exists

                bool shouldCopy = !File.Exists(dest) || File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(dest);
                if (shouldCopy)
                {
                    File.Copy(src, dest, true);
                    PrintSuccess($" {sourceFolder} [✓] Copied: {relativePath} {direction}  {targetFolder}");
                    fileCount++;
                    iniLines.Add($"file{fileCount}={relativePath}");
                    iniLines.Add($"file{fileCount}.direction={direction}");
                }
            }

            if (fileCount > 0)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                var logLines = new List<string>
        {
            $"[{timestamp}]",
            $"Sync = Files",
            $"changes = {fileCount}",
            $"direction = {direction}",
            ""
        };
                File.AppendAllLines(logPath, logLines);
            }

        }

        static void SyncFilesBothDirections(string mainclient, string serverDbPath)
        {
            try
            {
                if (_syncRunning)
                {
                    string logFilePath = Path.Combine(Path.GetDirectoryName(mainclient)!, "Configlog.ini");
                    bool isServerMdb = HasMdbExtension(serverDbPath);
                    string serverFolder = Path.GetDirectoryName(serverDbPath)!;
                    string clientFolder1 = Path.GetDirectoryName(mainclient)!;
                    var HelperList = new List<string>();

                    foreach (var clientFolder in Client_Folders)
                    {
                        if (!Directory.Exists(clientFolder)) continue;

                        string clientFolderName = Path.GetFileName(clientFolder);
                        HelperList.Add(clientFolderName);

                        string correspondingServerFolder = Path.Combine(serverFolder, clientFolderName);

                        // Client → Server Sync
                        if (!Directory.Exists(correspondingServerFolder))
                        {
                            Directory.CreateDirectory(correspondingServerFolder);
                            PrintSuccess($"[+] Created folder on server: {correspondingServerFolder}");
                        }

                        SyncFiles(clientFolder, correspondingServerFolder, logFilePath, "ClientToServer", HelperList, false);
                        SyncFiles(correspondingServerFolder, clientFolder, logFilePath, "ServerToClient", HelperList, false);
                    }

                    // Final server-to-main-client sync
                    SyncFiles(serverFolder, clientFolder1, logFilePath, "ServerToClient", HelperList, true);
                }
            }
            catch (Exception ex)
            {
                PrintError($"[!] Error during sync: {ex.Message}");

            }
        }
        static async Task PeriodicFileSyncRunner(string clientPath, string serverPath, int intervalMinutes)
        {
            while (_syncRunning)
            {
                try
                {
                    PrintInfo("Starting periodic file sync...");
                    SyncFilesBothDirections(clientPath, serverPath);
                    PrintSuccess("File sync completed.");
                }
                catch (Exception ex)
                {
                    PrintError($"[File Sync] Error: {ex.Message}");
                }

                DateTime nextFileSync = DateTime.Now.AddMinutes(intervalMinutes);
              //  Console.WriteLine();
                while (DateTime.Now < nextFileSync && _syncRunning)
                {
                    TimeSpan remaining = nextFileSync - DateTime.Now;
                   // Console.Write($"  Next file sync in {remaining.Minutes:D2}:{remaining.Seconds:D2}   ");
                    await Task.Delay(1000);
                }

                Console.WriteLine(); // Clean line after countdown
            }
        }



        static async Task ContinuousSync(
OleDbConnection serverConnStr, OleDbConnection clientConnStr,
string syncMetaFile,
SyncMetadata metadata)
        {
            
            while (_syncRunning)
            {
                try
                {
                    var clientTables = GetAllTableNames(clientConnStr);
                    var serverTables = GetAllTableNames(serverConnStr);
                    var allTables = clientTables.Union(serverTables).ToList();
                    if (!_syncRunning) break;
                    _cycleTimer.Restart();
                    PrintInfo($"Starting sync cycle at {DateTime.Now:T}");
                    _isOnline = CheckNetworkConnection(SERVER_IP);
                    if (_isOnline)
                    {
                        if (_lastOnlineTime == DateTime.MinValue)
                        {
                            PrintSuccess("Connection restored");
                        }
                        _lastOnlineTime = DateTime.Now;
                       // var fileSyncTask = Task.Run(() => SyncFilesBothDirections(clientDbPath, serverDbPath));
                        //  List<DateTime> lastSyncTimes1 = metadata.TableLastSync.Values.ToList();
                        int totalChanges = 0;
                        foreach (var tableName in allTables)
                        {
                            if (!_syncRunning) break;
                            try
                            {
                             
                                UpdateNullServerzeitForTable(serverConnStr, tableName);
                                UpdateNullServerzeitForTable(clientConnStr, tableName);
                               
                                DateTime lastSync = metadata.TableLastSync.TryGetValue(tableName, out var syncData)
                                 ? syncData.ServerToClient
                                 : DateTime.MinValue;
                                DateTime LasSync = metadata.TableLastSync.TryGetValue(tableName, out var syncData1)
                                    ? syncData1.ClientToServer
                                    : DateTime.MinValue;
                                DateTime Minimum = metadata.TableLastSync.TryGetValue(tableName, out var syncData2)
                                    ? syncData2.ServerToClient < syncData2.ClientToServer
                                        ? syncData2.ServerToClient
                                        : syncData2.ClientToServer
                                    : DateTime.MinValue;

                                var lastSyncTime = Minimum.ToLocalTime();

                                PrintInfo($"Syncing {tableName} since {lastSync.ToLocalTime():yyyy-MM-dd HH:mm:ss}");

                                var skipList = new HashSet<object>();
                                int serverToClient = await SyncDirection(
                                   serverConnStr,
                                    clientConnStr,
                                    tableName: tableName,
                                    lastSync: Minimum,
                                    isServerToClient: true,
                                    metadata: metadata,
                                    skipList
                                );

                                // Sync Server → Client

                                if (serverToClient > 0)
                                {
                                    PrintSuccess($"{tableName} sync: Server → Client: {serverToClient}");
                                    totalChanges += serverToClient;

                                    string logFilePath = Path.Combine(Path.GetDirectoryName(clientDbPath), "Configlog.ini");
                                    WriteChangesToIni(tableName, serverToClient, "ServerToClient", logFilePath);
                                }

                                // Sync Client → Server
                                int clientToServer = await SyncDirection(
                                        clientConnStr,
                                        serverConnStr,
                                        tableName: tableName,
                                        lastSync: Minimum,
                                        isServerToClient: false,
                                        metadata: metadata,
                                        skipList);

                                if (clientToServer > 0)
                                {
                                    PrintSuccess($"{tableName} sync: Client → Server: {clientToServer}");
                                    totalChanges += clientToServer;

                                    string logFilePath = Path.Combine(Path.GetDirectoryName(clientDbPath), "Configlog.ini");
                                    WriteChangesToIni(tableName, clientToServer, "ClientToServer", logFilePath);
                                }


                          
                                if (serverToClient == 0 && clientToServer == 0)
                                {
                                    PrintInfo($"No changes for {tableName}");
                                }

                                if (metadata.TableLastSync.TryGetValue(tableName, out var updatedSyncTime))
                                {
                                    //PrintInfo($"Updated sync time for {tableName}: {updatedSyncTime:yyyy-MM-dd HH:mm:ss}");
                                }
                            }
                            catch (Exception ex)
                            {
                                // PrintError($"Error syncing table {tableName}: {ex.Message}");
                            }
                        }
                        //await fileSyncTask;
                        _cycleTimer.Stop();
                        PrintSuccess($"Sync cycle completed in {_cycleTimer.Elapsed.TotalSeconds:0.00} seconds. Total changes: {totalChanges}");

                        if (totalChanges == 0)
                        {
                            PrintInfo("No changes detected in this cycle");
                        }
                    }
                    else
                    {
                        if (_lastOnlineTime != DateTime.MinValue)
                        {
                            PrintWarning("Connection lost - entering offline mode");
                            _lastOnlineTime = DateTime.MinValue;
                        }

                    }
                    _nextSyncTime = DateTime.Now.AddMinutes(_syncCycleWaitMinutes);
                    PrintInfo($"Next sync at {_nextSyncTime:T}");

                    while (DateTime.Now < _nextSyncTime && _syncRunning)
                    {
                        TimeSpan remaining = _nextSyncTime - DateTime.Now;
                        Console.Write($"\rWaiting for next sync in {remaining.Minutes}:{remaining.Seconds:00}...");
                        await Task.Delay(1000);
                    }

                    Console.WriteLine();

                }
                catch (Exception ex)
                {
                    PrintError($"Sync cycle error: {ex.Message}");
                }
                finally
                {
                    SaveSyncMetadata(syncMetaFile, metadata);
                }
            }
        }
        static async Task<int> SyncDirection(
        OleDbConnection sourceConn,
            OleDbConnection targetConn,
            string tableName,
            DateTime lastSync,
            bool isServerToClient,
            SyncMetadata metadata, HashSet<object> skipList)
        {
            int changesApplied = 0;

            DateTime maxTimestamp = lastSync;

            try
            {
                if (isServerToClient)
                {
                    SyncTableStructure(sourceConn, targetConn, tableName, metadata);
                }
                if (!isServerToClient)
                {
                    SyncTableStructure(sourceConn, targetConn, tableName, metadata);

                }

                string pkColumn = GetPrimaryKeyColumn(sourceConn, tableName);
                if (string.IsNullOrEmpty(pkColumn))
                {
                    ExecuteNonQuery(sourceConn, $"ALTER TABLE [{tableName}] ADD COLUMN [DefaultSynCID] GUID");
                    ExecuteNonQuery(sourceConn, $"ALTER TABLE [{tableName}] ADD CONSTRAINT pk_{tableName}_DefaultSynCID PRIMARY KEY ([DefaultSynCID])");
                }

                if (isServerToClient)
                {
                    string query = $@"SELECT * FROM [{tableName}] 
                                                    WHERE Serverzeit > ?
                                                    ORDER BY Serverzeit DESC";  // Newest first
                    using (var cmd = new OleDbCommand(query, sourceConn))
                    {
                        DateTime cleanedLastSync = SafeTimestamp(lastSync.ToLocalTime()); // keep original timestamp, strip milliseconds
                        cmd.Parameters.AddWithValue("?", cleanedLastSync);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);

                                }
                                skipList.Add(row[pkColumn]);


                                if (ApplyChangeWithConflictResolution(
                                    targetConn,
                                    tableName,
                                    row,
                                    isServerToClient,
                                    pkColumn))
                                {
                                    changesApplied++;
                                    var rowTimestamp = Convert.ToDateTime(row["Serverzeit"]);
                                    if (rowTimestamp > maxTimestamp)
                                        maxTimestamp = rowTimestamp;
                                }


                            }

                        }
                    }
                }
                else
                {
                    string query = $@"SELECT * FROM [{tableName}] 
                                                    WHERE Serverzeit > ?
                                                    ORDER BY Serverzeit DESC";  // Newest first
                    using (var cmd = new OleDbCommand(query, sourceConn))
                    {
                        DateTime cleanedLastSync = SafeTimestamp(lastSync.ToLocalTime()); // keep original timestamp, strip milliseconds
                        cmd.Parameters.AddWithValue("?", cleanedLastSync);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {

                                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);

                                }
                                if (skipList.Contains(row[pkColumn]))
                                {
                                    continue;
                                }
                                if (ApplyChangeWithConflictResolution(
                                targetConn,
                                tableName,
                                row,
                                isServerToClient,
                                pkColumn))
                                {
                                    changesApplied++;
                                    var rowTimestamp = Convert.ToDateTime(row["Serverzeit"]);
                                    if (rowTimestamp > maxTimestamp)
                                        maxTimestamp = rowTimestamp;
                                }


                            }
                        }
                    }

                }

            }
            catch (Exception ex)
            {
                //PrintError($"Error syncing {tableName}: {ex.Message}");
            }

            if (!metadata.TableLastSync.ContainsKey(tableName))
                metadata.TableLastSync[tableName] = new SyncTimestamps();

            if (isServerToClient)
            {
                if (changesApplied > 0)
                {

                    maxTimestamp = maxTimestamp.AddSeconds(1).ToUniversalTime();
                    metadata.TableLastSync[tableName].ServerToClient = maxTimestamp;
                    metadata.TableLastSync[tableName].ClientToServer = maxTimestamp;
                    SaveSyncMetadata(syncMetaFile, metadata);

                }

            }
            else
            {
                if (changesApplied > 0)
                {

                    maxTimestamp = maxTimestamp.AddSeconds(1).ToUniversalTime();

                    metadata.TableLastSync[tableName].ServerToClient = maxTimestamp;
                    metadata.TableLastSync[tableName].ClientToServer = maxTimestamp;
                    SaveSyncMetadata(syncMetaFile, metadata);
                }
            }



            return changesApplied;
        }


        static void WriteChangesToIni(string tableName, int changeCount, string direction, string filePath)
        {
            var sb = new StringBuilder();

            // Format: [2025-07-18 10:43]
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            sb.AppendLine($"[{timestamp}]");
            sb.AppendLine($"Sync = {tableName}");
            sb.AppendLine($"changes = {changeCount}");
            sb.AppendLine($"direction = {direction}");
            sb.AppendLine();

            File.AppendAllText(filePath, sb.ToString());
        }

        static DateTime SafeTimestamp(DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Kind);
        }

        static bool ApplyChangeWithConflictResolution(OleDbConnection targetConn,
                                            string tableName,
                                            Dictionary<string, object> row,
                                            bool isServerToClient,
                                            string pkColumn)
        {
            try
            {
                var pkValue = row[pkColumn]; // This is a GUID
                                             //  var incomingLastModified = Convert.ToDateTime(row["Serverzeit"]);

                bool exists = RecordExists(targetConn, tableName, pkColumn, pkValue);
                if (!exists)
                    return InsertRecord(targetConn, tableName, row);

                // Simple conflict resolution - server wins
                if (isServerToClient)
                {

                    return UpdateRecord(targetConn, tableName, row, pkColumn);
                }
                else
                {
                    return UpdateRecord(targetConn, tableName, row, pkColumn);

                }
            }
            catch (Exception ex)
            {

                return false;
            }
        }
        static string GetSqlDataType(DataRow column)
        {
            int oleDbType = (int)column["DATA_TYPE"];
            int size = column["CHARACTER_MAXIMUM_LENGTH"] is DBNull ? 0 : Convert.ToInt32(column["CHARACTER_MAXIMUM_LENGTH"]);
            switch (oleDbType)
            {
                case 130: return size > 0 ? $"TEXT({size})" : "TEXT(255)";
                case 3: return "INTEGER";
                case 5: return "DOUBLE";
                case 7: return "DATETIME";
                case 11: return "BIT";
                case 72: return "GUID";
                case 203: return "MEMO";
                default: return "TEXT(255)";
            }
        }
        static SyncMetadata LoadSyncMetadata(string path)
        {
            if (!File.Exists(path)) return new SyncMetadata();
            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<SyncMetadata>(json);
            }
            catch (Exception ex)
            {
                PrintError($"Error loading metadata: {ex.Message}");
                return new SyncMetadata();
            }
        }
        static void SaveSyncMetadata(string path, SyncMetadata metadata)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(metadata, options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                PrintError($"Error saving metadata: {ex.Message}");
            }
        }

        static bool PingHost(string nameOrAddress)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(nameOrAddress, 2000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        static void ShowGameStyleLoader(string message, int totalSteps)
        {
            Console.Write(message + " ");
            int progressBarWidth = 30;

            for (int i = 0; i <= totalSteps; i++)
            {
                double percentage = (double)i / totalSteps;
                int filledBars = (int)(percentage * progressBarWidth);
                string bar = new string('█', filledBars).PadRight(progressBarWidth, '-');

                Console.Write($"\r{message} [{bar}] {percentage * 100:0}%");
                Thread.Sleep(20);
            }
            Console.WriteLine();
        }

        static bool VerifyDatabaseFiles(string clientPath, string serverPath)
        {
            if (!File.Exists(clientPath))
            {
                PrintError($"\nClient database not found at: {clientPath}");
                return false;
            }

            if (!File.Exists(serverPath))
            {
                PrintError($"\nServer database not found at: {serverPath}");
                return false;
            }

            return true;
        }

        static bool TestConnection(string name, OleDbConnection connectionString)
        {
            try
            {

                PrintSuccess($"{name} connection successful");
                return true;
            }
            catch (Exception ex)
            {
                PrintError($"\n{name} connection failed: {ex.Message}");
                return false;
            }
        }

        static List<string> GetAllTableNames(OleDbConnection conn)
        {
            var tables = new List<string>();
            try
            {
                DataTable schemaTables = conn.GetSchema("Tables");

                foreach (DataRow row in schemaTables.Rows)
                {
                    string tableName = row["TABLE_NAME"].ToString();
                    string tableType = row["TABLE_TYPE"].ToString();

                    if (tableType == "TABLE" && !tableName.StartsWith("MSys")
                        && !tableName.StartsWith("~TMP") && !tableName.StartsWith("_"))
                    {
                        tables.Add(tableName);
                    }
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error getting table names: {ex.Message}");
            }
            return tables;
        }
        static string GetPrimaryKeyColumn(OleDbConnection conn, string tableName)
        {
            try
            {
                DataTable schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys,
                    new object[] { null, null, tableName });

                if (schema.Rows.Count > 0)
                {
                    return schema.Rows[0]["COLUMN_NAME"].ToString();
                }

                DataTable columns = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Columns,
                    new object[] { null, null, tableName, null });

                foreach (DataRow row in columns.Rows)
                {
                    string col = row["COLUMN_NAME"].ToString();

                    if (col.Equals("ID", StringComparison.OrdinalIgnoreCase) ||
                        col.Equals("GUID", StringComparison.OrdinalIgnoreCase))
                    {
                        return col;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        static bool TableExists(OleDbConnection conn, string tableName)
        {
            try
            {
                DataTable schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables,
                    new object[] { null, null, tableName, "TABLE" });
                    return schema.Rows.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        static bool RecordExists(OleDbConnection conn, string tableName, string pkColumn, object pkValue)
        {
            try
            {
                string query = $"SELECT COUNT(*) FROM [{tableName}] WHERE [{pkColumn}] = ?";
                using var cmd = new OleDbCommand(query, conn);
                cmd.Parameters.AddWithValue($"@{pkColumn}", pkValue);
                return (int)cmd.ExecuteScalar() > 0;
            }
            catch
            {
                return false;
            }
        }
        static bool InsertRecord(OleDbConnection conn, string tableName, Dictionary<string, object> row)
        {
            try
            {
                var columns = row.Keys.ToList();
                var columnList = string.Join(", ", columns.Select(c => $"[{c}]"));
                var valuePlaceholders = string.Join(", ", columns.Select(_ => "?"));
                string insertQuery = $@"INSERT INTO [{tableName}] ({columnList}) VALUES ({valuePlaceholders})";
                using var cmd = new OleDbCommand(insertQuery, conn);
                foreach (var col in columns)
                    cmd.Parameters.AddWithValue($"@{col}", row[col] ?? DBNull.Value);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                PrintError($"Error inserting record into {tableName}: {ex.Message}");
                return false;
            }
        }

        static bool UpdateRecord(OleDbConnection conn, string tableName, Dictionary<string, object> row, string pkColumn)
        {
            try
            {
                var columns = row.Keys.Where(k => k != pkColumn).ToList();
                var updateSet = string.Join(", ", columns.Select(c => $"[{c}] = ?"));
                string updateQuery = $@"UPDATE [{tableName}] SET {updateSet} WHERE [{pkColumn}] = ?";

                using var cmd = new OleDbCommand(updateQuery, conn);
                foreach (var col in columns)
                    cmd.Parameters.AddWithValue($"@{col}", row[col] ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"@{pkColumn}", row[pkColumn]);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                PrintError($"Error updating record in {tableName}: {ex.Message}");
                return false;
            }
        }

      
    }
}