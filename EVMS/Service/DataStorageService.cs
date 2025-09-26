using System;
using System.Configuration;
using Microsoft.Data.SqlClient;

namespace EVMS.Service
{
    internal class DataStorageService : IDisposable
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

        public void Dispose()
        {
            // Cleanup if needed
        }
    }

    internal class PartReadingDataModel
    {
        public string? Para_No { get; set; }
        public string? Parameter { get; set; }
        public double Nominal { get; set; }
        public double RTolPlus { get; set; }
        public double RTolMinus { get; set; }
    }

    internal class ProbeInstallModel
    {
        public string? PartNo { get; set; }
        public string? ProbeId { get; set; }
        public string? Name { get; set; }
        public int Channel { get; set; }

    }

    internal class MasterReadingModel
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
}
