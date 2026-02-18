using ExcelDataReader;
using System.Data;
using System.Globalization;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Services.Upload
{
    public static class ExcelExtractParser
    {
        static ExcelExtractParser()
        {
            // Required for .xls (codepages)
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public static List<ExtractRow> ParseExtractRows(Stream excelStream)
        {
            using var reader = ExcelReaderFactory.CreateReader(excelStream);

            var ds = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            });

            if (ds.Tables.Count == 0)
                throw new InvalidOperationException("No worksheets found in the uploaded Excel.");

            var table = ds.Tables[0];
            if (table.Rows.Count == 0)
                return new();

            // Build a normalized header lookup
            var colMap = BuildColumnMap(table);

            string? GetString(DataRow row, string key)
            {
                if (!colMap.TryGetValue(key, out var idx)) return null;
                var v = row[idx];
                if (v == null || v == DBNull.Value) return null;
                return v.ToString()?.Trim();
            }

            decimal? GetDecimal(DataRow row, string key)
            {
                var s = GetString(row, key);
                if (string.IsNullOrWhiteSpace(s)) return null;

                // Remove commas and currency symbols if present
                s = s.Replace("R", "", StringComparison.OrdinalIgnoreCase)
                     .Replace(",", "")
                     .Trim();

                if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    return d;

                if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("en-ZA"), out d))
                    return d;

                return null;
            }

            DateOnly? GetDateOnly(DataRow row, string key)
            {
                if (!colMap.TryGetValue(key, out var idx)) return null;
                var v = row[idx];
                if (v == null || v == DBNull.Value) return null;

                if (v is DateTime dt)
                    return DateOnly.FromDateTime(dt);

                var s = v.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(s)) return null;

                // Try common formats
                if (DateTime.TryParse(s, CultureInfo.GetCultureInfo("en-ZA"), DateTimeStyles.None, out var parsed))
                    return DateOnly.FromDateTime(parsed);

                if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                    return DateOnly.FromDateTime(parsed);

                return null;
            }

            var rows = new List<ExtractRow>(table.Rows.Count);

            foreach (DataRow r in table.Rows)
            {
                var valuationKey = GetString(r, "VALUATION_KEY") ?? "";
                if (string.IsNullOrWhiteSpace(valuationKey))
                    continue; // skip blank lines

                var row = new ExtractRow
                {
                    ValuationKey = valuationKey,
                    Sector = GetString(r, "SECTOR"),
                    Valuer = GetString(r, "VALUER"),
                    MarketValue = GetDecimal(r, "MARKET_VALUE"),
                    PropertyDescription = GetString(r, "PROPERTY_DESC") ?? GetString(r, "PROPERTY_DESCRIPTION"),
                    PropertyType = GetString(r, "PROPERTY_TYPE"),
                    Category = GetString(r, "CATEGORY"),
                    ExtractionDate = GetDateOnly(r, "EXTRACTION_DATE"),
                    Decision = VerificationDecision.Pending
                };

                rows.Add(row);
            }

            return rows;
        }

        private static Dictionary<string, int> BuildColumnMap(DataTable table)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < table.Columns.Count; i++)
            {
                var raw = table.Columns[i].ColumnName ?? "";
                var norm = NormalizeHeader(raw);

                // store both raw and normalized keys
                if (!map.ContainsKey(raw)) map[raw] = i;
                if (!map.ContainsKey(norm)) map[norm] = i;

                // Friendly aliases
                if (norm is "PROPERTY_DESC" or "PROPERTYDESCRIPTION" or "PROPERTY_DESCRIPTION")
                    map["PROPERTY_DESC"] = i;

                if (norm is "VALUATIONKEY")
                    map["VALUATION_KEY"] = i;
            }

            // Ensure standard names exist if matching headers differ slightly
            EnsureKey(map, "VALUATION_KEY", "VALUATIONKEY");
            EnsureKey(map, "MARKET_VALUE", "MARKETVALUE");
            EnsureKey(map, "EXTRACTION_DATE", "EXTRACTIONDATE");
            EnsureKey(map, "PROPERTY_TYPE", "PROPERTYTYPE");
            EnsureKey(map, "PROPERTY_DESC", "PROPERTYDESCRIPTION", "PROPERTY_DESCRIPTION");
            EnsureKey(map, "SECTOR", "SECTOR");
            EnsureKey(map, "VALUER", "VALUER");

            return map;
        }

        private static void EnsureKey(Dictionary<string, int> map, string key, params string[] alternatives)
        {
            if (map.ContainsKey(key)) return;
            foreach (var alt in alternatives)
            {
                if (map.TryGetValue(alt, out var idx))
                {
                    map[key] = idx;
                    return;
                }
            }
        }

        private static string NormalizeHeader(string s)
        {
            s = s.Trim();
            s = s.Replace(" ", "")
                 .Replace("_", "")
                 .Replace("-", "")
                 .Replace("/", "");
            return s.ToUpperInvariant();
        }
    }
}
