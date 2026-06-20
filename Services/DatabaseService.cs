using InspectionApp.Models;
using Microsoft.Data.Sqlite;
using System.IO;

namespace InspectionApp.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "InspectionApp");
            Directory.CreateDirectory(folder);
            _connectionString = $"Data Source={Path.Combine(folder, "inspection.db")}";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS PartTypes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PartNumber TEXT NOT NULL UNIQUE
                );
                CREATE TABLE IF NOT EXISTS PartParameters (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PartTypeId INTEGER NOT NULL,
                    SerialNumber INTEGER NOT NULL,
                    ParameterName TEXT NOT NULL,
                    Frequency TEXT NOT NULL DEFAULT '',
                    SampleBox TEXT NOT NULL DEFAULT '',
                    InputType TEXT NOT NULL DEFAULT 'Value',
                    FOREIGN KEY (PartTypeId) REFERENCES PartTypes(Id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS InspectionSessions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PartNumber TEXT NOT NULL,
                    Shift TEXT NOT NULL,
                    Auditor TEXT NOT NULL,
                    SubmittedAt TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS InspectionReadings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SessionId INTEGER NOT NULL,
                    SerialNumber INTEGER NOT NULL,
                    ParameterName TEXT NOT NULL,
                    Reading TEXT NOT NULL DEFAULT '',
                    InputType TEXT NOT NULL DEFAULT 'Check',
                    FOREIGN KEY (SessionId) REFERENCES InspectionSessions(Id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS PPAPParts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PartNumber TEXT NOT NULL UNIQUE,
                    CreatedAt TEXT NOT NULL DEFAULT ''
                );
                CREATE TABLE IF NOT EXISTS PPAPParameters (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PPAPPartId INTEGER NOT NULL,
                    SerialNumber INTEGER NOT NULL,
                    ParameterName TEXT NOT NULL DEFAULT '',
                    InputType TEXT NOT NULL DEFAULT 'Value',
                    FOREIGN KEY (PPAPPartId) REFERENCES PPAPParts(Id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS PPAPSessions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PartNumber TEXT NOT NULL,
                    Shift TEXT NOT NULL,
                    Auditor TEXT NOT NULL,
                    SubmittedAt TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS PPAPReadings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SessionId INTEGER NOT NULL,
                    SerialNumber INTEGER NOT NULL,
                    ParameterName TEXT NOT NULL,
                    Reading TEXT NOT NULL DEFAULT '',
                    InputType TEXT NOT NULL DEFAULT 'Value',
                    FOREIGN KEY (SessionId) REFERENCES PPAPSessions(Id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS AuditorLog (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    AuditorName TEXT NOT NULL,
                    Context TEXT NOT NULL DEFAULT '',
                    LoggedAt TEXT NOT NULL
                );";
            cmd.ExecuteNonQuery();
        }

        public (bool Success, string Message) SavePartType(string partNumber, List<PartParameter> parameters)
        {
            if (string.IsNullOrWhiteSpace(partNumber))
                return (false, "Part Number cannot be empty.");
            if (parameters.Count == 0)
                return (false, "Add at least one parameter before saving.");

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM PartTypes WHERE PartNumber = @pn";
                checkCmd.Parameters.AddWithValue("@pn", partNumber.Trim());
                if ((long)checkCmd.ExecuteScalar()! > 0)
                    return (false, $"Part Number '{partNumber}' already exists.");

                var insertPt = conn.CreateCommand();
                insertPt.CommandText = "INSERT INTO PartTypes (PartNumber) VALUES (@pn); SELECT last_insert_rowid();";
                insertPt.Parameters.AddWithValue("@pn", partNumber.Trim());
                var ptId = (long)insertPt.ExecuteScalar()!;

                foreach (var p in parameters)
                {
                    var insertP = conn.CreateCommand();
                    insertP.CommandText = @"
                        INSERT INTO PartParameters (PartTypeId, SerialNumber, ParameterName, Frequency, SampleBox, InputType)
                        VALUES (@ptId, @sn, @name, @freq, @sample, @type)";
                    insertP.Parameters.AddWithValue("@ptId", ptId);
                    insertP.Parameters.AddWithValue("@sn", p.SerialNumber);
                    insertP.Parameters.AddWithValue("@name", p.ParameterName);
                    insertP.Parameters.AddWithValue("@freq", p.Frequency);
                    insertP.Parameters.AddWithValue("@sample", p.SampleBox);
                    insertP.Parameters.AddWithValue("@type", p.InputType.ToString());
                    insertP.ExecuteNonQuery();
                }

                tx.Commit();
                return (true, $"Part Type '{partNumber}' saved successfully.");
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return (false, $"Save failed: {ex.Message}");
            }
        }

        public (bool Success, string Message) UpdatePartType(string partNumber, List<PartParameter> parameters)
        {
            if (parameters.Count == 0)
                return (false, "Add at least one parameter before saving.");

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                var getCmd = conn.CreateCommand();
                getCmd.CommandText = "SELECT Id FROM PartTypes WHERE PartNumber = @pn";
                getCmd.Parameters.AddWithValue("@pn", partNumber.Trim());
                var idObj = getCmd.ExecuteScalar();
                if (idObj == null) return (false, $"Part '{partNumber}' not found in database.");
                long ptId = (long)idObj;

                var delCmd = conn.CreateCommand();
                delCmd.CommandText = "DELETE FROM PartParameters WHERE PartTypeId = @id";
                delCmd.Parameters.AddWithValue("@id", ptId);
                delCmd.ExecuteNonQuery();

                foreach (var p in parameters)
                {
                    var ins = conn.CreateCommand();
                    ins.CommandText = @"INSERT INTO PartParameters (PartTypeId, SerialNumber, ParameterName, Frequency, SampleBox, InputType)
                                        VALUES (@ptId, @sn, @name, @freq, @sample, @type)";
                    ins.Parameters.AddWithValue("@ptId", ptId);
                    ins.Parameters.AddWithValue("@sn", p.SerialNumber);
                    ins.Parameters.AddWithValue("@name", p.ParameterName);
                    ins.Parameters.AddWithValue("@freq", p.Frequency);
                    ins.Parameters.AddWithValue("@sample", p.SampleBox);
                    ins.Parameters.AddWithValue("@type", p.InputType.ToString());
                    ins.ExecuteNonQuery();
                }

                tx.Commit();
                return (true, $"Part Type '{partNumber}' updated successfully.");
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return (false, $"Update failed: {ex.Message}");
            }
        }

        public (bool Success, string Message) RenameAndUpdatePartType(string originalPartNumber, string newPartNumber, List<PartParameter> parameters)
        {
            if (string.IsNullOrWhiteSpace(newPartNumber))
                return (false, "Part Number cannot be empty.");
            if (parameters.Count == 0)
                return (false, "Add at least one parameter before saving.");

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                var getCmd = conn.CreateCommand();
                getCmd.CommandText = "SELECT Id FROM PartTypes WHERE PartNumber = @pn";
                getCmd.Parameters.AddWithValue("@pn", originalPartNumber.Trim());
                var idObj = getCmd.ExecuteScalar();
                if (idObj == null) return (false, $"Part '{originalPartNumber}' not found in database.");
                long ptId = (long)idObj;

                bool isRename = !string.Equals(originalPartNumber.Trim(), newPartNumber.Trim(), StringComparison.OrdinalIgnoreCase);
                if (isRename)
                {
                    var checkCmd = conn.CreateCommand();
                    checkCmd.CommandText = "SELECT COUNT(*) FROM PartTypes WHERE PartNumber = @pn";
                    checkCmd.Parameters.AddWithValue("@pn", newPartNumber.Trim());
                    if ((long)checkCmd.ExecuteScalar()! > 0)
                        return (false, $"Part Number '{newPartNumber}' already exists.");

                    var renameCmd = conn.CreateCommand();
                    renameCmd.CommandText = "UPDATE PartTypes SET PartNumber = @newPn WHERE Id = @id";
                    renameCmd.Parameters.AddWithValue("@newPn", newPartNumber.Trim());
                    renameCmd.Parameters.AddWithValue("@id", ptId);
                    renameCmd.ExecuteNonQuery();
                }

                var delCmd = conn.CreateCommand();
                delCmd.CommandText = "DELETE FROM PartParameters WHERE PartTypeId = @id";
                delCmd.Parameters.AddWithValue("@id", ptId);
                delCmd.ExecuteNonQuery();

                foreach (var p in parameters)
                {
                    var ins = conn.CreateCommand();
                    ins.CommandText = @"INSERT INTO PartParameters (PartTypeId, SerialNumber, ParameterName, Frequency, SampleBox, InputType)
                                        VALUES (@ptId, @sn, @name, @freq, @sample, @type)";
                    ins.Parameters.AddWithValue("@ptId", ptId);
                    ins.Parameters.AddWithValue("@sn", p.SerialNumber);
                    ins.Parameters.AddWithValue("@name", p.ParameterName);
                    ins.Parameters.AddWithValue("@freq", p.Frequency);
                    ins.Parameters.AddWithValue("@sample", p.SampleBox);
                    ins.Parameters.AddWithValue("@type", p.InputType.ToString());
                    ins.ExecuteNonQuery();
                }

                tx.Commit();
                string msg = isRename
                    ? $"Part '{originalPartNumber}' renamed to '{newPartNumber}' and updated."
                    : $"Part Type '{newPartNumber}' updated successfully.";
                return (true, msg);
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return (false, $"Update failed: {ex.Message}");
            }
        }

        public (bool Success, string Message) DeletePartType(string partNumber)
        {
            if (string.IsNullOrWhiteSpace(partNumber))
                return (false, "Part Number cannot be empty.");

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            // Make sure cascade delete actually fires for the parameters table
            using (var pragma = conn.CreateCommand())
            {
                pragma.CommandText = "PRAGMA foreign_keys = ON";
                pragma.ExecuteNonQuery();
            }

            using var tx = conn.BeginTransaction();
            try
            {
                var getCmd = conn.CreateCommand();
                getCmd.CommandText = "SELECT Id FROM PartTypes WHERE PartNumber = @pn";
                getCmd.Parameters.AddWithValue("@pn", partNumber.Trim());
                var idObj = getCmd.ExecuteScalar();
                if (idObj == null) return (false, $"Part '{partNumber}' not found in database.");
                long ptId = (long)idObj;

                // Belt-and-braces: delete parameters explicitly even though FK cascades.
                var delParams = conn.CreateCommand();
                delParams.CommandText = "DELETE FROM PartParameters WHERE PartTypeId = @id";
                delParams.Parameters.AddWithValue("@id", ptId);
                delParams.ExecuteNonQuery();

                var delPt = conn.CreateCommand();
                delPt.CommandText = "DELETE FROM PartTypes WHERE Id = @id";
                delPt.Parameters.AddWithValue("@id", ptId);
                delPt.ExecuteNonQuery();

                tx.Commit();
                return (true, $"Part Type '{partNumber}' deleted.");
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return (false, $"Delete failed: {ex.Message}");
            }
        }

        public List<string> GetAllPartNumbers()
        {
            var result = new List<string>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT PartNumber FROM PartTypes ORDER BY PartNumber COLLATE NOCASE";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) result.Add(reader.GetString(0));
            return result;
        }

        public List<PartParameter> GetParametersForPart(string partNumber)
        {
            var result = new List<PartParameter>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT pp.Id, pp.PartTypeId, pp.SerialNumber, pp.ParameterName,
                       pp.Frequency, pp.SampleBox, pp.InputType
                FROM PartParameters pp
                INNER JOIN PartTypes pt ON pt.Id = pp.PartTypeId
                WHERE pt.PartNumber = @pn
                ORDER BY pp.SerialNumber";
            cmd.Parameters.AddWithValue("@pn", partNumber);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new PartParameter
                {
                    Id = reader.GetInt32(0),
                    PartTypeId = reader.GetInt32(1),
                    SerialNumber = reader.GetInt32(2),
                    ParameterName = reader.GetString(3),
                    Frequency = reader.GetString(4),
                    SampleBox = reader.GetString(5),
                    InputType = Enum.Parse<ParameterInputType>(reader.GetString(6))
                });
            }
            return result;
        }

        // ── Inspection Sessions ───────────────────────────────────────────────────

        public int SaveInspectionSession(string partNumber, string shift, string auditor, List<Models.InspectionReading> readings)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                var insSession = conn.CreateCommand();
                insSession.CommandText = @"
                    INSERT INTO InspectionSessions (PartNumber, Shift, Auditor, SubmittedAt)
                    VALUES (@pn, @shift, @auditor, @at);
                    SELECT last_insert_rowid();";
                insSession.Parameters.AddWithValue("@pn",      partNumber);
                insSession.Parameters.AddWithValue("@shift",   shift);
                insSession.Parameters.AddWithValue("@auditor", auditor);
                insSession.Parameters.AddWithValue("@at",      DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                long sessionId = (long)insSession.ExecuteScalar()!;

                foreach (var r in readings)
                {
                    var insReading = conn.CreateCommand();
                    insReading.CommandText = @"
                        INSERT INTO InspectionReadings (SessionId, SerialNumber, ParameterName, Reading, InputType)
                        VALUES (@sid, @sn, @name, @reading, @type)";
                    insReading.Parameters.AddWithValue("@sid",     sessionId);
                    insReading.Parameters.AddWithValue("@sn",      r.SerialNumber);
                    insReading.Parameters.AddWithValue("@name",    r.ParameterName);
                    insReading.Parameters.AddWithValue("@reading", r.Reading);
                    insReading.Parameters.AddWithValue("@type",    r.InputType);
                    insReading.ExecuteNonQuery();
                }

                tx.Commit();
                return (int)sessionId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public List<Models.InspectionSession> GetInspectionSessions(
            string? partNumber = null,
            string? shift      = null,
            string? auditor    = null,
            DateTime? fromDate = null,
            DateTime? toDate   = null)
        {
            var sessions = new List<Models.InspectionSession>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var conditions = new List<string>();
            if (partNumber != null) conditions.Add("PartNumber = @pn");
            if (shift      != null) conditions.Add("Shift = @shift");
            if (auditor    != null) conditions.Add("Auditor LIKE @auditor");
            if (fromDate   != null) conditions.Add("SubmittedAt >= @from");
            if (toDate     != null) conditions.Add("SubmittedAt <= @to");

            string where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

            var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT Id, PartNumber, Shift, Auditor, SubmittedAt FROM InspectionSessions {where} ORDER BY SubmittedAt";
            if (partNumber != null) cmd.Parameters.AddWithValue("@pn",      partNumber);
            if (shift      != null) cmd.Parameters.AddWithValue("@shift",   shift);
            if (auditor    != null) cmd.Parameters.AddWithValue("@auditor", $"%{auditor}%");
            if (fromDate   != null) cmd.Parameters.AddWithValue("@from",    fromDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            if (toDate     != null) cmd.Parameters.AddWithValue("@to",      toDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                sessions.Add(new Models.InspectionSession
                {
                    Id          = reader.GetInt32(0),
                    PartNumber  = reader.GetString(1),
                    Shift       = reader.GetString(2),
                    Auditor     = reader.GetString(3),
                    SubmittedAt = DateTime.Parse(reader.GetString(4))
                });
            }

            foreach (var session in sessions)
            {
                var readCmd = conn.CreateCommand();
                readCmd.CommandText = @"
                    SELECT SerialNumber, ParameterName, Reading, InputType
                    FROM InspectionReadings WHERE SessionId = @sid ORDER BY SerialNumber";
                readCmd.Parameters.AddWithValue("@sid", session.Id);
                using var rdr = readCmd.ExecuteReader();
                while (rdr.Read())
                {
                    session.Readings.Add(new Models.InspectionReading
                    {
                        SerialNumber  = rdr.GetInt32(0),
                        ParameterName = rdr.GetString(1),
                        Reading       = rdr.GetString(2),
                        InputType     = rdr.GetString(3)
                    });
                }
            }

            return sessions;
        }

        public List<string> GetDistinctShifts(string? partNumber = null)
        {
            var result = new List<string>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = partNumber == null
                ? "SELECT DISTINCT Shift FROM InspectionSessions ORDER BY Shift"
                : "SELECT DISTINCT Shift FROM InspectionSessions WHERE PartNumber = @pn ORDER BY Shift";
            if (partNumber != null) cmd.Parameters.AddWithValue("@pn", partNumber);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) result.Add(reader.GetString(0));
            return result;
        }

        public List<string> GetDistinctAuditors(string? partNumber = null)
        {
            var result = new List<string>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = partNumber == null
                ? "SELECT DISTINCT Auditor FROM InspectionSessions ORDER BY Auditor COLLATE NOCASE"
                : "SELECT DISTINCT Auditor FROM InspectionSessions WHERE PartNumber = @pn ORDER BY Auditor COLLATE NOCASE";
            if (partNumber != null) cmd.Parameters.AddWithValue("@pn", partNumber);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) result.Add(reader.GetString(0));
            return result;
        }

        // ── PPAP ─────────────────────────────────────────────────────────────────

        public void CreatePPAPPart(string partNumber)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO PPAPParts (PartNumber, CreatedAt) VALUES (@pn, @at)";
            cmd.Parameters.AddWithValue("@pn", partNumber);
            cmd.Parameters.AddWithValue("@at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
        }

        public void LogAuditor(string auditorName, string context)
        {
            if (string.IsNullOrWhiteSpace(auditorName)) return;
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO AuditorLog (AuditorName, Context, LoggedAt) VALUES (@name, @ctx, @at)";
            cmd.Parameters.AddWithValue("@name", auditorName.Trim());
            cmd.Parameters.AddWithValue("@ctx",  context);
            cmd.Parameters.AddWithValue("@at",   DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
        }

        public List<(DateTime Time, string AuditorName, string Context)> GetAuditorHistory()
        {
            var result = new List<(DateTime, string, string)>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT LoggedAt, AuditorName, Context FROM AuditorLog ORDER BY LoggedAt DESC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add((DateTime.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2)));
            return result;
        }

        public List<string> GetAllPPAPPartNumbers()
        {
            var result = new List<string>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT PartNumber FROM PPAPParts ORDER BY PartNumber COLLATE NOCASE";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) result.Add(reader.GetString(0));
            return result;
        }

        public List<(int SerialNumber, string ParameterName, string InputType)> GetPPAPParameters(string partNumber)
        {
            var result = new List<(int, string, string)>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT pp.SerialNumber, pp.ParameterName, pp.InputType
                FROM PPAPParameters pp
                INNER JOIN PPAPParts p ON p.Id = pp.PPAPPartId
                WHERE p.PartNumber = @pn ORDER BY pp.SerialNumber";
            cmd.Parameters.AddWithValue("@pn", partNumber);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
            return result;
        }

        public void SaveOrUpdatePPAPPart(string partNumber,
            List<(int SerialNumber, string ParameterName, string InputType)> parameters)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            var getCmd = conn.CreateCommand();
            getCmd.CommandText = "SELECT Id FROM PPAPParts WHERE PartNumber = @pn";
            getCmd.Parameters.AddWithValue("@pn", partNumber);
            var idObj = getCmd.ExecuteScalar();

            long partId;
            if (idObj == null)
            {
                var insCmd = conn.CreateCommand();
                insCmd.CommandText = "INSERT INTO PPAPParts (PartNumber, CreatedAt) VALUES (@pn, @at); SELECT last_insert_rowid();";
                insCmd.Parameters.AddWithValue("@pn", partNumber);
                insCmd.Parameters.AddWithValue("@at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                partId = (long)insCmd.ExecuteScalar()!;
            }
            else
            {
                partId = (long)idObj;
                var delCmd = conn.CreateCommand();
                delCmd.CommandText = "DELETE FROM PPAPParameters WHERE PPAPPartId = @id";
                delCmd.Parameters.AddWithValue("@id", partId);
                delCmd.ExecuteNonQuery();
            }

            foreach (var p in parameters)
            {
                var ins = conn.CreateCommand();
                ins.CommandText = "INSERT INTO PPAPParameters (PPAPPartId, SerialNumber, ParameterName, InputType) VALUES (@id, @sn, @name, @type)";
                ins.Parameters.AddWithValue("@id",   partId);
                ins.Parameters.AddWithValue("@sn",   p.SerialNumber);
                ins.Parameters.AddWithValue("@name", p.ParameterName);
                ins.Parameters.AddWithValue("@type", p.InputType);
                ins.ExecuteNonQuery();
            }

            tx.Commit();
        }

        public int SavePPAPSession(string partNumber, string shift, string auditor,
            List<Models.InspectionReading> readings)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                var insSession = conn.CreateCommand();
                insSession.CommandText = @"
                    INSERT INTO PPAPSessions (PartNumber, Shift, Auditor, SubmittedAt)
                    VALUES (@pn, @shift, @auditor, @at); SELECT last_insert_rowid();";
                insSession.Parameters.AddWithValue("@pn",      partNumber);
                insSession.Parameters.AddWithValue("@shift",   shift);
                insSession.Parameters.AddWithValue("@auditor", auditor);
                insSession.Parameters.AddWithValue("@at",      DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                long sessionId = (long)insSession.ExecuteScalar()!;

                foreach (var r in readings)
                {
                    var ins = conn.CreateCommand();
                    ins.CommandText = @"INSERT INTO PPAPReadings (SessionId, SerialNumber, ParameterName, Reading, InputType)
                                        VALUES (@sid, @sn, @name, @reading, @type)";
                    ins.Parameters.AddWithValue("@sid",     sessionId);
                    ins.Parameters.AddWithValue("@sn",      r.SerialNumber);
                    ins.Parameters.AddWithValue("@name",    r.ParameterName);
                    ins.Parameters.AddWithValue("@reading", r.Reading);
                    ins.Parameters.AddWithValue("@type",    r.InputType);
                    ins.ExecuteNonQuery();
                }

                tx.Commit();
                return (int)sessionId;
            }
            catch { tx.Rollback(); throw; }
        }

        public List<Models.InspectionSession> GetPPAPSessions(
            string? partNumber = null,
            string? shift      = null,
            string? auditor    = null,
            DateTime? fromDate = null,
            DateTime? toDate   = null)
        {
            var sessions = new List<Models.InspectionSession>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var conditions = new List<string>();
            if (partNumber != null) conditions.Add("PartNumber = @pn");
            if (shift      != null) conditions.Add("Shift = @shift");
            if (auditor    != null) conditions.Add("Auditor LIKE @auditor");
            if (fromDate   != null) conditions.Add("SubmittedAt >= @from");
            if (toDate     != null) conditions.Add("SubmittedAt <= @to");
            string where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

            var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT Id, PartNumber, Shift, Auditor, SubmittedAt FROM PPAPSessions {where} ORDER BY SubmittedAt";
            if (partNumber != null) cmd.Parameters.AddWithValue("@pn",      partNumber);
            if (shift      != null) cmd.Parameters.AddWithValue("@shift",   shift);
            if (auditor    != null) cmd.Parameters.AddWithValue("@auditor", $"%{auditor}%");
            if (fromDate   != null) cmd.Parameters.AddWithValue("@from",    fromDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            if (toDate     != null) cmd.Parameters.AddWithValue("@to",      toDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                sessions.Add(new Models.InspectionSession
                {
                    Id = reader.GetInt32(0), PartNumber = reader.GetString(1),
                    Shift = reader.GetString(2), Auditor = reader.GetString(3),
                    SubmittedAt = DateTime.Parse(reader.GetString(4))
                });

            foreach (var session in sessions)
            {
                var readCmd = conn.CreateCommand();
                readCmd.CommandText = @"SELECT SerialNumber, ParameterName, Reading, InputType
                    FROM PPAPReadings WHERE SessionId = @sid ORDER BY SerialNumber";
                readCmd.Parameters.AddWithValue("@sid", session.Id);
                using var rdr = readCmd.ExecuteReader();
                while (rdr.Read())
                    session.Readings.Add(new Models.InspectionReading
                    {
                        SerialNumber = rdr.GetInt32(0), ParameterName = rdr.GetString(1),
                        Reading = rdr.GetString(2), InputType = rdr.GetString(3)
                    });
            }

            return sessions;
        }

        public List<string> GetDistinctPPAPShifts(string? partNumber = null)
        {
            var result = new List<string>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = partNumber == null
                ? "SELECT DISTINCT Shift FROM PPAPSessions ORDER BY Shift"
                : "SELECT DISTINCT Shift FROM PPAPSessions WHERE PartNumber = @pn ORDER BY Shift";
            if (partNumber != null) cmd.Parameters.AddWithValue("@pn", partNumber);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) result.Add(reader.GetString(0));
            return result;
        }
    }
}
