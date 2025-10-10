using Microsoft.Data.SqlClient;
using System;
using System.Configuration;
using System.Diagnostics;

namespace EVMS.Service
{
    public class DataStorageService : IDisposable
    {
        private readonly string _connectionString;

        public DataStorageService()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["EVMSDb"].ConnectionString;

            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new ArgumentException("Connection string must not be empty.", nameof(_connectionString));
        }

        // Get PartConfig list by part number
        public List<PartReadingDataModel> GetPartConfigByPartNumber(string partNumber)
        {
            var list = new List<PartReadingDataModel>();
            string query = "SELECT * FROM PartConfig WHERE Para_No = @PartNumber";

            using SqlConnection conn = new(_connectionString);
            using SqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@PartNumber", partNumber);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new PartReadingDataModel
                {
                    Para_No = reader["Para_No"].ToString(),
                    Parameter = reader["Parameter"].ToString(),
                    Nominal = Convert.ToDouble(reader["Nominal"]),
                    RTolPlus = Convert.ToDouble(reader["RTolPlus"]),
                    RTolMinus = Convert.ToDouble(reader["RTolMinus"]),
                });
            }
            return list;
        }

        // Get ProbeInstallationData by part number
        public List<ProbeInstallModel> GetProbeInstallByPartNumber(string partNumber)
        {
            var list = new List<ProbeInstallModel>();
            string query = @"
                SELECT PartNo, ProbeId, Name
                FROM ProbeInstallationData
                WHERE PartNo = @PartNo";

            using SqlConnection conn = new(_connectionString);
            using SqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@PartNo", partNumber);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var item = new ProbeInstallModel
                {
                    PartNo = reader["PartNo"].ToString(),
                    ProbeId = reader["ProbeId"].ToString(),
                    Name = reader["Name"].ToString()
                };
                list.Add(item);
            }
            return list;
        }

        // Get MasterReadingData by part number
        public List<MasterReadingModel> GetMasterReadingByPart(string partNumber)
        {
            var list = new List<MasterReadingModel>();
            string query = @"
                SELECT Para_No, Parameter, Nominal, RTolPlus, RTolMinus
                FROM MasterReadingData
                WHERE Para_No = @ParaNo";

            using SqlConnection conn = new(_connectionString);
            using SqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@ParaNo", partNumber);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MasterReadingModel
                {
                    Para_No = reader["Para_No"].ToString(),
                    Parameter = reader["Parameter"].ToString(),
                    Nominal = Convert.ToDouble(reader["Nominal"]),
                    RTolPlus = Convert.ToDouble(reader["RTolPlus"]),
                    RTolMinus = Convert.ToDouble(reader["RTolMinus"])
                });
            }
            return list;
        }

        // Get Active parts from Part_Entry table (ActivePart = 1)
        public List<PartEntryModel> GetActiveParts()
        {
            var list = new List<PartEntryModel>();
            string query = "SELECT Para_No, Para_Name, ActivePart FROM Part_Entry WHERE ActivePart = 1";

            using SqlConnection conn = new(_connectionString);
            using SqlCommand cmd = new(query, conn);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new PartEntryModel
                {
                    Para_No = reader["Para_No"].ToString(),
                    Para_Name = reader["Para_Name"].ToString(),
                    ActivePart = Convert.ToInt32(reader["ActivePart"])
                });
            }
            return list;
        }


        public bool ProbeReferencesExist(string partNo)
        {
            string query = "SELECT COUNT(*) FROM MasterReadingProbeReference WHERE PartNo = @PartNo";

            using SqlConnection conn = new(_connectionString);
            using SqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@PartNo", partNo);

            conn.Open();
            int count = (int)cmd.ExecuteScalar();
            return count > 0;
        }

        public List<(string Name, double Value)> GetMasterProbeRef(string partNo)
        {
            string query = @"
        SELECT Name, Value
        FROM MasterReadingProbeReference
        WHERE PartNo = @PartNo";

            var result = new List<(string, double)>();

            using SqlConnection conn = new SqlConnection(_connectionString);
            using SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@PartNo", partNo);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string name = reader["Name"].ToString() ?? "";
                double value = reader["Value"] != DBNull.Value ? Convert.ToDouble(reader["Value"]) : 0.0;

                result.Add((name, value));
            }
            return result;
        }


        public void SaveProbeReadings(List<ProbeInstallModel> probes, string partNo, Dictionary<string, double> probeValues)
        {
            using SqlConnection conn = new SqlConnection(_connectionString);
            conn.Open();

            foreach (var probe in probes)
            {
                if (!string.IsNullOrEmpty(probe.ProbeId) && probeValues.TryGetValue(probe.ProbeId, out double value))
                {
                    string query = @"
                    MERGE MasterReadingProbeReference AS target
                    USING (VALUES (@PartNo, @ProbeId, @Name, @Value)) AS source (PartNo, ProbeId, Name, Value)
                    ON (target.PartNo = source.PartNo AND target.ProbeId = source.ProbeId)
                    WHEN MATCHED THEN
                        UPDATE SET Value = source.Value, LastUpdated = GETDATE(), Name = source.Name
                    WHEN NOT MATCHED THEN
                        INSERT (PartNo, ProbeId, Name, Value, LastUpdated)
                        VALUES (source.PartNo, source.ProbeId, source.Name, source.Value, GETDATE());
                ";

                    using SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@PartNo", partNo);
                    cmd.Parameters.AddWithValue("@ProbeId", probe.ProbeId);
                    cmd.Parameters.AddWithValue("@Name", probe.Name ?? "");
                    cmd.Parameters.AddWithValue("@Value", value);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<Controls> GetActiveBit()
        {
            var list = new List<Controls>();
            string query = "SELECT Description, Bit FROM Controls";

            using SqlConnection conn = new(_connectionString);
            using SqlCommand cmd = new(query, conn);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Controls
                {
                    Description = reader["Description"]?.ToString() ?? string.Empty,
                    Bit = reader["Bit"] != DBNull.Value ? Convert.ToInt32(reader["Bit"]) : 0
                });
            }
            return list;
        }

        public List<Dictionary<string, object>> GetAllMeasuredDataDynamic(string partNo, DateTime? filterDate = null)
        {
            var list = new List<Dictionary<string, object>>();

            string query = "SELECT * FROM MeasuredData WHERE PartNo = @PartNo";

            if (filterDate.HasValue)
            {
                query += " AND DateTime >= @StartDate AND DateTime < @EndDate ";
            }

            query += " ORDER BY DateTime ASC";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand(query, conn);

                cmd.Parameters.AddWithValue("@PartNo", partNo);

                if (filterDate.HasValue)
                {
                    var startDate = filterDate.Value.Date;
                    var endDate = startDate.AddDays(1);

                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                }

                conn.Open();

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 1; i < reader.FieldCount; i++)
                    {
                        var colName = reader.GetName(i);
                        var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        row[colName] = val;
                    }
                    list.Add(row);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DB Exception: " + ex.Message);
            }

            return list;
        }




        //public void SaveMeasuredDataDynamic(
        //    string partNo,
        //    DateTime dateTime,
        //    string lotNo,
        //    Dictionary<string, double> paramValues,
        //    string operatorName,
        //    string partStatus)
        //{
        //    var partParams = GetPartConfigByPartNumber(partNo);

        //    var columns = new List<string> { "DateTime", "LotNo" };
        //    var parameters = new List<string> { "@DateTime", "@LotNo" };
        //                var sqlParams = new List<SqlParameter>
        //        {
        //            new SqlParameter("@DateTime", dateTime),
        //            new SqlParameter("@LotNo", lotNo ?? string.Empty)
        //        };

        //    foreach (var param in partParams)
        //    {
        //        string colName = param.Parameter.Replace(" ", "_");
        //        if (paramValues.TryGetValue(param.Parameter, out double value))
        //        {
        //            columns.Add(colName);
        //            string paramName = "@" + colName;
        //            parameters.Add(paramName);
        //            sqlParams.Add(new SqlParameter(paramName, value));
        //        }
        //    }

        //    columns.Add("PartNo");
        //    parameters.Add("@PartNo");
        //    sqlParams.Add(new SqlParameter("@PartNo", partNo));

        //    columns.Add("Operator");
        //    parameters.Add("@Operator");
        //    sqlParams.Add(new SqlParameter("@Operator", operatorName ?? string.Empty));

        //    columns.Add("PartStatus");
        //    parameters.Add("@PartStatus");
        //    sqlParams.Add(new SqlParameter("@PartStatus", partStatus ?? string.Empty));

        //    string insertQuery = $"INSERT INTO MeasuredData ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameters)})";

        //    using SqlConnection conn = new SqlConnection(_connectionString);
        //    using SqlCommand cmd = new SqlCommand(insertQuery, conn);
        //    cmd.Parameters.AddRange(sqlParams.ToArray());

        //    conn.Open();
        //    cmd.ExecuteNonQuery();
        //}

        // Insert a new record into MasterInspection table
        public async Task InsertMasterInspectionAsync(string partNo, string operatorId, string lotNo,
     float ol, float de, float hd, float gp, float stdg, float stdu, float girDia, float stn,
     float efro, float sh, float sRo, float dg, string status)
        {
            string query = @"
    INSERT INTO MasterInspection 
    (PartNo, Operator_ID, LotNo, OL, DE, HD, GP, STDG, STDU, GIR_DIA, STN, EFRO, SH, S_RO, DG, Status, InspectionDate)
    VALUES 
    (@PartNo, @Operator_ID, @LotNo, @OL, @DE, @HD, @GP, @STDG, @STDU, @GIR_DIA, @STN, @EFRO, @SH, @S_RO, @DG, @Status, GETDATE())";

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@PartNo", partNo);
                command.Parameters.AddWithValue("@Operator_ID", operatorId);
                command.Parameters.AddWithValue("@LotNo", lotNo);
                command.Parameters.AddWithValue("@OL", ol.ToString("F3"));
                command.Parameters.AddWithValue("@DE", de.ToString("F3"));
                command.Parameters.AddWithValue("@HD", hd.ToString("F3"));
                command.Parameters.AddWithValue("@GP", gp.ToString("F3"));
                command.Parameters.AddWithValue("@STDG", stdg.ToString("F3"));
                command.Parameters.AddWithValue("@STDU", stdu.ToString("F3"));
                command.Parameters.AddWithValue("@GIR_DIA", girDia.ToString("F3"));
                command.Parameters.AddWithValue("@STN", stn.ToString("F3"));
                command.Parameters.AddWithValue("@EFRO", efro.ToString("F3"));
                command.Parameters.AddWithValue("@SH", sh.ToString("F3"));
                command.Parameters.AddWithValue("@S_RO", sRo.ToString("F3"));
                command.Parameters.AddWithValue("@DG", dg.ToString("F3"));

                command.Parameters.AddWithValue("@Status", status);

                await connection.OpenAsync();
                int rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                    throw new Exception("Insert failed: No rows were affected.");
            }
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }

    public class PartReadingDataModel
    {
        public string? Para_No { get; set; }
        public string? Parameter { get; set; }
        public double Nominal { get; set; }
        public double RTolPlus { get; set; }
        public double RTolMinus { get; set; }
    }

    public class ProbeInstallModel
    {
        public string? PartNo { get; set; }
        public string? ProbeId { get; set; }
        public string? Name { get; set; }
        public int Channel { get; set; }

    }

    public class MasterReadingModel
    {
        public string? Para_No { get; set; }
        public string? Parameter { get; set; }
        public double Nominal { get; set; }
        public double RTolPlus { get; set; }
        public double RTolMinus { get; set; }
    }

    public class PartEntryModel
    {
        public string? Para_No { get; set; }
        public string? Para_Name { get; set; }
        public int ActivePart { get; set; }  // 1=active, 0=deactive
    }

    public class MasterReadingProbeReferenceModel
    {
        public string? PartNo { get; set; }
        public string? ProbeId { get; set; }
        public string? ProbeName { get; set; }
        public double Value { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class Controls
    {
        public string ? Description { get; set; }

        public int Bit { get; set; }
    }
}
