using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace EVMS.Service
{
    internal class DataStorageService : IDisposable
    {
        private readonly string _connectionString;

        public DataStorageService(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string must not be empty.", nameof(connectionString));

            _connectionString = connectionString;
        }


        public List<MasterReadingDataModel> GetMasterReadingParameters(string paraNo)
        {
            var list = new List<MasterReadingDataModel>();
            string query = @"
        SELECT Para_No, Parameter, Nominal, RTolPlus, RTolMinus
        FROM MasterReadingData
        WHERE Para_No = @ParaNo";
            using SqlConnection conn = new(_connectionString);
            using SqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@ParaNo", paraNo);
            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var item = new MasterReadingDataModel
                {
                    Para_No = reader.IsDBNull(0) ? null : reader.GetString(0),
                    Parameter = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Nominal = reader.IsDBNull(2) ? 0 : reader.GetDouble(2),
                    RTolPlus = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                    RTolMinus = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                };
                list.Add(item);
            }
            return list;
        }


        // Fetch PartConfig by part number
        public List<PartConfigModel> GetPartConfigByPartNumber(string partNumber)
        {
            var list = new List<PartConfigModel>();
            string query = "SELECT PartID, PartNumber, PartName, Description FROM PartConfig WHERE PartNumber = @PartNumber";

            using SqlConnection conn = new(_connectionString);
            using SqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@PartNumber", partNumber);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var item = new PartConfigModel
                {
                    PartID = reader.GetInt32(0),
                    PartNumber = reader.GetString(1),
                    PartName = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CreatedDate = reader.GetDateTime(4)
                };
                list.Add(item);
            }
            return list;
        }

        // Fetch ProbeInstall by part number
        public List<ProbeInstallModel> GetProbeInstallByPartNumber(string partNumber)
        {
            var list = new List<ProbeInstallModel>();
            string query = "SELECT ProbeID, PartNumber, ProbeName, InstalledDate, Status FROM ProbeInstall WHERE PartNumber = @PartNumber";

            using SqlConnection conn = new(_connectionString);
            using SqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@PartNumber", partNumber);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var item = new ProbeInstallModel
                {
                    ProbeID = reader.GetInt32(0),
                    PartNumber = reader.GetString(1),
                    ProbeName = reader.GetString(2),
                    InstalledDate = reader.GetDateTime(3),
                    Status = reader.GetString(4)
                };
                list.Add(item);
            }
            return list;
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }

    internal class PartConfigModel
    {
        public int PartID { get; set; }
        public string? PartNumber { get; set; }   // Added PartNumber
        public string? PartName { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    internal class ProbeInstallModel
    {
        public int ProbeID { get; set; }
        public string? PartNumber { get; set; }   // Added PartNumber
        public string? ProbeName { get; set; }
        public DateTime InstalledDate { get; set; }
        public string? Status { get; set; }
    }

    internal class MasterReadingDataModel
    {
        public string? Para_No { get; set; }
        public string? Parameter { get; set; }
        public double Nominal { get; set; }
        public double RTolPlus { get; set; }
        public double RTolMinus { get; set; }
    }

}
