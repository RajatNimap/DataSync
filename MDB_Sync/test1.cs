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

//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static async Task Main()
//        {

//            string clientDbPath = @"C:\Users\Nimap\Documents\mdbfile\rajat.mdb";
//            string serverDbPath = @"\\192.168.1.87\MDB\suraj.mdb";
//            const string tableName = "MyTable";
//            const string pkColumn = "ID"; // Now GUID (string)
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

//            // Load last sync times
//            var metadata = LoadSyncMetadata(syncMetaFile) ?? new SyncMetadata
//            {
//                LastClientSyncTime = GetMaxLastModified(serverConnStr, tableName),
//                LastServerSyncTime = GetMaxLastModified(clientConnStr, tableName)
//            };

//            _lastClientSyncTime = metadata.LastClientSyncTime;
//            _lastServerSyncTime = metadata.LastServerSyncTime;

//            Console.WriteLine("\nStarting continuous synchronization...");
//            Console.WriteLine("Press 'Q' then Enter to stop.\n");

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
//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static SyncMetadata LoadSyncMetadata(string path)
//        {
//            if (!File.Exists(path)) return null;
//            var json = File.ReadAllText(path);
//            return JsonSerializer.Deserialize<SyncMetadata>(json);
//        }
//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static void SaveSyncMetadata(string path, SyncMetadata metadata)
//        {
//            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
//            File.WriteAllText(path, json);
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
//                return (result != DBNull.Value && result != null) ? Convert.ToDateTime(result) : DateTime.MinValue;
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error fetching max LastModified: {ex.Message}");
//                return DateTime.MinValue;
//            }
//        }
//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
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
//                        pkColumn: pkColumn);
//                    Console.WriteLine($"[{DateTime.Now:T}] Server → Client: {serverToClientChanges} changes applied");

//                    Console.WriteLine($"[{DateTime.Now:T}] Syncing Client → Server...");
//                    int clientToServerChanges = SyncDirection(
//                        sourceConn: clientConn,
//                        targetConn: serverConn,
//                        tableName: tableName,
//                        lastSyncTime: ref _lastClientSyncTime,
//                        pkColumn: pkColumn);
//                    Console.WriteLine($"[{DateTime.Now:T}] Client → Server: {clientToServerChanges} changes applied");

//                    // Save sync times
//                    SaveSyncMetadata(syncMetaFile, new SyncMetadata
//                    {
//                        LastClientSyncTime = _lastClientSyncTime,
//                        LastServerSyncTime = _lastServerSyncTime
//                    });

//                    // Deletion sync (every 10 seconds)
//                    await Task.Delay(10000);
//                    Console.WriteLine($"[{DateTime.Now:T}] Checking for deletions...");
//                    SyncDeletionsByComparison(serverConn, clientConn, tableName, pkColumn); // Server → Client
//                    SyncDeletionsByComparison(clientConn, serverConn, tableName, pkColumn); // Client → Server
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"[{DateTime.Now:T}] Sync error: {ex.Message}");
//                }
//                await Task.Delay(5000); // Wait before next cycle
//            }
//        }
//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static List<string> GetAllIds(OleDbConnection conn, string tableName, string pkColumn)
//        {
//            var ids = new List<string>();
//            string query = $"SELECT [{pkColumn}] FROM [{tableName}]";
//            using var cmd = new OleDbCommand(query, conn);
//            using var reader = cmd.ExecuteReader();
//            while (reader.Read())
//            {
//                ids.Add(reader[pkColumn].ToString()); // GUID as string
//            }
//            return ids;
//        }
//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static void SyncDeletionsByComparison(OleDbConnection sourceConn, OleDbConnection targetConn, string tableName, string pkColumn)
//        {
//            var sourceIds = GetAllIds(sourceConn, tableName, pkColumn);
//            var targetIds = GetAllIds(targetConn, tableName, pkColumn);
//            var idsToDelete = targetIds.Except(sourceIds).ToList();

//            foreach (var id in idsToDelete)
//            {
//                string deleteQuery = $"DELETE FROM [{tableName}] WHERE [{pkColumn}] = ?";
//                using var cmd = new OleDbCommand(deleteQuery, targetConn);
//                cmd.Parameters.AddWithValue($"@{pkColumn}", id);
//                if (cmd.ExecuteNonQuery() > 0)
//                    Console.WriteLine($"Deleted ID {id} from target");
//            }
//        }
//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static int SyncDirection(OleDbConnection sourceConn, OleDbConnection targetConn,
//                                 string tableName, ref DateTime lastSyncTime,
//                                 string pkColumn)
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

//                var rowTimestamp = Convert.ToDateTime(row["LastModified"]);
//                if (rowTimestamp > maxTimestamp)
//                    maxTimestamp = rowTimestamp;

//                if (ApplyChange(targetConn, tableName, row, pkColumn))
//                {
//                    changesApplied++;
//                }
//            }

//            lastSyncTime = maxTimestamp; // Update sync time to newest timestamp seen
//            return changesApplied;
//        }
//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static bool ApplyChange(OleDbConnection targetConn, string tableName,
//                               Dictionary<string, object> row, string pkColumn)
//        {
//            var pkValue = row[pkColumn].ToString(); // GUID as string
//            var incomingLastModified = Convert.ToDateTime(row["LastModified"]);

//            if (!RecordExists(targetConn, tableName, pkColumn, pkValue))
//            {
//                return InsertRecord(targetConn, tableName, row); // Insert new GUID record
//            }

//            // Get target's LastModified for this GUID
//            var targetLastModified = GetLastModified(targetConn, tableName, pkColumn, pkValue);

//            // Update only if source is newer
//            if (incomingLastModified > targetLastModified)
//            {
//                return UpdateRecord(targetConn, tableName, row, pkColumn);
//            }

//            return false; // Skip older changes
//        }
//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static bool RecordExists(OleDbConnection conn, string tableName, string pkColumn, string pkValue)
//        {
//            string query = $"SELECT COUNT(*) FROM [{tableName}] WHERE [{pkColumn}] = ?";
//            using var cmd = new OleDbCommand(query, conn);
//            cmd.Parameters.AddWithValue($"@{pkColumn}", pkValue);
//            return (int)cmd.ExecuteScalar() > 0;
//        }
//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
//        static DateTime GetLastModified(OleDbConnection conn, string tableName, string pkColumn, string pkValue)
//        {
//            string query = $"SELECT LastModified FROM [{tableName}] WHERE [{pkColumn}] = ?";
//            using var cmd = new OleDbCommand(query, conn);
//            cmd.Parameters.AddWithValue($"@{pkColumn}", pkValue);
//            var result = cmd.ExecuteScalar();
//            return (result != DBNull.Value && result != null) ? Convert.ToDateTime(result) : DateTime.MinValue;
//        }
//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
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
//        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
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
//    }
//}