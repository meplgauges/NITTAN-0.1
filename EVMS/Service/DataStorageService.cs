using Microsoft.Data.SqlClient;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Data;
using System.Windows;


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
                    ShortName = reader["ShortName"].ToString(),
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



        public List<PartConfigModel> GetPartConfig(string partNumber)
        {
            var list = new List<PartConfigModel>();
            string query = @"
        SELECT Parameter, Nominal, RTolPlus, RTolMinus, YTolPlus, YTolMinus, ProbeStatus, Para_No
        FROM PartConfig
        WHERE Para_No = @ParaNo";

            using SqlConnection conn = new(_connectionString);
            using SqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@ParaNo", partNumber);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new PartConfigModel
                {
                    Parameter = reader["Parameter"].ToString(),
                    Nominal = Convert.ToDouble(reader["Nominal"]),
                    RTolPlus = Convert.ToDouble(reader["RTolPlus"]),
                    RTolMinus = Convert.ToDouble(reader["RTolMinus"]),
                    YTolPlus = Convert.ToDouble(reader["YTolPlus"]),
                    YTolMinus = Convert.ToDouble(reader["YTolMinus"]),
                    Para_No = reader["Para_No"].ToString()


                });
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

        public List<Dictionary<string, object>> GetAllMeasurementReadingsDynamic(string partNo, DateTime? filterDate = null)
        {
            var list = new List<Dictionary<string, object>>();

            string query = "SELECT * FROM MeasurementReading WHERE PartNo = @PartNo";

            if (filterDate.HasValue)
            {
                query += " AND MeasurementDate >= @StartDate AND MeasurementDate < @EndDate ";
            }

            query += " ORDER BY MeasurementDate ASC";

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
                MessageBox.Show("DB Exception: " + ex.Message);
            }

            return list;
        }


        public void UpdateAutoManualBit(int bitValue)
        {
            string sql = "UPDATE Controls SET Bit = @bit WHERE Description = 'Auto/Manual'";

            using (var conn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@bit", bitValue);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public int GetAutoManualBit()
        {
            string sql = "SELECT Bit FROM Controls WHERE Description = 'Auto/Manual'";

            using (var conn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 0; // Default to Manual (0)
            }
        }



        // Insert a new record into MasterInspection table
        public async Task InsertMasterInspectionAsync(string partNo, string operatorId, string lotNo,
    float ol, float de, float hd, float gp, float stdg, float stdu, float girDia,
    float stn, float ovalitySdg, float ovalitySdu, float ovalityHead,
    float stemTaper, float efro, float faceRunout, float sh, float sRo, float dg,
    string status)
        {
            string query = @"
        INSERT INTO MasterInspection
        (PartNo, Operator_ID, LotNo, OL, DE, HD, GP, STDG, STDU, GIR_DIA, STN, Ovality_SDG, Ovality_SDU, Ovality_Head, Stem_Taper, EFRO, Face_Runout, SH, S_RO, DG, Status, InspectionDate)
        VALUES 
        (@PartNo, @Operator_ID, @LotNo, @OL, @DE, @HD, @GP, @STDG, @STDU, @GIR_DIA, @STN, @Ovality_SDG, @Ovality_SDU, @Ovality_Head, @Stem_Taper, @EFRO, @Face_Runout, @SH, @S_RO, @DG, @Status, GETDATE())";

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, connection);

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
            command.Parameters.AddWithValue("@Ovality_SDG", ovalitySdg.ToString("F3"));
            command.Parameters.AddWithValue("@Ovality_SDU", ovalitySdu.ToString("F3"));
            command.Parameters.AddWithValue("@Ovality_Head", ovalityHead.ToString("F3"));
            command.Parameters.AddWithValue("@Stem_Taper", stemTaper.ToString("F3"));
            command.Parameters.AddWithValue("@EFRO", efro.ToString("F3"));
            command.Parameters.AddWithValue("@Face_Runout", faceRunout.ToString("F3"));
            command.Parameters.AddWithValue("@SH", sh.ToString("F3"));
            command.Parameters.AddWithValue("@S_RO", sRo.ToString("F3"));
            command.Parameters.AddWithValue("@DG", dg.ToString("F3"));
            command.Parameters.AddWithValue("@Status", status);

            await connection.OpenAsync();
            int rowsAffected = await command.ExecuteNonQueryAsync();
            if (rowsAffected == 0)
                throw new Exception("Insert failed: No rows were affected.");
        }


        public async Task InsertMeasurementReadingAsync(
    string partNo,
    string operatorId,
    string lotNo,
    float ol, float de, float hd, float gp, float stdg, float stdu, float girDia,
    float stn, float ovalitySdg, float ovalitySdu, float ovalityHead, float stemTaper,
    float efro, float faceRunout, float sh, float sRo, float dg,
    string status)
        {
            string query = @"
    INSERT INTO MeasurementReading
    (PartNo, Operator_ID, LotNo, OL, DE, HD, GP, STDG, STDU, GIR_DIA, STN, Ovality_SDG, Ovality_SDU, Ovality_Head, Stem_Taper, EFRO, Face_Runout, SH, S_RO, DG, Status, MeasurementDate)
    VALUES
    (@PartNo, @Operator_ID, @LotNo, @OL, @DE, @HD, @GP, @STDG, @STDU, @GIR_DIA, @STN, @Ovality_SDG, @Ovality_SDU, @Ovality_Head, @Stem_Taper, @EFRO, @Face_Runout, @SH, @S_RO, @DG, @Status, GETDATE())";

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, connection);

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
            command.Parameters.AddWithValue("@Ovality_SDG", ovalitySdg.ToString("F3"));
            command.Parameters.AddWithValue("@Ovality_SDU", ovalitySdu.ToString("F3"));
            command.Parameters.AddWithValue("@Ovality_Head", ovalityHead.ToString("F3"));
            command.Parameters.AddWithValue("@Stem_Taper", stemTaper.ToString("F3"));
            command.Parameters.AddWithValue("@EFRO", efro.ToString("F3"));
            command.Parameters.AddWithValue("@Face_Runout", faceRunout.ToString("F3"));
            command.Parameters.AddWithValue("@SH", sh.ToString("F3"));
            command.Parameters.AddWithValue("@S_RO", sRo.ToString("F3"));
            command.Parameters.AddWithValue("@DG", dg.ToString("F3"));
            command.Parameters.AddWithValue("@Status", status);

            await connection.OpenAsync();
            int rowsAffected = await command.ExecuteNonQueryAsync();
            if (rowsAffected == 0)
                throw new Exception("Insert failed: No rows were affected.");
        }

        public async Task<DataTable> GetParameterDataTableAsync(string partNo)
        {
            DataTable dt = new DataTable();

            string query = @"
        SELECT Parameter, Value
        FROM MeasurementTable  -- Replace with your actual table
        WHERE PartNo = @PartNo";

            using (var conn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@PartNo", partNo);
                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    dt.Load(reader);
                }
            }

            return dt;
        }



        public (int Mode, int SetValue, DateTime UpdatedAt) GetMasterExpiration()
        {
            using var con = new SqlConnection(_connectionString); // class member
            con.Open();
            using var cmd = new SqlCommand("SELECT Mode, SetValue, UpdatedAt FROM MasterExpiration WHERE Id = 1", con);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                bool modeBool = reader.GetBoolean(0); // Correct for BIT type
                int mode = modeBool ? 1 : 0;          // Convert to int
                return (mode, reader.GetInt32(1), reader.GetDateTime(2));
            }
            throw new InvalidOperationException("Master expiration settings not found.");
        }


        public void Dispose()
        {
            // Cleanup if needed
        }
    }



    public class PartConfigModel
    {
       // public string SrNo { get; set; }
        public string? Parameter { get; set; }
        public double Nominal { get; set; }
        public double RTolPlus { get; set; }
        public double RTolMinus { get; set; }
        public double YTolPlus { get; set; }
        public double YTolMinus { get; set; }
        public string? Para_No { get; set; }

    }

    public class PartReadingDataModel
    {
        public string? Para_No { get; set; }
        public string? Parameter { get; set; }
        public string? ShortName { get; set; }

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
