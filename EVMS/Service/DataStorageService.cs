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

        // ✅ Fetch Parameter Names directly from PartConfig
        public List<PartReadingDataModel> GetParametersByPartNumber(string partNumber)
        {
            var list = new List<PartReadingDataModel>();

            string query = @"
        SELECT *
        FROM PartConfig
        WHERE Para_No = @PartNumber";   // ✅ Assuming Para_No is linked to PartNumber

            using SqlConnection conn = new(_connectionString);
            using SqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@PartNumber", partNumber);

            conn.Open();
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var item = new PartReadingDataModel
                {
                    Para_No = reader["Para_No"].ToString(),
                    Parameter = reader["Parameter"].ToString(),
                    Nominal = Convert.ToDouble(reader["Nominal"]),
                    RTolPlus = Convert.ToDouble(reader["RTolPlus"]),
                    RTolMinus = Convert.ToDouble(reader["RTolMinus"]),
                    //YTolPlus = Convert.ToDouble(reader["YTolPlus"]),
                    //YTolMinus = Convert.ToDouble(reader["YTolMinus"]),
                    //ProbeStatus = reader["ProbeStatus"].ToString()
                };
                list.Add(item);
            }
            return list;
        }


        // ✅ Probe installation info
        //public List<ProbeInstallModel> GetProbeInstallByPartNumber(string partNumber)
        //{
        //    var list = new List<ProbeInstallModel>();
        //    string query = @"
        //        SELECT ProbeID, PartNumber, ProbeName, InstalledDate, Status
        //        FROM ProbeInstall
        //        WHERE PartNumber = @PartNumber AND Status = 'Active'";

            //    using SqlConnection conn = new(_connectionString);
            //    using SqlCommand cmd = new(query, conn);
            //    cmd.Parameters.AddWithValue("@PartNumber", partNumber);

            //    conn.Open();
            //    using SqlDataReader reader = cmd.ExecuteReader();
            //    while (reader.Read())
            //    {
            //        var item = new ProbeInstallModel
            //        {
            //            ProbeID = reader.GetInt32(0),
            //            PartNumber = reader.GetString(1),
            //            ProbeName = reader.GetString(2),
            //            InstalledDate = reader.GetDateTime(3),
            //            Status = reader.GetString(4)
            //        };
            //        list.Add(item);
            //    }
            //    return list;
            //}

        public void Dispose()
        {
            // Cleanup if needed
        }


        public List<ProbeInstallModel> GetProbeInstallByPartNumber(string partNumber)
        {
            var list = new List<ProbeInstallModel>();
            string query = @"
        SELECT PartNo,ProbeId,Name
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
                    ProbeId = reader.GetString(0),
                    PartNo = reader.GetString(1),
                    Name = reader.GetString(2),
                };
                list.Add(item);
            }
            return list;
        }

       
    }

    internal class PartReadingDataModel
    {
        public string? Para_No { get; set; }
        public string? Parameter { get; set; }
        public double Nominal { get; set; }
        public double RTolPlus { get; set; }
        public double RTolMinus { get; set; }
        //public double YTolPlus { get; set; }
        //public double YTolMinus { get; set; }
        //public string? ProbeStatus { get; set; }
    }


    //internal class ProbeInstallModel
    //{
    //    public int ProbeID { get; set; }
    //    public string? PartNumber { get; set; }
    //    public string? ProbeName { get; set; }
    //    public DateTime InstalledDate { get; set; }
    //    public string? Status { get; set; }
    //}

    internal class ProbeInstallModel
    {
        public  string? ProbeId { get; set; }
        public string? PartNo { get; set; }
        public string? Name { get; set; }
        
    }

}
