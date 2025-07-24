//using System.Data.OleDb;

//namespace RobustAccessDbSync
//{
//    class Program
//    {
//        private static bool _syncRunning = true;
//        private static DateTime _lastClientSyncTime;
//        private static DateTime _lastServerSyncTime;
//        private const string ConflictSuffix = "_CONFLICT_RESOLVED";

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static async Task Main()
//        {
//            string clientDbPath = @"C:\Users\DELL\Documents\mdb\yash.mdb";
//            string serverDbPath = @"\\192.168.1.93\mdbfile\rajat.mdb";
//            const string tableName = "People";
//            const string pkColumn = "ID";

//            if (!VerifyDatabaseFiles(clientDbPath, serverDbPath))
//            {
//                Console.ReadKey();
//                return;
//            }

//            string clientConnStr = $"Provider=Microsoft.ACE.OLEDB.16.0;Data Source={clientDbPath};";
//            string serverConnStr = $"Provider=Microsoft.ACE.OLEDB.16.0;Data Source={serverDbPath};";

//            if (!TestConnection("Client DB", clientConnStr) || !TestConnection("Server DB", serverConnStr))
//            {
//                Console.ReadKey();
//                return;
//            }

//            // Initialize last sync times
//            _lastClientSyncTime = GetMaxLastModified(serverConnStr, tableName);
//            _lastServerSyncTime = GetMaxLastModified(clientConnStr, tableName);

//            Console.WriteLine("\nStarting continuous synchronization...");
//            Console.WriteLine("Press 'Q' then Enter to stop synchronization.\n");

//            var syncTask = Task.Run(() => ContinuousSync(serverConnStr, clientConnStr, tableName, pkColumn));

//            while (_syncRunning)
//            {
//                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
//                {
//                    _syncRunning = false;
//                    Console.WriteLine("Stopping synchronization...");
//                }
//                await Task.Delay(100);
//            }

//            await syncTask;
//            Console.WriteLine("Synchronization stopped. Press any key to exit.");
//            Console.ReadKey();
//        }

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static bool VerifyDatabaseFiles(string clientPath, string serverPath)
//        {
//            if (!File.Exists(clientPath))
//            {
//                Console.WriteLine($"Client database not found at: {clientPath}");
//                return false;
//            }

//            if (!File.Exists(serverPath))
//            {
//                Console.WriteLine($"Server database not found at: {serverPath}");
//                return false;
//            }

//            return true;
//        }

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static bool TestConnection(string name, string connectionString)
//        {
//            Console.WriteLine($"Testing {name} connection...");
//            try
//            {
//                using var connection = new OleDbConnection(connectionString);
//                connection.Open();
//                Console.WriteLine($"{name} connection successful.");
//                return true;
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"{name} connection failed: {ex.Message}");
//                return false;
//            }
//        }

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static DateTime GetMaxLastModified(string connectionString, string tableName)
//        {
//            try
//            {
//                using var conn = new OleDbConnection(connectionString);
//                conn.Open();
//                using var cmd = new OleDbCommand($"SELECT MAX(LastModified) FROM [{tableName}]", conn);
//                var result = cmd.ExecuteScalar();
//                if (result != DBNull.Value && result != null)
//                    return Convert.ToDateTime(result);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error fetching max LastModified: {ex.Message}");
//            }
//            return DateTime.MinValue;
//        }

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static async Task ContinuousSync(string serverConnStr, string clientConnStr, string tableName, string pkColumn)
//        {
//            while (_syncRunning)
//            {
//                try
//                {
//                    using var serverConn = new OleDbConnection(serverConnStr);
//                    using var clientConn = new OleDbConnection(clientConnStr);
//                    serverConn.Open();
//                    clientConn.Open();

//                    // Server → Client sync (server wins, but preserve client data)
//                    Console.WriteLine($"[{DateTime.Now:T}] Syncing Server → Client...");
//                    int serverToClientChanges = SyncDirection(
//                        sourceConn: serverConn,
//                        targetConn: clientConn,
//                        tableName: tableName,
//                        lastSyncTime: ref _lastServerSyncTime,
//                        isServerToClient: true,
//                        pkColumn: pkColumn);
//                    Console.WriteLine($"[{DateTime.Now:T}] Server → Client: {serverToClientChanges} changes applied");

//                    // Client → Server sync (server wins, but preserve client data)
//                    Console.WriteLine($"[{DateTime.Now:T}] Syncing Client → Server...");
//                    int clientToServerChanges = SyncDirection(
//                        sourceConn: clientConn,
//                        targetConn: serverConn,
//                        tableName: tableName,
//                        lastSyncTime: ref _lastClientSyncTime,
//                        isServerToClient: false,
//                        pkColumn: pkColumn);
//                    Console.WriteLine($"[{DateTime.Now:T}] Client → Server: {clientToServerChanges} changes applied");

//                    // Handle deletions
//                    //SyncDeletions(serverConn, clientConn, tableName, pkColumn);
//                    //SyncDeletions(clientConn, serverConn, tableName, pkColumn);
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"[{DateTime.Now:T}] Sync error: {ex.Message}");
//                }

//                await Task.Delay(5000);
//            }
//        }

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static int SyncDirection(OleDbConnection sourceConn, OleDbConnection targetConn,
//                              string tableName, ref DateTime lastSyncTime,
//                              bool isServerToClient, string pkColumn)
//        {
//            int changesApplied = 0;
//            DateTime maxTimestamp = lastSyncTime;

//            string lastSyncFormatted = lastSyncTime.ToString("MM/dd/yyyy hh:mm:ss tt");
//            string getChangesQuery = $@"
//                SELECT * FROM [{tableName}]
//                WHERE LastModified > ?
//                ORDER BY LastModified";

//            using var cmd = new OleDbCommand(getChangesQuery, sourceConn);
//            cmd.Parameters.AddWithValue("@LastModified", lastSyncFormatted);

//            using var reader = cmd.ExecuteReader();
//            while (reader.Read())
//            {
//                var row = new Dictionary<string, object>();
//                for (int i = 0; i < reader.FieldCount; i++)
//                {
//                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
//                }

//                if (ApplyChangeWithConflictResolution(
//                    targetConn, tableName, row, isServerToClient, pkColumn))
//                {
//                    changesApplied++;
//                    var rowTimestamp = Convert.ToDateTime(row["LastModified"]);
//                    if (rowTimestamp > maxTimestamp)
//                        maxTimestamp = rowTimestamp;
//                }
//            }

//            lastSyncTime = maxTimestamp;
//            return changesApplied;
//        }

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static bool ApplyChangeWithConflictResolution(OleDbConnection targetConn,
//                                                    string tableName,
//                                                    Dictionary<string, object> row,
//                                                    bool isServerToClient,
//                                                    string pkColumn)
//        {
//            var pkValue = row[pkColumn];
//            var incomingLastModified = Convert.ToDateTime(row["LastModified"]);

//            // Check if record exists in target
//            bool exists = RecordExists(targetConn, tableName, pkColumn, pkValue);

//            if (!exists)
//            {
//                // Simple insert for new records
//                return InsertRecord(targetConn, tableName, row);
//            }

//            // Handle existing record
//            var targetLastModified = GetLastModified(targetConn, tableName, pkColumn, pkValue);

//            // If incoming record is older or same, skip
//            //if (incomingLastModified <= targetLastModified)
//            //return false;

//            // Get the target record before overwriting
//            var targetRecord = GetRecord(targetConn, tableName, pkColumn, pkValue);

//            // Server always wins in conflicts
//            if (isServerToClient)
//            {
//                // Preserve client data before overwriting
//                if (!IsConflictResolvedRecord(targetRecord))
//                {
//                    // Create a copy of the client data with new ID
//                    int newId = GetNextAvailableId(targetConn, tableName, pkColumn);
//                    targetRecord[pkColumn] = newId;
//                    targetRecord["Notes"] = AddConflictNote(targetRecord,
//                        $"Preserved client data (original ID: {pkValue}) before server overwrite");

//                    InsertRecord(targetConn, tableName, targetRecord);
//                    Console.WriteLine($"Preserved client data for ID {pkValue} as new ID {newId}");
//                }

//                // Now update with server data
//                return UpdateRecord(targetConn, tableName, row, pkColumn);
//            }
//            else
//            {
//                // For client→server sync, we still let server win (no overwrite)
//                // but preserve client data as a new record
//                if (!IsConflictResolvedRecord(row))
//                {
//                    int newId = GetNextAvailableId(targetConn, tableName, pkColumn);
//                    row[pkColumn] = newId;
//                    row["Notes"] = AddConflictNote(row,
//                        $"Preserved client data (original ID: {pkValue}) - server version was kept");

//                    Console.WriteLine($"Preserved client data for ID {pkValue} as new ID {newId}");
//                    return InsertRecord(targetConn, tableName, row);
//                }
//                return false;
//            }
//        }

//        static string AddConflictNote(Dictionary<string, object> record, string note)
//        {
//            if (record.ContainsKey("Notes") && record["Notes"] != null)
//            {
//                return record["Notes"].ToString() + "; " + note;
//            }
//            return note;
//        }

//        static bool IsConflictResolvedRecord(Dictionary<string, object> record)
//        {
//            return record.ContainsKey("Notes") &&
//                   record["Notes"] != null &&
//                   record["Notes"].ToString().Contains("CONFLICT_RESOLVED");
//        }

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static Dictionary<string, object> GetRecord(OleDbConnection conn, string tableName,
//                                                  string pkColumn, object pkValue)
//        {
//            var record = new Dictionary<string, object>();
//            string query = $"SELECT * FROM [{tableName}] WHERE [{pkColumn}] = ?";

//            using var cmd = new OleDbCommand(query, conn);
//            cmd.Parameters.AddWithValue($"@{pkColumn}", pkValue);

//            using var reader = cmd.ExecuteReader();
//            if (reader.Read())
//            {
//                for (int i = 0; i < reader.FieldCount; i++)
//                {
//                    record[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
//                }
//            }
//            return record;
//        }

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static bool UpdateRecord(OleDbConnection conn, string tableName,
//                               Dictionary<string, object> row, string pkColumn)
//        {
//            var columns = row.Keys.Where(k => k != pkColumn).ToList();
//            var updateSet = string.Join(", ", columns.Select(c => $"[{c}] = ?"));

//            string updateQuery = $@"
//                UPDATE [{tableName}]
//                SET {updateSet}
//                WHERE [{pkColumn}] = ?";

//            using var cmd = new OleDbCommand(updateQuery, conn);

//            foreach (var col in columns)
//                cmd.Parameters.AddWithValue($"@{col}", row[col] ?? DBNull.Value);

//            cmd.Parameters.AddWithValue($"@{pkColumn}", row[pkColumn]);

//            return cmd.ExecuteNonQuery() > 0;
//        }

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static bool InsertRecord(OleDbConnection conn, string tableName, Dictionary<string, object> row)
//        {
//            var columns = row.Keys.ToList();
//            var columnList = string.Join(", ", columns.Select(c => $"[{c}]"));
//            var valuePlaceholders = string.Join(", ", columns.Select(_ => "?"));

//            string insertQuery = $@"
//                INSERT INTO [{tableName}] ({columnList})
//                VALUES ({valuePlaceholders})";

//            using var cmd = new OleDbCommand(insertQuery, conn);

//            foreach (var col in columns)
//                cmd.Parameters.AddWithValue($"@{col}", row[col] ?? DBNull.Value);

//            return cmd.ExecuteNonQuery() > 0;
//        }

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static int GetNextAvailableId(OleDbConnection conn, string tableName, string pkColumn)
//        {
//            string query = $"SELECT MAX([{pkColumn}]) FROM [{tableName}]";
//            using var cmd = new OleDbCommand(query, conn);
//            var result = cmd.ExecuteScalar();
//            int maxId = (result != DBNull.Value && result != null) ? Convert.ToInt32(result) : 0;
//            return maxId + 1;
//        }


//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static bool RecordExists(OleDbConnection conn, string tableName, string pkColumn, object pkValue)
//        {
//            string query = $"SELECT COUNT(*) FROM [{tableName}] WHERE [{pkColumn}] = ?";
//            using var cmd = new OleDbCommand(query, conn);
//            cmd.Parameters.AddWithValue($"@{pkColumn}", pkValue);
//            return (int)cmd.ExecuteScalar() > 0;
//        }

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static DateTime GetLastModified(OleDbConnection conn, string tableName, string pkColumn, object pkValue)
//        {
//            string query = $"SELECT LastModified FROM [{tableName}] WHERE [{pkColumn}] = ?";
//            using var cmd = new OleDbCommand(query, conn);
//            cmd.Parameters.AddWithValue($"@{pkColumn}", pkValue);
//            var result = cmd.ExecuteScalar();
//            return (result != DBNull.Value && result != null) ? Convert.ToDateTime(result) : DateTime.MinValue;
//        }

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static void SyncDeletions(OleDbConnection sourceConn, OleDbConnection targetConn, string tableName, string pkColumn)
//        {
//            var sourceIds = GetAllIds(sourceConn, tableName, pkColumn);
//            var targetIds = GetAllIds(targetConn, tableName, pkColumn);

//            var toDeleteInTarget = targetIds.Except(sourceIds).ToList();


//            foreach (var id in toDeleteInTarget)
//            {
//                if (IsConflictPreservedRecord(targetConn, tableName, pkColumn, id))
//                {
//                    // Skip conflict-preserved rows
//                    continue;
//                }

//                string deleteQuery = $"DELETE FROM [{tableName}] WHERE [{pkColumn}] = ?";
//                using var cmd = new OleDbCommand(deleteQuery, targetConn);
//                cmd.Parameters.AddWithValue("?", id);
//                cmd.ExecuteNonQuery();
//                Console.WriteLine($"Deleted ID {id} from target.");
//            }

//        }
//        static bool IsConflictPreservedRecord(OleDbConnection conn, string tableName, string pkColumn, int id)
//        {
//            string query = $"SELECT Notes FROM [{tableName}] WHERE [{pkColumn}] = ?";
//            using var cmd = new OleDbCommand(query, conn);
//            cmd.Parameters.AddWithValue("?", id);
//            var result = cmd.ExecuteScalar();
//            return result != DBNull.Value && result != null && result.ToString().Contains("Preserved");
//        }


//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static List<int> GetAllIds(OleDbConnection conn, string tableName, string pkColumn)
//        {
//            var ids = new List<int>();
//            string query = $"SELECT [{pkColumn}] FROM [{tableName}]";

//            using var cmd = new OleDbCommand(query, conn);
//            using var reader = cmd.ExecuteReader();

//            while (reader.Read())
//            {
//                ids.Add(reader.GetInt32(0));
//            }

//            return ids;
//        }
//    }
//}






//using System;
//using System.Collections.Generic;
//using System.Data.OleDb;
//using System.IO;
//using System.Linq;
//using System.Text.Json;
//using System.Threading.Tasks;

//namespace RobustAccessDbSync
//{
//    class SyncMetadata
//    {
//        public DateTime LastClientSyncTime { get; set; }
//        public DateTime LastServerSyncTime { get; set; }
//    }

//    class Program
//    {
//        private static bool _syncRunning = true;
//        private static DateTime _lastClientSyncTime;
//        private static DateTime _lastServerSyncTime;
//        private const string ConflictSuffix = "_CONFLICT_RESOLVED";

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static async Task Main()
//        {
//            string clientDbPath = @"C:\Users\Nimap\Documents\mdbfile\rajat.mdb";
//            string serverDbPath = @"\\95.111.230.3\BatFolder\rajat.mdb";
//            const string tableName = "People";
//            const string pkColumn = "ID";
//            string syncMetaFile = "sync_metadata.json";

//            if (!VerifyDatabaseFiles(clientDbPath, serverDbPath))
//            {
//                Console.ReadKey();
//                return;
//            }

//            string clientConnStr = $"Provider=Microsoft.ACE.OLEDB.16.0;Data Source={clientDbPath};";
//            string serverConnStr = $"Provider=Microsoft.ACE.OLEDB.16.0;Data Source={serverDbPath};";

//            if (!TestConnection("Client DB", clientConnStr) || !TestConnection("Server DB", serverConnStr))
//            {
//                Console.ReadKey();
//                return;
//            }

//            // Load last sync times from file
//            var metadata = LoadSyncMetadata(syncMetaFile) ?? new SyncMetadata
//            {
//                LastClientSyncTime = GetMaxLastModified(serverConnStr, tableName),
//                LastServerSyncTime = GetMaxLastModified(clientConnStr, tableName)
//            };

//            _lastClientSyncTime = metadata.LastClientSyncTime;
//            _lastServerSyncTime = metadata.LastServerSyncTime;

//            Console.WriteLine("\nStarting continuous synchronization...");
//            Console.WriteLine("Press 'Q' then Enter to stop synchronization.\n");

//            var syncTask = Task.Run(() => ContinuousSync(serverConnStr, clientConnStr, tableName, pkColumn, syncMetaFile));

//            while (_syncRunning)
//            {
//                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
//                {
//                    _syncRunning = false;
//                    Console.WriteLine("Stopping synchronization...");
//                }
//                await Task.Delay(100);
//            }

//            await syncTask;
//            Console.WriteLine("Synchronization stopped. Press any key to exit.");


//            Console.ReadKey();
//        }

//        static SyncMetadata LoadSyncMetadata(string path)
//        {
//            if (!File.Exists(path)) return null;
//            var json = File.ReadAllText(path);
//            return JsonSerializer.Deserialize<SyncMetadata>(json);
//        }

//        static void SaveSyncMetadata(string path, SyncMetadata metadata)
//        {
//            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
//            File.WriteAllText(path, json);
//        }

//        static bool VerifyDatabaseFiles(string clientPath, string serverPath)
//        {
//            if (!File.Exists(clientPath))
//            {
//                Console.WriteLine($"Client database not found at: {clientPath}");
//                return false;
//            }

//            if (!File.Exists(serverPath))
//            {
//                Console.WriteLine($"Server database not found at: {serverPath}");
//                return false;
//            }

//            return true;
//        }

//        static bool TestConnection(string name, string connectionString)
//        {
//            Console.WriteLine($"Testing {name} connection...");
//            try
//            {
//                using var connection = new OleDbConnection(connectionString);
//                connection.Open();
//                Console.WriteLine($"{name} connection successful.");
//                return true;
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"{name} connection failed: {ex.Message}");
//                return false;
//            }
//        }

//        static DateTime GetMaxLastModified(string connectionString, string tableName)
//        {
//            try
//            {
//                using var conn = new OleDbConnection(connectionString);
//                conn.Open();
//                using var cmd = new OleDbCommand($"SELECT MAX(LastModified) FROM [{tableName}]", conn);
//                var result = cmd.ExecuteScalar();
//                if (result != DBNull.Value && result != null)
//                    return Convert.ToDateTime(result);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error fetching max LastModified: {ex.Message}");
//            }
//            return DateTime.MinValue;
//        }

//        static async Task ContinuousSync(string serverConnStr, string clientConnStr, string tableName, string pkColumn, string syncMetaFile)
//        {
//            while (_syncRunning)
//            {
//                try
//                {
//                    using var serverConn = new OleDbConnection(serverConnStr);
//                    using var clientConn = new OleDbConnection(clientConnStr);
//                    serverConn.Open();
//                    clientConn.Open();

//                    Console.WriteLine($"[{DateTime.Now:T}] Syncing Server → Client...");
//                    int serverToClientChanges = SyncDirection(
//                        sourceConn: serverConn,
//                        targetConn: clientConn,
//                        tableName: tableName,
//                        lastSyncTime: ref _lastServerSyncTime,
//                        isServerToClient: true,
//                        pkColumn: pkColumn);
//                    Console.WriteLine($"[{DateTime.Now:T}] Server → Client: {serverToClientChanges} changes applied");

//                    Console.WriteLine($"[{DateTime.Now:T}] Syncing Client → Server...");
//                    int clientToServerChanges = SyncDirection(
//                        sourceConn: clientConn,
//                        targetConn: serverConn,
//                        tableName: tableName,
//                        lastSyncTime: ref _lastClientSyncTime,
//                        isServerToClient: false,
//                        pkColumn: pkColumn);
//                    Console.WriteLine($"[{DateTime.Now:T}] Client → Server: {clientToServerChanges} changes applied");

//                    // Save updated sync times
//                    SaveSyncMetadata(syncMetaFile, new SyncMetadata
//                    {
//                        LastClientSyncTime = _lastClientSyncTime,
//                        LastServerSyncTime = _lastServerSyncTime
//                    });
//                    if (DateTime.Now.Second % 15 == 0)
//                    {
//                        Console.WriteLine($"[{DateTime.Now:T}] Checking for deletions...");
//                        SyncDeletions(serverConn, clientConn, tableName, pkColumn);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"[{DateTime.Now:T}] Sync error: {ex.Message}");
//                }

//                await Task.Delay(5000);
//            }
//        }

//        static int SyncDirection(OleDbConnection sourceConn, OleDbConnection targetConn,
//                                 string tableName, ref DateTime lastSyncTime,
//                                 bool isServerToClient, string pkColumn)
//        {
//            int changesApplied = 0;
//            DateTime maxTimestamp = lastSyncTime;

//            string getChangesQuery = $@"
//                SELECT * FROM [{tableName}]
//                WHERE LastModified > ?
//                ORDER BY LastModified";

//            using var cmd = new OleDbCommand(getChangesQuery, sourceConn);
//            cmd.Parameters.AddWithValue("@LastModified", lastSyncTime);

//            using var reader = cmd.ExecuteReader();
//            while (reader.Read())
//            {
//                var row = new Dictionary<string, object>();
//                for (int i = 0; i < reader.FieldCount; i++)
//                {
//                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
//                }

//                if (ApplyChangeWithConflictResolution(targetConn, tableName, row, isServerToClient, pkColumn))
//                {
//                    changesApplied++;
//                    var rowTimestamp = Convert.ToDateTime(row["LastModified"]);
//                    if (rowTimestamp > maxTimestamp)
//                        maxTimestamp = rowTimestamp;
//                }
//            }

//            lastSyncTime = maxTimestamp;
//            return changesApplied;
//        }

//        static bool ApplyChangeWithConflictResolution(OleDbConnection targetConn,
//                                                      string tableName,
//                                                      Dictionary<string, object> row,
//                                                      bool isServerToClient,
//                                                      string pkColumn)
//        {
//            var pkValue = row[pkColumn];
//            var incomingLastModified = Convert.ToDateTime(row["LastModified"]);

//            bool exists = RecordExists(targetConn, tableName, pkColumn, pkValue);
//            if (!exists)
//                return InsertRecord(targetConn, tableName, row);

//            var targetLastModified = GetLastModified(targetConn, tableName, pkColumn, pkValue);
//            var targetRecord = GetRecord(targetConn, tableName, pkColumn, pkValue);

//            if (isServerToClient)
//            {
//                bool dataIsDifferent = !row["Name"].Equals(targetRecord["Name"]);
//                if (dataIsDifferent && !IsConflictResolvedRecord(targetRecord))
//                {
//                    int newId = GetNextAvailableId(targetConn, tableName, pkColumn);
//                    targetRecord[pkColumn] = newId;
//                    targetRecord["Notes"] = AddConflictNote(targetRecord,
//                        $"Preserved client data (original ID: {pkValue}) before server overwrite");
//                    targetRecord["LastModified"] = GetSafeFutureTimestamp();

//                    InsertRecord(targetConn, tableName, targetRecord);
//                    Console.WriteLine($"Preserved client data for ID {pkValue} as new ID {newId}");
//                }

//                return UpdateRecord(targetConn, tableName, row, pkColumn);
//            }
//            else
//            {
//                Console.WriteLine($"Skipped client record for ID {pkValue}; server version retained");
//                return false;
//            }
//        }

//        static DateTime GetSafeFutureTimestamp()
//        {
//            var now = DateTime.Now;
//            return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second).AddSeconds(5);
//        }

//        static string AddConflictNote(Dictionary<string, object> record, string note)
//        {
//            if (record.ContainsKey("Notes") && record["Notes"] != null)
//            {
//                return record["Notes"].ToString() + "; " + note;
//            }
//            return note;
//        }

//        static bool IsConflictResolvedRecord(Dictionary<string, object> record)
//        {
//            return record.ContainsKey("Notes") &&
//                   record["Notes"] != null &&
//                   record["Notes"].ToString().Contains("CONFLICT_RESOLVED");
//        }

//        static Dictionary<string, object> GetRecord(OleDbConnection conn, string tableName, string pkColumn, object pkValue)
//        {
//            var record = new Dictionary<string, object>();
//            string query = $"SELECT * FROM [{tableName}] WHERE [{pkColumn}] = ?";

//            using var cmd = new OleDbCommand(query, conn);
//            cmd.Parameters.AddWithValue($"@{pkColumn}", pkValue);

//            using var reader = cmd.ExecuteReader();
//            if (reader.Read())
//            {
//                for (int i = 0; i < reader.FieldCount; i++)
//                    record[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
//            }

//            return record;
//        }

//        static bool UpdateRecord(OleDbConnection conn, string tableName, Dictionary<string, object> row, string pkColumn)
//        {
//            var columns = row.Keys.Where(k => k != pkColumn).ToList();
//            var updateSet = string.Join(", ", columns.Select(c => $"[{c}] = ?"));
//            string updateQuery = $@"UPDATE [{tableName}] SET {updateSet} WHERE [{pkColumn}] = ?";

//            using var cmd = new OleDbCommand(updateQuery, conn);
//            foreach (var col in columns)
//                cmd.Parameters.AddWithValue($"@{col}", row[col] ?? DBNull.Value);
//            cmd.Parameters.AddWithValue($"@{pkColumn}", row[pkColumn]);

//            return cmd.ExecuteNonQuery() > 0;
//        }

//        static bool InsertRecord(OleDbConnection conn, string tableName, Dictionary<string, object> row)
//        {
//            var columns = row.Keys.ToList();
//            var columnList = string.Join(", ", columns.Select(c => $"[{c}]"));
//            var valuePlaceholders = string.Join(", ", columns.Select(_ => "?"));

//            string insertQuery = $@"INSERT INTO [{tableName}] ({columnList}) VALUES ({valuePlaceholders})";

//            using var cmd = new OleDbCommand(insertQuery, conn);
//            foreach (var col in columns)
//                cmd.Parameters.AddWithValue($"@{col}", row[col] ?? DBNull.Value);

//            return cmd.ExecuteNonQuery() > 0;
//        }

//        static int GetNextAvailableId(OleDbConnection conn, string tableName, string pkColumn)
//        {
//            string query = $"SELECT MAX([{pkColumn}]) FROM [{tableName}]";
//            using var cmd = new OleDbCommand(query, conn);
//            var result = cmd.ExecuteScalar();
//            int maxId = (result != DBNull.Value && result != null) ? Convert.ToInt32(result) : 0;
//            return maxId + 1;
//        }

//        static bool RecordExists(OleDbConnection conn, string tableName, string pkColumn, object pkValue)
//        {
//            string query = $"SELECT COUNT(*) FROM [{tableName}] WHERE [{pkColumn}] = ?";
//            using var cmd = new OleDbCommand(query, conn);
//            cmd.Parameters.AddWithValue($"@{pkColumn}", pkValue);
//            return (int)cmd.ExecuteScalar() > 0;
//        }


//        static void SyncDeletions(OleDbConnection serverConn, OleDbConnection clientConn,
//                          string tableName, string pkColumn)
//        {
//            var serverIds = GetAllIds(serverConn, tableName, pkColumn);
//            var clientIds = GetAllIds(clientConn, tableName, pkColumn);

//            // IDs present in client but missing in server → delete from client
//            var toDeleteFromClient = clientIds.Except(serverIds).ToList();
//            foreach (var id in toDeleteFromClient)
//            {
//                DeleteRecord(clientConn, tableName, pkColumn, id);
//                Console.WriteLine($"Deleted ID {id} from client (missing in server)");
//            }

//            // IDs present in server but missing in client → delete from server
//            var toDeleteFromServer = serverIds.Except(clientIds).ToList();
//            foreach (var id in toDeleteFromServer)
//            {
//                DeleteRecord(serverConn, tableName, pkColumn, id);
//                Console.WriteLine($"Deleted ID {id} from server (missing in client)");
//            }
//        }
//        static List<int> GetAllIds(OleDbConnection conn, string tableName, string pkColumn)
//        {
//            var ids = new List<int>();
//            string query = $"SELECT [{pkColumn}] FROM [{tableName}]";
//            using var cmd = new OleDbCommand(query, conn);
//            using var reader = cmd.ExecuteReader();
//            while (reader.Read())
//            {
//                ids.Add(reader.GetInt32(0));
//            }
//            return ids;
//        }

//        static bool DeleteRecord(OleDbConnection conn, string tableName, string pkColumn, int pkValue)
//        {
//            string query = $"DELETE FROM [{tableName}] WHERE [{pkColumn}] = ?";
//            using var cmd = new OleDbCommand(query, conn);
//            cmd.Parameters.AddWithValue($"@{pkColumn}", pkValue);
//            return cmd.ExecuteNonQuery() > 0;
//        }

//        static DateTime GetLastModified(OleDbConnection conn, string tableName, string pkColumn, object pkValue)
//        {
//            string query = $"SELECT LastModified FROM [{tableName}] WHERE [{pkColumn}] = ?";
//            using var cmd = new OleDbCommand(query, conn);
//            cmd.Parameters.AddWithValue($"@{pkColumn}", pkValue);
//            var result = cmd.ExecuteScalar();
//            return (result != DBNull.Value && result != null) ? Convert.ToDateTime(result) : DateTime.MinValue;
//        }
//    }
//}