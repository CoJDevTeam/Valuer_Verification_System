using ExcelDataReader;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Services.Upload
{
    public static class ExcelExtractParser
    {
        public static List<ExtractRow> ParseExtractRows(Stream excelStream)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            using var reader = ExcelReaderFactory.CreateReader(excelStream);

            var rows = new List<ExtractRow>();
            var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            bool headerRead = false;

            while (reader.Read())
            {
                if (!headerRead)
                {
                    // Build header map from row 0
                    for (int c = 0; c < reader.FieldCount; c++)
                    {
                        var name = (reader.GetValue(c)?.ToString() ?? "").Trim();
                        if (!string.IsNullOrWhiteSpace(name) && !header.ContainsKey(name))
                            header[name] = c;
                    }

                    headerRead = true;
                    continue;
                }

                // skip empty rows
                if (IsRowEmpty(reader)) continue;

                var g = new RowGetter(reader, header);

                var valuationKey = g.GetString("VALUATION_KEY");
                if (string.IsNullOrWhiteSpace(valuationKey))
                    continue;

                var r = new ExtractRow
                {
                    ExtractionDate = g.GetDateOnly("EXTRACTION_DATE"),
                    TownNameDesc = g.GetString("TOWN_NAME_DESC"),
                    PropertyId = g.GetString("PROPERTY_ID"),
                    SgId = g.GetString("SG_ID"),
                    StandNo = g.GetString("STAND_NO"),
                    StatusSubtype = g.GetString("STATUS_SUBTYPE"),

                    UnitKey = g.GetString("UNIT_KEY"),
                    UnitType = g.GetString("UNIT_TYPE"),
                    UnitNo = g.GetString("UNIT_NO"),
                    LegalArea = g.GetString("LEGAL_AREA"),

                    UnitRightsKey = g.GetString("UNIT_RIGHTS_KEY"),
                    RightsPremise = g.GetString("RIGHTS_PREMISE"),
                    RightDescription = g.GetString("RIGHT_DESCRIPTION"),
                    RightsPremiseSize = g.GetDecimal("RIGHTS_PREMISE_SIZE"),
                    RightsStatus = g.GetString("RIGHTS_STATUS"),

                    ValuationKey = valuationKey.Trim(),
                    ValuationRollKey = g.GetString("VALUATION_ROLL_KEY"),
                    ValuationType = g.GetString("VALUATION_TYPE"),
                    ValuationDate = g.GetDateOnly("VALUATION_DATE"),
                    WefDate = g.GetDateOnly("WEF_DATE"),
                    StatusCode = g.GetString("STATUS_CODE"),

                    RateableArea = g.GetDecimal("RATEABLE_AREA"),
                    MarketValue = g.GetDecimal("MARKET_VALUE"),

                    ValuationSplitNo = g.GetString("VALUATION_SPLIT_NO"),
                    ValuationSplitInd = g.GetString("VALUATION_SPLIT_IND"),

                    CatCode = g.GetString("CAT_CODE"),
                    CatDesc = g.GetString("CAT_DESC"),

                    Sector = g.GetString("SECTOR"),
                    SendNotice = g.GetString("SEND NOTICE?"),
                    Valuer = g.GetString("VALUER"),

                    Decision = VerificationDecision.Pending
                };

                rows.Add(r);
            }

            return rows;
        }

        private static bool IsRowEmpty(IExcelDataReader reader)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetValue(i) != null && !string.IsNullOrWhiteSpace(reader.GetValue(i)!.ToString()))
                    return false;
            }
            return true;
        }

        private sealed class RowGetter
        {
            private readonly IExcelDataReader _r;
            private readonly Dictionary<string, int> _h;

            public RowGetter(IExcelDataReader r, Dictionary<string, int> h)
            {
                _r = r;
                _h = h;
            }

            public string? GetString(string col)
            {
                if (!_h.TryGetValue(col, out var idx)) return null;
                var v = _r.GetValue(idx)?.ToString();
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }

            public decimal? GetDecimal(string col)
            {
                if (!_h.TryGetValue(col, out var idx)) return null;

                var v = _r.GetValue(idx);
                if (v == null) return null;

                if (v is double d) return (decimal)d;
                if (v is float f) return (decimal)f;
                if (v is decimal m) return m;

                var s = v.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(s)) return null;

                // strip commas/spaces
                s = s.Replace(",", "").Replace(" ", "");

                return decimal.TryParse(s, out var x) ? x : null;
            }

            public DateOnly? GetDateOnly(string col)
            {
                if (!_h.TryGetValue(col, out var idx)) return null;

                var v = _r.GetValue(idx);
                if (v == null) return null;

                if (v is DateTime dt) return DateOnly.FromDateTime(dt);

                var s = v.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(s)) return null;

                // Excel sometimes gives yyyy-MM-dd or dd/MM/yyyy depending on source
                if (DateTime.TryParse(s, out var dtx))
                    return DateOnly.FromDateTime(dtx);

                return null;
            }
        }
    }
}
