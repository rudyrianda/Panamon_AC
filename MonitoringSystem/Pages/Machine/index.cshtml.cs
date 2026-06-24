using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace MonitoringSystem.Controllers
{
    [Route("api/machine")]
    [ApiController]
    public class MachineController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public MachineController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // ─── Helper: buka koneksi ke MachineDB ───────────────────────────────────
        private SqlConnection OpenConn()
        {
            var connStr = _configuration.GetConnectionString("MachineConnection");
            if (string.IsNullOrEmpty(connStr))
                throw new InvalidOperationException("Connection string 'MachineConnection' tidak ditemukan.");
            var conn = new SqlConnection(connStr);
            conn.Open();
            return conn;
        }

        // ─── GET /api/machine/efficiency?month=X&year=Y ───────────────────────────
        // Ambil rata-rata OEE, OperatingRatio, Ability, Quality, Achievement per machine per bulan
        [HttpGet("efficiency")]
        public IActionResult GetMachineEfficiency([FromQuery] int month, [FromQuery] int year)
        {
            if (month < 1 || month > 12)
                return BadRequest(new { error = "Bulan tidak valid (1–12)." });
            if (year < 2000 || year > 2100)
                return BadRequest(new { error = "Tahun tidak valid." });

            var result = new List<object>();
            try
            {
                using var conn = OpenConn();
                var query = @"
                    SELECT
                        ml.MachineName,
                        ROUND(AVG(CAST(e.OEE            AS FLOAT)), 2) AS OEE,
                        ROUND(AVG(CAST(e.OperatingRatio AS FLOAT)), 2) AS OperatingRatio,
                        ROUND(AVG(CAST(e.Ability        AS FLOAT)), 2) AS Ability,
                        ROUND(AVG(CAST(e.Quality        AS FLOAT)), 2) AS Quality,
                        ROUND(AVG(CAST(e.Achievement    AS FLOAT)), 2) AS Achievement
                    FROM [dbo].[Efficiency] e
                    JOIN [dbo].[MachineList] ml ON ml.IdMachine = e.IdMachine
                    WHERE MONTH(e.[Date]) = @Month
                      AND YEAR(e.[Date])  = @Year
                    GROUP BY ml.MachineName
                    ORDER BY ml.MachineName";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Month", month);
                cmd.Parameters.AddWithValue("@Year", year);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new
                    {
                        machineName = reader["MachineName"]?.ToString() ?? "-",
                        oee = reader["OEE"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["OEE"]),
                        operatingRatio = reader["OperatingRatio"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["OperatingRatio"]),
                        ability = reader["Ability"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["Ability"]),
                        quality = reader["Quality"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["Quality"]),
                        achievement = reader["Achievement"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["Achievement"])
                    });
                }

                return Ok(result);
            }
            catch (SqlException sqlEx) { return StatusCode(500, new { error = $"Database error: {sqlEx.Message}" }); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        // ─── GET /api/machine/list ─────────────────────────────────────────────────
        // Daftar semua machine dari tabel MachineList
        [HttpGet("list")]
        public IActionResult GetMachineList()
        {
            var result = new List<object>();
            try
            {
                using var conn = OpenConn();
                var query = @"
                    SELECT IdMachine, MachineName
                    FROM [dbo].[MachineList]
                    ORDER BY MachineName";

                using var cmd = new SqlCommand(query, conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    result.Add(new
                    {
                        idMachine = Convert.ToInt32(reader["IdMachine"]),
                        machineName = reader["MachineName"]?.ToString() ?? "-"
                    });

                return Ok(result);
            }
            catch (SqlException sqlEx) { return StatusCode(500, new { error = $"Database error: {sqlEx.Message}" }); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        // ─── GET /api/machine/detail?machineName=X&month=Y&year=Z ─────────────────
        // Detail harian 1 machine: OEE + breakdown loss per shift
        [HttpGet("detail")]
        public IActionResult GetMachineDetail(
            [FromQuery] string machineName,
            [FromQuery] int month,
            [FromQuery] int year)
        {
            if (string.IsNullOrEmpty(machineName))
                return BadRequest(new { error = "machineName wajib diisi." });
            if (month < 1 || month > 12)
                return BadRequest(new { error = "Bulan tidak valid." });
            if (year < 2000 || year > 2100)
                return BadRequest(new { error = "Tahun tidak valid." });

            var result = new List<object>();
            try
            {
                using var conn = OpenConn();
                var query = @"
                    SELECT
                        e.ID,
                        ml.MachineName,
                        CONVERT(VARCHAR, e.[Date], 23) AS [Date],
                        e.Shift,
                        e.OEE,
                        e.OperatingRatio,
                        e.Ability,
                        e.Quality,
                        e.Achievement,
                        e.WorkingTime,
                        e.PlanQty,
                        e.GoodProductionQty,
                        e.DefectQty,
                        el.LossCategory,
                        el.LossGroup,
                        el.LossMinutes
                    FROM [dbo].[Efficiency] e
                    JOIN [dbo].[MachineList] ml ON ml.IdMachine = e.IdMachine
                    LEFT JOIN [dbo].[EfficiencyLoss] el ON el.EfficiencyID = e.ID
                    WHERE ml.MachineName = @MachineName
                      AND MONTH(e.[Date]) = @Month
                      AND YEAR(e.[Date])  = @Year
                    ORDER BY e.[Date], e.Shift, el.LossGroup, el.LossCategory";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@MachineName", machineName);
                cmd.Parameters.AddWithValue("@Month", month);
                cmd.Parameters.AddWithValue("@Year", year);

                // Group by header ID, flatten loss items
                var headers = new Dictionary<int, dynamic>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int id = Convert.ToInt32(reader["ID"]);
                    if (!headers.ContainsKey(id))
                    {
                        headers[id] = new
                        {
                            id = id,
                            machineName = reader["MachineName"]?.ToString() ?? "-",
                            date = reader["Date"]?.ToString() ?? "-",
                            shift = reader["Shift"]?.ToString() ?? "-",
                            oee = reader["OEE"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["OEE"]),
                            operatingRatio = reader["OperatingRatio"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["OperatingRatio"]),
                            ability = reader["Ability"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["Ability"]),
                            quality = reader["Quality"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["Quality"]),
                            achievement = reader["Achievement"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["Achievement"]),
                            workingTime = reader["WorkingTime"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["WorkingTime"]),
                            planQty = reader["PlanQty"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["PlanQty"]),
                            goodProductionQty = reader["GoodProductionQty"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["GoodProductionQty"]),
                            defectQty = reader["DefectQty"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["DefectQty"]),
                            lossItems = new List<object>()
                        };
                    }
                    if (reader["LossCategory"] != DBNull.Value)
                    {
                        ((List<object>)headers[id].lossItems).Add(new
                        {
                            lossCategory = reader["LossCategory"]?.ToString() ?? "",
                            lossGroup = reader["LossGroup"]?.ToString() ?? "",
                            lossMinutes = reader["LossMinutes"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["LossMinutes"])
                        });
                    }
                }

                return Ok(headers.Values.ToList());
            }
            catch (SqlException sqlEx) { return StatusCode(500, new { error = $"Database error: {sqlEx.Message}" }); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }
    }
}