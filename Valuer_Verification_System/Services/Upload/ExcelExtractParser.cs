using System.Globalization;
using ExcelDataReader;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Services.Upload
{
    /// <summary>
    /// Parses the weekly LIS extract XLS file into <see cref="ExtractRow"/> objects.
    ///
    /// Key notes:
    ///   • MARKET_VALUE and RATEABLE_AREA are stored as TEXT strings in the XLS
    ///     (e.g. "8245000.00").  We parse with InvariantCulture so the dot is always
    ///     treated as the decimal separator, regardless of the server's OS locale.
    ///   • Column look-up is case-insensitive.
    ///   • GetStringAny() tries alternate header names to handle minor naming differences
    ///     between export versions.
    /// </summary>
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
                    for (int c = 0; c < reader.FieldCount; c++)
                    {
                        var name = (reader.GetValue(c)?.ToString() ?? "").Trim();
                        if (!string.IsNullOrWhiteSpace(name) && !header.ContainsKey(name))
                            header[name] = c;
                    }
                    headerRead = true;
                    continue;
                }

                if (IsRowEmpty(reader)) continue;

                var g = new RowGetter(reader, header);

                var valuationKey = g.GetString("VALUATION_KEY");
                if (string.IsNullOrWhiteSpace(valuationKey))
                    continue;

                var r = new ExtractRow
                {
                    // ── Section 1: Core XLS columns ──────────────────────────────
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
                    RightsUnitType = g.GetString("RIGHTS_UNIT_TYPE"),
                    RightDescription = g.GetString("RIGHT_DESCRIPTION"),
                    RightsPremiseSize = g.GetDecimal("RIGHTS_PREMISE_SIZE"),
                    RightsStatus = g.GetString("RIGHTS_STATUS"),

                    ValuationKey = valuationKey.Trim(),
                    ValuationKey2 = g.GetString("VALUATION_KEY2"),
                    ValuationRollKey = g.GetString("VALUATION_ROLL_KEY"),
                    ValuationType = g.GetString("VALUATION_TYPE"),
                    ValuationDate = g.GetDateOnly("VALUATION_DATE"),
                    WefDate = g.GetDateOnly("WEF_DATE"),
                    StatusCode = g.GetString("STATUS_CODE"),

                    // TEXT columns stored as strings in LIS XLS — must use InvariantCulture
                    RateableArea = g.GetDecimal("RATEABLE_AREA"),
                    MarketValue = g.GetDecimal("MARKET_VALUE"),

                    ValuationSplitNo = g.GetString("VALUATION_SPLIT_NO"),
                    ValuationSplitInd = g.GetString("VALUATION_SPLIT_IND"),

                    CatCode = g.GetString("CAT_CODE"),
                    CatDesc = g.GetString("CAT_DESC"),

                    Sector = g.GetString("SECTOR"),
                    Valuer = g.GetString("VALUER"),

                    // Try every known header variant for SendNotice
                    SendNotice = g.GetStringAny(
                                            "SendNotices Y/N",
                                            "SEND NOTICE?",
                                            "SEND NOTICE",
                                            "SEND_NOTICE",
                                            "SENDNOTICE"),

                    // ── Section 2: Extended LIS / Week Table / Notice columns ────
                    Erf = g.GetString("ERF"),
                    Ptn = g.GetString("PTN"),
                    Re = g.GetString("RE"),
                    PremiseId = g.GetString("PREMISE_ID"),

                    NoticeOpen = g.GetString("NOTICE_OPEN"),
                    ExtractPeriod = g.GetString("EXTRACT_PERIOD"),
                    LetterDate = g.GetDateOnly("LETTER_DATE"),
                    ReviewOpenDate = g.GetDateOnly("REVIEW_OPEN_DATE"),
                    ReviewCloseDate = g.GetDateOnly("REVIEW_CLOSE_DATE"),
                    ReviewOpen = g.GetString("REVIEW_OPEN"),
                    ReviewCloseDate2 = g.GetDateOnly("REVIEW_CLOSE_DATE2"),
                    ReviewOpenDate2 = g.GetDateOnly("REVIEW_OPEN_DATE2"),

                    SchemeName = g.GetStringAny("Scheme_Name", "SCHEME_NAME"),
                    SchemeNo = g.GetStringAny("Scheme_No", "SCHEME_NO"),
                    SchemeYear = g.GetStringAny("Scheme_Year", "SCHEME_YEAR"),
                    SchemeNameDesc = g.GetString("SCHEME_NAME_DESC"),
                    SchemeNumber = g.GetString("SCHEME_NUMBER"),

                    StPropertyDescription = g.GetString("ST_PROPERTY_DESCRIPTION"),
                    PropertyDescription_ = g.GetString("PROPERTY_DESCRIPTION_"),
                    PropertyDesc = g.GetStringAny("PROPERTY_DESC", "PROPERTY_DESCRIPTION"),
                    PropertyCategory = g.GetString("PROPERTY_CATEGORY"),
                    LeasePropertyDesc = g.GetString("LEASE_PROP_DESC"),

                    OwnerName = g.GetStringAny("OwnerS_Name", "OWNER_NAME"),
                    Addr1 = g.GetString("ADDR1"),
                    Addr2 = g.GetString("ADDR2"),
                    Addr3 = g.GetString("ADDR3"),
                    Addr4 = g.GetString("ADDR4"),
                    Addr5 = g.GetString("ADDR5"),
                    PremiseAddress = g.GetString("PREMISE_ADDRESS"),
                    LisStreetAddress = g.GetString("LIS_STREET_ADDRESS"),
                    PhysicalAddress = g.GetString("PHYSICAL_ADDRESS"),
                    StAddr2Postal = g.GetString("ST_ADDR2_POSTAL"),
                    UnitNoChar = g.GetString("UNIT_NO_CHAR"),

                    EmailInd = g.GetString("EMAIL_IND"),
                    EmailAddr = g.GetString("EMAIL_ADDR"),

                    CojOwned = g.GetString("COJ_OWNED"),
                    SapData = g.GetString("SAP_DATA"),
                    Usable = g.GetString("USABLE"),
                    SplitPost = g.GetString("SPLIT_POST"),

                    RemarkDesc = g.GetString("REMARK_DESC"),
                    Remarks = g.GetString("REMARKS"),
                    Extent = g.GetString("EXTENT"),
                    EStatus = g.GetString("eSTATUS"),
                    LeaseUnitNo = g.GetString("LEASE_UNIT_NO"),

                    Decision = VerificationDecision.Pending
                };

                rows.Add(r);
            }

            return rows;
        }

        // ──────────────────────────────────────────────────────────────────
        private static bool IsRowEmpty(IExcelDataReader reader)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetValue(i) != null &&
                    !string.IsNullOrWhiteSpace(reader.GetValue(i)!.ToString()))
                    return false;
            }
            return true;
        }

        // ──────────────────────────────────────────────────────────────────
        private sealed class RowGetter
        {
            private readonly IExcelDataReader _r;
            private readonly Dictionary<string, int> _h;

            public RowGetter(IExcelDataReader r, Dictionary<string, int> h)
            {
                _r = r;
                _h = h;
            }

            /// <summary>Return the trimmed string value of a column, or null.</summary>
            public string? GetString(string col)
            {
                if (!_h.TryGetValue(col, out var idx)) return null;
                var v = _r.GetValue(idx)?.ToString();
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }

            /// <summary>
            /// Try each column name in turn; return the first non-null value found.
            /// Handles naming differences between LIS export versions.
            /// </summary>
            public string? GetStringAny(params string[] cols)
            {
                foreach (var col in cols)
                {
                    var v = GetString(col);
                    if (v != null) return v;
                }
                return null;
            }

            /// <summary>
            /// Parse a decimal column.
            ///
            /// IMPORTANT: MARKET_VALUE and RATEABLE_AREA are stored as TEXT strings
            /// in the LIS XLS (e.g. "8245000.00").  We MUST use InvariantCulture so
            /// that the dot is treated as the decimal separator regardless of whether
            /// the server OS is set to a locale that uses comma as decimal separator
            /// (which is common in South Africa and would cause plain TryParse to return null).
            /// </summary>
            public decimal? GetDecimal(string col)
            {
                if (!_h.TryGetValue(col, out var idx)) return null;

                var v = _r.GetValue(idx);
                if (v == null) return null;

                // ExcelDataReader returns numeric cells as double
                if (v is double d) return (decimal)d;
                if (v is float f) return (decimal)f;
                if (v is decimal m) return m;
                if (v is int i) return (decimal)i;
                if (v is long l) return (decimal)l;

                // Text cell — clean then parse with InvariantCulture
                var s = v.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(s)) return null;

                // Strip thousands separators, currency prefix (R), and any spaces
                s = s.Replace(",", "")
                     .Replace(" ", "")
                     .TrimStart('R', 'r');   // handle "R 220000" format

                return decimal.TryParse(
                    s,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var result)
                    ? result
                    : null;
            }

            public DateOnly? GetDateOnly(string col)
            {
                if (!_h.TryGetValue(col, out var idx)) return null;

                var v = _r.GetValue(idx);
                if (v == null) return null;

                if (v is DateTime dt) return DateOnly.FromDateTime(dt);

                var s = v.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(s)) return null;

                if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dtx))
                    return DateOnly.FromDateTime(dtx);

                return null;
            }
        }
    }
}